using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StorageManager.Core;
using StorageManager.Infrastructure;

namespace StorageManager.Api;

public static class EndpointExtensions
{
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/setup/status", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(new { required = !await db.Installations.AsNoTracking().AnyAsync(ct) })).AllowAnonymous();

        api.MapGet("/auth/csrf", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();

        api.MapPost("/setup", async (SetupRequest request, IConfiguration config, AppDbContext db,
            UserManager<AppUser> users, CancellationToken ct) =>
        {
            var configuredToken = config["Setup:Token"];
            if (string.IsNullOrWhiteSpace(configuredToken))
                throw new DomainException(503, "setup_unavailable", "A instalação ainda não possui uma chave de configuração.");
            if (!FixedTimeEquals(request.SetupToken, configuredToken))
                throw new DomainException(403, "setup_token_invalid", "A chave de instalação não é válida.");

            var organizationName = Rules.Text(request.OrganizationName, "organizationName", 160);
            var name = Rules.Text(request.Name, "name", 160);
            var email = Rules.Email(request.Email);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(721947001)", ct);
            if (await db.Installations.AnyAsync(ct))
                throw new DomainException(409, "already_configured", "Esta instalação já foi configurada.");

            var organization = new Organization { Name = organizationName };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(ct);
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                Name = name,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Role = Roles.Admin,
                IsActive = true
            };
            var created = await users.CreateAsync(user, request.Password);
            EnsureIdentity(created);
            db.Installations.Add(new Installation());
            db.AuditEvents.Add(new AuditEvent
            {
                OrganizationId = organization.Id,
                ActorId = user.Id,
                Action = "installation.setup",
                SubjectId = organization.Id.ToString()
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created("/api/v1/setup/status", new { organizationId = organization.Id, userId = user.Id });
        }).AllowAnonymous().RequireRateLimiting("login");

        api.MapPost("/auth/login", async (LoginRequest request, HttpContext http, AppDbContext db,
            UserManager<AppUser> users, SignInManager<AppUser> signIn, CancellationToken ct) =>
        {
            var email = Rules.Email(request.Email);
            var user = await users.FindByEmailAsync(email);
            if (user is null || !user.IsActive)
                throw new DomainException(401, "invalid_credentials", "E-mail ou senha inválidos.");
            var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!check.Succeeded)
                throw new DomainException(401, "invalid_credentials", check.IsLockedOut
                    ? "Conta temporariamente bloqueada por tentativas inválidas." : "E-mail ou senha inválidos.");
            await http.SignInAsync(IdentityConstants.ApplicationScheme, Security.Principal(user));
            var organizationName = await db.Organizations.Where(x => x.Id == user.OrganizationId).Select(x => x.Name).SingleAsync(ct);
            return Results.Ok(new UserDto(user.Id, user.Name, user.Email!, user.Role, user.OrganizationId, organizationName));
        }).AllowAnonymous().RequireRateLimiting("login");

        api.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        api.MapGet("/me", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var id = http.User.Actor();
            var org = http.User.Org();
            var result = await (from user in db.Users.AsNoTracking()
                                join organization in db.Organizations.AsNoTracking() on user.OrganizationId equals organization.Id
                                where user.Id == id && user.OrganizationId == org && user.IsActive
                                select new UserDto(user.Id, user.Name, user.Email!, user.Role, org, organization.Name)).SingleAsync(ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        api.MapGet("/users", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var items = await db.Users.AsNoTracking().Where(x => x.OrganizationId == org)
                .OrderBy(x => x.Name).Select(x => new AdminUserDto(x.Id, x.Name, x.Email!, x.Role, x.IsActive)).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization("Admin");

        api.MapPost("/users", async (CreateUserRequest request, HttpContext http, AppDbContext db,
            UserManager<AppUser> users, CancellationToken ct) =>
        {
            if (!Roles.IsValid(request.Role)) Rules.Invalid("role", "Perfil inválido.");
            var email = Rules.Email(request.Email);
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = http.User.Org(),
                Name = Rules.Text(request.Name, "name", 160),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Role = request.Role,
                IsActive = true
            };
            var result = await users.CreateAsync(user, request.Password);
            EnsureIdentity(result);
            db.AuditEvents.Add(Audit(http, "user.created", user.Id));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/users/{user.Id}", new AdminUserDto(user.Id, user.Name, user.Email!, user.Role, user.IsActive));
        }).RequireAuthorization("Admin");

        api.MapPatch("/users/{id:guid}/status", async (Guid id, UserStatusRequest request, HttpContext http,
            AppDbContext db, UserManager<AppUser> users, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var target = await db.Users.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct)
                ?? throw new DomainException(404, "not_found", "Usuário não encontrado.");
            if (!request.IsActive && target.Id == http.User.Actor())
                throw new DomainException(409, "self_disable", "Você não pode desativar sua própria conta.");
            if (!request.IsActive && target.Role == Roles.Admin &&
                !await db.Users.AnyAsync(x => x.OrganizationId == org && x.Role == Roles.Admin && x.IsActive && x.Id != id, ct))
                throw new DomainException(409, "last_admin", "A organização precisa manter ao menos um administrador ativo.");
            target.IsActive = request.IsActive;
            await users.UpdateSecurityStampAsync(target);
            db.AuditEvents.Add(Audit(http, request.IsActive ? "user.activated" : "user.deactivated", target.Id));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new AdminUserDto(target.Id, target.Name, target.Email!, target.Role, target.IsActive));
        }).RequireAuthorization("Admin");

        return api;
    }

    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/locations", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var items = await db.Locations.AsNoTracking().Where(x => x.OrganizationId == org)
                .OrderBy(x => x.Path).Select(x => Rules.Dto(x)).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        api.MapPost("/locations", async (CreateLocationRequest request, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var name = Rules.Text(request.Name, "name", 120);
            var parent = request.ParentId is null ? null : await db.Locations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == request.ParentId, ct)
                ?? throw new DomainException(400, "parent_not_found", "O local pai não foi encontrado.");
            var location = new Location
            {
                OrganizationId = org,
                Name = name,
                Code = Rules.Code(request.Code, "code", 40),
                Type = Rules.Text(request.Type, "type", 60),
                ParentId = parent?.Id,
                Path = parent is null ? name : $"{parent.Path} / {name}",
                CanStore = request.CanStore
            };
            db.Locations.Add(location);
            db.AuditEvents.Add(Audit(http, "location.created", location.Id));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/locations/{location.Id}", Rules.Dto(location));
        }).RequireAuthorization("Write");

        api.MapGet("/products", async (string? search, int? page, int? pageSize, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var paging = Rules.Pagination(page, pageSize);
            var term = Rules.Search(search);
            var query = db.Products.AsNoTracking().Where(x => x.OrganizationId == org);
            if (term is not null) query = query.Where(x => x.Code.ToUpper().Contains(term) || x.Name.ToUpper().Contains(term) || x.Category.ToUpper().Contains(term));
            var total = await query.CountAsync(ct);
            var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Code).Skip((paging.Page - 1) * paging.Size).Take(paging.Size).ToListAsync(ct);
            var items = rows.Select(Rules.Dto).ToList();
            return Results.Ok(new PageDto<ProductDto>(items, total, paging.Page, paging.Size));
        }).RequireAuthorization();

        api.MapPost("/products", async (CreateProductRequest request, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (request.Kind is not ("Consumable" or "Returnable")) Rules.Invalid("kind", "Tipo de item inválido.");
            if (request.QuantityScale is < 0 or > 6) Rules.Invalid("quantityScale", "A precisão deve estar entre 0 e 6.");
            var product = new Product
            {
                OrganizationId = http.User.Org(),
                Code = Rules.Code(request.Code, "code", 60),
                Name = Rules.Text(request.Name, "name", 160),
                Category = Rules.Text(request.Category, "category", 100),
                Unit = Rules.Text(request.Unit, "unit", 20),
                Kind = request.Kind,
                MinimumStock = Rules.Quantity(request.MinimumStock, "minimumStock", request.QuantityScale, positive: false),
                QuantityScale = request.QuantityScale
            };
            db.Products.Add(product);
            db.AuditEvents.Add(Audit(http, "product.created", product.Id));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/products/{product.Id}", Rules.Dto(product));
        }).RequireAuthorization("Write");
        return api;
    }

    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/movements", async (CreateMovementRequest request, HttpRequest rawRequest, HttpContext http,
            InventoryService inventory, CancellationToken ct) =>
        {
            var key = rawRequest.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
                throw new DomainException(400, "idempotency_required", "Informe uma Idempotency-Key válida.");
            var result = await inventory.PostAsync(request, key, http.User, ct);
            return result.Replayed ? Results.Ok(result.Movement) : Results.Created($"/api/v1/movements/{result.Movement.Id}", result.Movement);
        }).RequireAuthorization("Write");

        api.MapGet("/stock", async (string? search, Guid? locationId, bool? lowStockOnly, int? page, int? pageSize,
            HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var paging = Rules.Pagination(page, pageSize);
            var term = Rules.Search(search);
            var query = from balance in db.StockBalances.AsNoTracking()
                        join product in db.Products.AsNoTracking() on new { balance.OrganizationId, Id = balance.ProductId } equals new { product.OrganizationId, product.Id }
                        join location in db.Locations.AsNoTracking() on new { balance.OrganizationId, Id = balance.LocationId } equals new { location.OrganizationId, location.Id }
                        where balance.OrganizationId == org
                        select new { balance, product, location };
            if (term is not null) query = query.Where(x => x.product.Code.ToUpper().Contains(term) || x.product.Name.ToUpper().Contains(term) || x.location.Path.ToUpper().Contains(term));
            if (locationId is not null) query = query.Where(x => x.location.Id == locationId);
            if (lowStockOnly == true) query = query.Where(x => x.balance.Quantity <= x.product.MinimumStock);
            var total = await query.CountAsync(ct);
            var raw = await query.OrderBy(x => x.product.Name).ThenBy(x => x.location.Path)
                .Skip((paging.Page - 1) * paging.Size).Take(paging.Size).ToListAsync(ct);
            var items = raw.Select(x => ToStock(x.balance, x.product, x.location)).ToList();
            return Results.Ok(new PageDto<StockDto>(items, total, paging.Page, paging.Size));
        }).RequireAuthorization();

        api.MapGet("/movements", async (string? search, string? type, int? page, int? pageSize,
            HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var paging = Rules.Pagination(page, pageSize);
            var term = Rules.Search(search);
            var query = db.StockMovements.AsNoTracking().Where(x => x.OrganizationId == org);
            if (term is not null) query = query.Where(x => x.ProductCode.ToUpper().Contains(term) || x.ProductName.ToUpper().Contains(term) || x.LocationPath.ToUpper().Contains(term) || x.Reference.ToUpper().Contains(term) || x.Recipient.ToUpper().Contains(term));
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (type is not ("Receipt" or "Consumption")) Rules.Invalid("type", "Tipo de movimentação inválido.");
                query = query.Where(x => x.Type == type);
            }
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((paging.Page - 1) * paging.Size).Take(paging.Size).ToListAsync(ct);
            var items = rows.Select(Rules.Dto).ToList();
            return Results.Ok(new PageDto<MovementDto>(items, total, paging.Page, paging.Size));
        }).RequireAuthorization();

        api.MapGet("/dashboard", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var org = http.User.Org();
            var productCount = await db.Products.CountAsync(x => x.OrganizationId == org, ct);
            var locationCount = await db.Locations.CountAsync(x => x.OrganizationId == org, ct);
            var positionCount = await db.StockBalances.CountAsync(x => x.OrganizationId == org, ct);
            var lowStock = await (from balance in db.StockBalances.AsNoTracking()
                                  join product in db.Products.AsNoTracking() on new { balance.OrganizationId, Id = balance.ProductId } equals new { product.OrganizationId, product.Id }
                                  where balance.OrganizationId == org && balance.Quantity <= product.MinimumStock
                                  select balance).CountAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var today = now - now.TimeOfDay;
            var todayCount = await db.StockMovements.CountAsync(x => x.OrganizationId == org && x.CreatedAt >= today, ct);
            var recentRows = await db.StockMovements.AsNoTracking().Where(x => x.OrganizationId == org)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(6).ToListAsync(ct);
            var recent = recentRows.Select(Rules.Dto).ToList();
            var lowRaw = await (from balance in db.StockBalances.AsNoTracking()
                                join product in db.Products.AsNoTracking() on new { balance.OrganizationId, Id = balance.ProductId } equals new { product.OrganizationId, product.Id }
                                join location in db.Locations.AsNoTracking() on new { balance.OrganizationId, Id = balance.LocationId } equals new { location.OrganizationId, location.Id }
                                where balance.OrganizationId == org && balance.Quantity <= product.MinimumStock
                                orderby balance.Quantity, product.Name
                                select new { balance, product, location }).Take(5).ToListAsync(ct);
            return Results.Ok(new DashboardDto(productCount, locationCount, positionCount, lowStock, todayCount,
                recent, lowRaw.Select(x => ToStock(x.balance, x.product, x.location)).ToList()));
        }).RequireAuthorization();
        return api;
    }

    private static StockDto ToStock(StockBalance balance, Product product, Location location)
    {
        var status = balance.Quantity == 0 ? "Empty" : balance.Quantity <= product.MinimumStock ? "Low" : "Healthy";
        return new StockDto(product.Id, product.Code, product.Name, product.Category, product.Unit, product.Kind,
            location.Id, location.Path, Rules.Decimal(balance.Quantity), "0", Rules.Decimal(balance.Quantity),
            Rules.Decimal(product.MinimumStock), status);
    }

    private static AuditEvent Audit(HttpContext http, string action, Guid subject) => new()
    {
        OrganizationId = http.User.Org(),
        ActorId = http.User.Actor(),
        Action = action,
        SubjectId = subject.ToString()
    };

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var left = Encoding.UTF8.GetBytes(supplied ?? "");
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static void EnsureIdentity(IdentityResult result)
    {
        if (result.Succeeded) return;
        var errors = result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray());
        throw new DomainException(400, "identity_validation", string.Join(" ", result.Errors.Select(x => x.Description)), errors);
    }
}

public sealed record MovementResult(MovementDto Movement, bool Replayed);

public sealed class InventoryService(AppDbContext db)
{
    public async Task<MovementResult> PostAsync(CreateMovementRequest request, string idempotencyKey,
        System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct)
    {
        if (request.Type is not ("Receipt" or "Consumption")) Rules.Invalid("type", "Tipo de movimentação inválido.");
        var org = principal.Org();
        var actor = principal.Actor();
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == request.ProductId && x.IsActive, ct)
            ?? throw new DomainException(404, "product_not_found", "Produto não encontrado.");
        var location = await db.Locations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == request.LocationId && x.CanStore, ct)
            ?? throw new DomainException(400, "location_not_storable", "Escolha um local que possa armazenar itens.");
        if (request.Type == "Consumption" && product.Kind != "Consumable")
            throw new DomainException(409, "loan_required", "Itens retornáveis devem sair pelo fluxo de empréstimo.");
        var quantity = Rules.Quantity(request.Quantity, "quantity", product.QuantityScale, positive: true);
        var reference = Rules.Text(request.Reference, "reference", 120, required: false);
        var notes = Rules.Text(request.Notes, "notes", 2000, required: false);
        var recipient = Rules.Text(request.Recipient, "recipient", 160, required: request.Type == "Consumption");
        var canonical = JsonSerializer.Serialize(new
        {
            request.Type,
            request.ProductId,
            request.LocationId,
            Quantity = Rules.Decimal(quantity),
            reference,
            notes,
            recipient
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockAsync($"idem:{org}:{actor}:{idempotencyKey}", ct);
        var existing = await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == org && x.ActorId == actor && x.Key == idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash)
                throw new DomainException(409, "idempotency_mismatch", "Esta chave já foi usada com outros dados.");
            var replay = JsonSerializer.Deserialize<MovementDto>(existing.ResultJson, JsonOptions())!;
            await transaction.CommitAsync(ct);
            return new MovementResult(replay, true);
        }

        await LockAsync($"stock:{org}:{product.Id}:{location.Id}", ct);
        var balance = await db.StockBalances.SingleOrDefaultAsync(x => x.OrganizationId == org && x.ProductId == product.Id && x.LocationId == location.Id, ct);
        if (balance is null)
        {
            if (request.Type == "Consumption")
                throw new DomainException(409, "insufficient_stock", "Não há saldo disponível nesse local.");
            balance = new StockBalance { OrganizationId = org, ProductId = product.Id, LocationId = location.Id };
            db.StockBalances.Add(balance);
        }
        if (request.Type == "Consumption" && balance.Quantity < quantity)
            throw new DomainException(409, "insufficient_stock", $"Saldo insuficiente. Disponível: {Rules.Decimal(balance.Quantity)} {product.Unit}.");
        balance.Quantity += request.Type == "Receipt" ? quantity : -quantity;
        var movement = new StockMovement
        {
            OrganizationId = org,
            Type = request.Type,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            LocationId = location.Id,
            LocationPath = location.Path,
            Quantity = quantity,
            Unit = product.Unit,
            Reference = reference,
            Notes = notes,
            Recipient = recipient,
            ActorId = actor,
            ActorName = principal.ActorName()
        };
        db.StockMovements.Add(movement);
        db.StockEntries.AddRange(
            new StockEntry { OrganizationId = org, MovementId = movement.Id, ProductId = product.Id, LocationId = location.Id, Account = "Warehouse", Quantity = request.Type == "Receipt" ? quantity : -quantity },
            new StockEntry { OrganizationId = org, MovementId = movement.Id, ProductId = product.Id, LocationId = null, Account = "External", Quantity = request.Type == "Receipt" ? -quantity : quantity });
        db.AuditEvents.Add(new AuditEvent { OrganizationId = org, ActorId = actor, Action = $"movement.{request.Type.ToLowerInvariant()}", SubjectId = movement.Id.ToString() });
        await db.SaveChangesAsync(ct);
        var dto = Rules.Dto(movement);
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            OrganizationId = org,
            ActorId = actor,
            Key = idempotencyKey,
            RequestHash = hash,
            MovementId = movement.Id,
            ResultJson = JsonSerializer.Serialize(dto, JsonOptions())
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new MovementResult(dto, false);
    }

    private Task LockAsync(string value, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({value}, 0))", ct);

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);
}

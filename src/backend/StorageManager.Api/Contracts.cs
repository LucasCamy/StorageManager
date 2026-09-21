using System.Globalization;
using System.Text.RegularExpressions;
using StorageManager.Core;

namespace StorageManager.Api;

public sealed record SetupRequest(string OrganizationName, string Name, string Email, string Password, string SetupToken);
public sealed record LoginRequest(string Email, string Password);
public sealed record CreateUserRequest(string Name, string Email, string Password, string Role);
public sealed record UserStatusRequest(bool IsActive);
public sealed record CreateLocationRequest(string Name, string Code, string Type, Guid? ParentId, bool CanStore);
public sealed record CreateProductRequest(string Code, string Name, string Category, string Unit, string Kind, string MinimumStock, int QuantityScale);
public sealed record CreateMovementRequest(string Type, Guid ProductId, Guid LocationId, string Quantity, string? Reference, string? Notes, string? Recipient);
public sealed record UserDto(Guid Id, string Name, string Email, string Role, Guid OrganizationId, string OrganizationName);
public sealed record AdminUserDto(Guid Id, string Name, string Email, string Role, bool IsActive);
public sealed record LocationDto(Guid Id, string Name, string Code, string Type, Guid? ParentId, string Path, bool CanStore);
public sealed record ProductDto(Guid Id, string Code, string Name, string Category, string Unit, string Kind, string MinimumStock, int QuantityScale, bool IsActive);
public sealed record StockDto(Guid ProductId, string ProductCode, string ProductName, string Category, string Unit, string Kind, Guid LocationId, string LocationPath, string Quantity, string Reserved, string Available, string MinimumStock, string Status);
public sealed record MovementDto(Guid Id, string Number, string Type, Guid ProductId, string ProductCode, string ProductName, Guid LocationId, string LocationPath, string Quantity, string Unit, string Reference, string Notes, string Recipient, string ActorName, DateTimeOffset CreatedAt);
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record DashboardDto(int ProductCount, int LocationCount, int StockedPositionCount, int LowStockCount, int TodayMovementCount, IReadOnlyList<MovementDto> RecentMovements, IReadOnlyList<StockDto> LowStockItems);

public sealed class DomainException(int status, string code, string detail, IDictionary<string, string[]>? errors = null) : Exception(detail)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}

public static partial class Rules
{
    public const decimal MaxQuantity = 99999999999999.999999m;
    [GeneratedRegex(@"^\d{1,14}(\.\d{1,6})?$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalFormat();

    public static string Text(string? value, string field, int max, bool required = true)
    {
        var clean = value?.Trim() ?? "";
        if ((required && clean.Length == 0) || clean.Length > max || clean.Any(char.IsControl))
            Invalid(field, $"Informe {field} com {(required ? "1" : "0")} a {max} caracteres, sem caracteres de controle.");
        return clean;
    }

    public static string Email(string? value)
    {
        var email = Text(value, "email", 254).ToLowerInvariant();
        try { _ = new System.Net.Mail.MailAddress(email); }
        catch { Invalid("email", "Informe um endereço de e-mail válido."); }
        return email;
    }

    public static string Code(string? value, string field, int max)
    {
        var code = Text(value, field, max).ToUpperInvariant();
        if (!Regex.IsMatch(code, @"^[A-Z0-9][A-Z0-9._/-]*$", RegexOptions.CultureInvariant))
            Invalid(field, "Use letras, números, ponto, hífen, barra ou sublinhado.");
        return code;
    }

    public static decimal Quantity(string? value, string field, int scale, bool positive)
    {
        if (value is null || !DecimalFormat().IsMatch(value) || !decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity))
        {
            Invalid(field, "Use quantidade decimal como texto, ponto como separador e até 14 dígitos inteiros e 6 decimais.");
            return 0;
        }
        if ((positive && quantity <= 0) || quantity > MaxQuantity || decimal.Round(quantity, scale) != quantity)
            Invalid(field, $"A quantidade deve ser {(positive ? "positiva" : "não negativa")} e respeitar até {scale} casas decimais.");
        return quantity;
    }

    public static void Invalid(string field, string message) => throw new DomainException(400, "validation_error", message, new Dictionary<string, string[]> { [field] = [message] });
    public static string Decimal(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    public static LocationDto Dto(Location x) => new(x.Id, x.Name, x.Code, x.Type, x.ParentId, x.Path, x.CanStore);
    public static ProductDto Dto(Product x) => new(x.Id, x.Code, x.Name, x.Category, x.Unit, x.Kind, Decimal(x.MinimumStock), x.QuantityScale, x.IsActive);
    public static MovementDto Dto(StockMovement x) => new(x.Id, $"MOV-{x.Sequence:D10}", x.Type, x.ProductId, x.ProductCode, x.ProductName, x.LocationId, x.LocationPath, Decimal(x.Quantity), x.Unit, x.Reference, x.Notes, x.Recipient, x.ActorName, x.CreatedAt);
    public static (int Page, int Size) Pagination(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? 25;
        if (p < 1 || p > 1000000 || size < 1 || size > 100) Invalid("page", "Página deve ser positiva e pageSize deve estar entre 1 e 100.");
        return (p, size);
    }
    public static string? Search(string? search) => string.IsNullOrWhiteSpace(search) ? null : Text(search, "search", 160).ToUpperInvariant();
}

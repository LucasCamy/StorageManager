namespace StorageManager.Core;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    public static bool IsValid(string? role) => role is Admin or Operator or Viewer;
}

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}

public sealed class Installation
{
    public int Id { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public Guid? ParentId { get; set; }
    public string Path { get; set; } = "";
    public bool CanStore { get; set; }
}

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Kind { get; set; } = "Consumable";
    public decimal MinimumStock { get; set; }
    public int QuantityScale { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StockBalance
{
    public Guid OrganizationId { get; set; }
    public Guid ProductId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public long Sequence { get; set; }
    public string Type { get; set; } = "";
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public Guid LocationId { get; set; }
    public string LocationPath { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Recipient { get; set; } = "";
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// Every movement has one Warehouse entry and its opposite External entry.
public sealed class StockEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public string Account { get; set; } = "";
    public decimal Quantity { get; set; }
}

public sealed class IdempotencyRecord
{
    public Guid OrganizationId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid MovementId { get; set; }
    public string ResultJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

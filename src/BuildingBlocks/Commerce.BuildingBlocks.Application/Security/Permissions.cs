namespace Commerce.BuildingBlocks.Application.Security;

/// <summary>Catalogs permissions for implemented public application capabilities.</summary>
public static class Permissions
{
    /// <summary>Reads product records.</summary>
    public const string ProductRead = "catalog.products.read";
    /// <summary>Creates draft products.</summary>
    public const string ProductCreate = "catalog.products.create";
    /// <summary>Updates and activates products.</summary>
    public const string ProductUpdate = "catalog.products.update";
    /// <summary>Deactivates products.</summary>
    public const string ProductDeactivate = "catalog.products.deactivate";
    /// <summary>Reads physical stock.</summary>
    public const string InventoryRead = "inventory.read";
    /// <summary>Adjusts physical stock.</summary>
    public const string InventoryAdjust = "inventory.adjust";
    /// <summary>Reads leased reservations.</summary>
    public const string ReservationRead = "inventory.reservations.read";
    /// <summary>Reads orders across customer ownership.</summary>
    public const string OrderRead = "orders.read";
    /// <summary>Reads payment records.</summary>
    public const string PaymentRead = "payments.read";
    /// <summary>Reads shipment records.</summary>
    public const string ShipmentRead = "shipments.read";
    /// <summary>Advances the implemented fulfilment lifecycle.</summary>
    public const string ShipmentUpdate = "shipments.update";
    /// <summary>Reads the safe user directory.</summary>
    public const string UserRead = "users.read";
    /// <summary>Manages role assignments and role permissions.</summary>
    public const string RoleManage = "users.roles.manage";
    /// <summary>Views Gateway diagnostics.</summary>
    public const string SystemRead = "system.health.read";
    /// <summary>Enters the administrative portal.</summary>
    public const string BackofficeAccess = "backoffice.access";
    /// <summary>Gets every supported permission name.</summary>
    public static IReadOnlyCollection<string> All { get; } = [ProductRead, ProductCreate, ProductUpdate, ProductDeactivate, InventoryRead, InventoryAdjust, ReservationRead, OrderRead, PaymentRead, ShipmentRead, ShipmentUpdate, UserRead, RoleManage, SystemRead, BackofficeAccess];
    /// <summary>JWT/Identity claim type containing one effective permission.</summary>
    public const string ClaimType = "permission";
    /// <summary>Policy for owner access or elevated order-read permission.</summary>
    public const string OrderResourcePolicy = "resource.order";
}

/// <summary>Describes resource ownership independently of service domain types.</summary>
/// <param name="OwnerId">Authenticated resource owner's identifier.</param>
public sealed record OwnedResource(Guid OwnerId);

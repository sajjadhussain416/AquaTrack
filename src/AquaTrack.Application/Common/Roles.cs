namespace AquaTrack.Application.Common;

/// <summary>The four roles from SRS AUTH-02. Use these constants in [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string PlantOwner = "PlantOwner";
    public const string PlantManager = "PlantManager";
    public const string DeliveryBoy = "DeliveryBoy";

    public const string OwnerOrManager = PlantOwner + "," + PlantManager;

    public static readonly string[] All = { SuperAdmin, PlantOwner, PlantManager, DeliveryBoy };
}

/// <summary>Custom claim types added to the login cookie.</summary>
public static class AppClaims
{
    public const string PlantId = "plant_id";
    public const string FullName = "full_name";
}

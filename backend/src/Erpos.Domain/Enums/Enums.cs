namespace Erpos.Domain.Enums;

public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Trial = 3
}

public enum IndustryType
{
    General = 0,
    Ngo = 1,
    Tourism = 2,
    Hotel = 3,
    Travel = 4,
    Logistics = 5,
    SoftwareServices = 6,
    Retail = 7,
    Manufacturing = 8
}

/// <summary>
/// Broad user category. Real access is decided by role assignments and permission overrides;
/// UserType only marks the two bypass levels (PlatformAdmin, SuperAdmin) and helps the UI.
/// </summary>
public enum UserType
{
    PlatformAdmin = 1,
    SuperAdmin = 2,
    Admin = 3,
    Manager = 4,
    Employee = 5
}

public enum EmailStatus { Pending = 1, Sent = 2, Failed = 3 }

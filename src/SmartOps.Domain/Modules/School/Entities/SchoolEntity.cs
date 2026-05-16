using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.School.Entities;

public sealed class SchoolEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public string SchoolCode { get; set; } = string.Empty;

    public string? RegistrationNumber { get; set; }

    public string? AffiliatedBoard { get; set; }

    public string? SchoolType { get; set; }

    public int? EstablishedYear { get; set; }

    public string? AboutSchool { get; set; }

    public string? StreetAddress { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Pincode { get; set; }

    public string Country { get; set; } = "India";

    public string? Timezone { get; set; }

    public string? GoogleMapsLink { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? AlternatePhone { get; set; }

    public string? Fax { get; set; }

    public string? PrimaryEmail { get; set; }

    public string? PrincipalEmail { get; set; }

    public string? Website { get; set; }

    public string? LogoUrl { get; set; }

    public string? FaviconUrl { get; set; }

    public string? Tagline { get; set; }

    public string? ShortName { get; set; }

    public string PrimaryColor { get; set; } = "#639922";

    public string? SecondaryColor { get; set; }

    public string? AccentColor { get; set; }

    public string? TextOnPrimary { get; set; }

    public string? CustomDomain { get; set; }

    public string? SslCertificate { get; set; }

    public string? AcademicYearFormat { get; set; }

    public string? CurrentAcademicYear { get; set; }

    public string? GradingSystem { get; set; }

    public int? PassingPercentage { get; set; }

    public string? WorkingDaysPerWeek { get; set; }

    public string? SchoolTiming { get; set; }

    public string? ClassesFrom { get; set; }

    public string? ClassesTo { get; set; }

    public int? SectionsPerClass { get; set; }

    public string? SectionNaming { get; set; }

    public int? MaxStudentsPerSection { get; set; }

    public string? AdmissionNumberFormat { get; set; }

    public string? AttendanceType { get; set; }

    public int? MinimumAttendancePercent { get; set; }

    public int? LateMarkAfterMinutes { get; set; }

    public bool AutoNotifyParentsOnAbsence { get; set; } = true;

    public bool AllowBackdatedAttendance { get; set; }

    public string Currency { get; set; } = "INR";

    public string? PaymentCycle { get; set; }

    public int? FeeDueDay { get; set; }

    public string? LateFeeType { get; set; }

    public decimal? LateFeeValue { get; set; }

    public int? GracePeriodDays { get; set; }

    [DbJsonb]
    public string? FeeHeadsJson { get; set; }

    [DbJsonb]
    public string? DiscountTypesJson { get; set; }

    [DbJsonb]
    public string? PaymentMethodsJson { get; set; }

    [DbJsonb]
    public string? PortalSettingsJson { get; set; }

    public string? SchemaName { get; set; }

    public string? StoragePlan { get; set; }

    public string? DataRegion { get; set; }

    public int? SessionTimeoutMinutes { get; set; }

    public string? PasswordPolicy { get; set; }

    public int? LoginAttemptsBeforeLock { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public bool IpWhitelistEnabled { get; set; }

    public bool BranchDataIsolation { get; set; } = true;

    public bool SharedFeeStructure { get; set; }

    public bool CentralAdminViewAllBranches { get; set; } = true;

    [DbIgnore]
    public List<SchoolBranchEntity> Branches { get; set; } = new();
}

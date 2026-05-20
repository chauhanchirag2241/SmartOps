using System.Text.Json;
using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Application.Modules.School;

public sealed class CreateSchoolDto
{
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
    public List<string> FeeHeads { get; set; } = new();
    public List<string> DiscountTypes { get; set; } = new();
    public List<string> PaymentMethods { get; set; } = new();
    public Dictionary<string, bool>? PortalSettings { get; set; }
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
    public List<SchoolBranchDto> Branches { get; set; } = new();

    public SchoolEntity ToEntity()
    {
        var entity = new SchoolEntity
        {
            Name = Name,
            Subdomain = Subdomain,
            SchoolCode = SchoolCode,
            RegistrationNumber = RegistrationNumber,
            AffiliatedBoard = AffiliatedBoard,
            SchoolType = SchoolType,
            EstablishedYear = EstablishedYear,
            AboutSchool = AboutSchool,
            StreetAddress = StreetAddress,
            City = City,
            State = State,
            Pincode = Pincode,
            Country = Country,
            Timezone = Timezone,
            GoogleMapsLink = GoogleMapsLink,
            Latitude = Latitude,
            Longitude = Longitude,
            PrimaryPhone = PrimaryPhone,
            AlternatePhone = AlternatePhone,
            Fax = Fax,
            PrimaryEmail = PrimaryEmail,
            PrincipalEmail = PrincipalEmail,
            Website = Website,
            LogoUrl = LogoUrl,
            FaviconUrl = FaviconUrl,
            Tagline = Tagline,
            ShortName = ShortName,
            PrimaryColor = PrimaryColor,
            SecondaryColor = SecondaryColor,
            AccentColor = AccentColor,
            TextOnPrimary = TextOnPrimary,
            CustomDomain = CustomDomain,
            SslCertificate = SslCertificate,
            AcademicYearFormat = AcademicYearFormat,
            CurrentAcademicYear = CurrentAcademicYear,
            GradingSystem = GradingSystem,
            PassingPercentage = PassingPercentage,
            WorkingDaysPerWeek = WorkingDaysPerWeek,
            SchoolTiming = SchoolTiming,
            ClassesFrom = ClassesFrom,
            ClassesTo = ClassesTo,
            SectionsPerClass = SectionsPerClass,
            SectionNaming = SectionNaming,
            MaxStudentsPerSection = MaxStudentsPerSection,
            AdmissionNumberFormat = AdmissionNumberFormat,
            AttendanceType = AttendanceType,
            MinimumAttendancePercent = MinimumAttendancePercent,
            LateMarkAfterMinutes = LateMarkAfterMinutes,
            AutoNotifyParentsOnAbsence = AutoNotifyParentsOnAbsence,
            AllowBackdatedAttendance = AllowBackdatedAttendance,
            Currency = Currency,
            PaymentCycle = PaymentCycle,
            FeeDueDay = FeeDueDay,
            LateFeeType = LateFeeType,
            LateFeeValue = LateFeeValue,
            GracePeriodDays = GracePeriodDays,
            FeeHeadsJson = SerializeList(FeeHeads),
            DiscountTypesJson = SerializeList(DiscountTypes),
            PaymentMethodsJson = SerializeList(PaymentMethods),
            PortalSettingsJson = PortalSettings is null ? null : JsonSerializer.Serialize(PortalSettings),
            SchemaName = SchemaName ?? $"school_{Subdomain.Replace('-', '_')}",
            StoragePlan = StoragePlan,
            DataRegion = DataRegion,
            SessionTimeoutMinutes = SessionTimeoutMinutes,
            PasswordPolicy = PasswordPolicy,
            LoginAttemptsBeforeLock = LoginAttemptsBeforeLock,
            TwoFactorEnabled = TwoFactorEnabled,
            IpWhitelistEnabled = IpWhitelistEnabled,
            BranchDataIsolation = BranchDataIsolation,
            SharedFeeStructure = SharedFeeStructure,
            CentralAdminViewAllBranches = CentralAdminViewAllBranches,
            IsActive = true,
            Branches = Branches.Select(b => new SchoolBranchEntity
            {
                Name = b.Name,
                Email = b.Email,
                Address = b.Address,
                IsHeadOffice = b.IsHeadOffice
            }).ToList()
        };

        if (entity.Branches.Count == 0)
        {
            entity.Branches.Add(new SchoolBranchEntity
            {
                Name = $"{Name} — Main Campus",
                IsHeadOffice = true
            });
        }

        return entity;
    }

    private static string? SerializeList(IReadOnlyList<string> values) =>
        values.Count == 0 ? null : JsonSerializer.Serialize(values);
}

public sealed class SchoolBranchDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsHeadOffice { get; set; }
}

public sealed class SchoolDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public sealed class UpdateSchoolDto
{
    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;
}

public sealed record CreateSchoolResponse(string Message, Guid SchoolId);

/// <summary>
/// Public school profile for tenant UI bootstrap (no sensitive config).
/// </summary>
public sealed class SchoolBootstrapDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? Tagline { get; set; }

    public string? LogoUrl { get; set; }

    public string? FaviconUrl { get; set; }

    public string PrimaryColor { get; set; } = "#639922";

    public string? SecondaryColor { get; set; }

    public string? AccentColor { get; set; }

    public string? TextOnPrimary { get; set; }

    public string SchemaName { get; set; } = string.Empty;
}

public static class SchoolBootstrapMapping
{
    public static SchoolBootstrapDto ToBootstrapDto(this SchoolEntity school) =>
        new()
        {
            Id = school.Id,
            Name = school.Name,
            Subdomain = school.Subdomain,
            ShortName = school.ShortName,
            Tagline = school.Tagline,
            LogoUrl = school.LogoUrl,
            FaviconUrl = school.FaviconUrl,
            PrimaryColor = school.PrimaryColor,
            SecondaryColor = school.SecondaryColor,
            AccentColor = school.AccentColor,
            TextOnPrimary = school.TextOnPrimary,
            SchemaName = school.SchemaName ?? $"school_{school.Subdomain.Replace('-', '_')}",
        };
}
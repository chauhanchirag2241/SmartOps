using System.Globalization;
using System.Text.RegularExpressions;
using SmartOps.Application.Common.Excel;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.BulkImport;
using SmartOps.Application.Modules.Department;
using SmartOps.Application.Modules.Employee;
using SmartOps.Application.Modules.Employee.Import;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Employee;
using SmartOps.Infrastructure.Modules.Identity.Services;
using static SmartOps.Application.Modules.BulkImport.BulkImportRowHelpers;

namespace SmartOps.Infrastructure.Modules.Employee;

public sealed class EmployeeImportService(
    IExcelHelper excelHelper,
    IEmployeeRepository employeeRepository,
    IDepartmentRepository departmentRepository,
    IUserRepository userRepository,
    IBranchContext branchContext) : IEmployeeImportService
{
    public const string EmployeesSheet = "Employees";
    public const string InstructionsSheet = "Instructions";
    public const string LookupsSheet = "Lookups";

    private static readonly Regex EmployeeCodePattern = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MobilePattern = new(@"^\d{10}$", RegexOptions.Compiled);
    private static readonly Regex PanPattern = new("^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var departments = await departmentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var managers = await employeeRepository.GetReportingManagerDropdownAsync(cancellationToken).ConfigureAwait(false);

        var lookupRows = new List<IReadOnlyList<string>>();
        foreach (string userType in StaffUserTypes())
        {
            lookupRows.Add(["UserType", userType, "Use exact name in Employees → UserType"]);
        }

        foreach ((Guid _, string roleName, string description) in RoleNames.Defaults
                     .Where(r => !string.Equals(r.Name, RoleNames.SmartOpsAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            lookupRows.Add(["PortalRole", roleName, description]);
        }

        foreach (var dept in departments.Where(d => d.IsActive))
        {
            lookupRows.Add(["Department", dept.Name, dept.Code ?? ""]);
        }

        foreach (var mgr in managers)
        {
            lookupRows.Add(["ReportingManager", mgr.Name, "Match this exact name in Employees → ReportingManager"]);
        }

        lookupRows.Add(["Gender", "Male", ""]);
        lookupRows.Add(["Gender", "Female", ""]);
        lookupRows.Add(["Gender", "Other", ""]);
        lookupRows.Add(["PortalAccess", "Enabled", "Default if blank"]);
        lookupRows.Add(["PortalAccess", "Disabled", ""]);

        var employeeColumns = new List<ExcelColumnSpec>
        {
            new() { Header = "EmployeeCode", Required = true, Example = "EMP-001", Width = 14 },
            new() { Header = "FirstName", Required = true, Example = "Priya", Width = 14 },
            new() { Header = "LastName", Required = true, Example = "Shah", Width = 14 },
            new() { Header = "Dob", Required = true, Example = "15/08/1990", Width = 12 },
            new() { Header = "Gender", Required = true, Example = "Female", Width = 10 },
            new() { Header = "Mobile", Required = true, Example = "9876543210", Width = 13 },
            new() { Header = "Email", Required = true, Example = "priya.shah@school.com", Width = 22 },
            new() { Header = "JoiningDate", Required = true, Example = "01/04/2026", Width = 13 },
            new() { Header = "UserType", Required = true, Example = "Teacher", Width = 18 },
            new() { Header = "PortalRole", Required = true, Example = "Teacher", Width = 18 },
            new() { Header = "PortalAccess", Required = false, Example = "Enabled", Width = 12 },
            new() { Header = "Username", Required = false, Example = "priya.shah", Width = 14 },
            new() { Header = "BloodGroup", Required = false, Example = "B+", Width = 11 },
            new() { Header = "AadhaarNo", Required = false, Example = "123456789012", Width = 14 },
            new() { Header = "PanNo", Required = false, Example = "ABCDE1234F", Width = 12 },
            new() { Header = "AlternateMobile", Required = false, Example = "", Width = 14 },
            new() { Header = "Address", Required = false, Example = "Ahmedabad", Width = 18 },
            new() { Header = "Designation", Required = false, Example = "Senior Teacher", Width = 16 },
            new() { Header = "ExperienceYears", Required = false, Example = "5", Width = 12 },
            new() { Header = "Qualifications", Required = false, Example = "B.Ed; M.A", Width = 16 },
            new() { Header = "Department", Required = false, Example = departments.FirstOrDefault(d => d.IsActive)?.Name ?? "Academics", Width = 14 },
            new() { Header = "ReportingManager", Required = false, Example = managers.FirstOrDefault()?.Name ?? "", Width = 18 },
            new() { Header = "BankAccountNumber", Required = false, Example = "", Width = 16 },
            new() { Header = "BankIfscCode", Required = false, Example = "", Width = 12 },
            new() { Header = "BankName", Required = false, Example = "", Width = 14 },
            new() { Header = "ShiftStartTime", Required = false, Example = "09:00", Width = 12 },
            new() { Header = "ShiftEndTime", Required = false, Example = "17:00", Width = 12 },
        };

        var instructionNotes = new List<ExcelNoteLine>
        {
            new() { Kind = "warn", Text = "READ FIRST: Fill the Employees sheet, then Validate in SmartOps before Import. Do not rename sheet names or header titles." },
            new() { Kind = "required", Text = "RED headers = Required (EmployeeCode, FirstName, LastName, Dob, Gender, Mobile, Email, JoiningDate, UserType, PortalRole)." },
            new() { Kind = "optional", Text = "GREEN headers = Optional. You can leave them blank." },
            new() { Kind = "info", Text = "Dates must be dd/MM/yyyy or yyyy-MM-dd (example: 15/08/1990)." },
            new() { Kind = "tip", Text = "UserType, PortalRole, Department, and ReportingManager must match the Lookups sheet exactly (copy-paste recommended)." },
            new() { Kind = "info", Text = "Mobile must be 10 digits. EmployeeCode: letters/numbers starting with alphanumeric; may include . _ -" },
            new() { Kind = "warn", Text = "Portal username is firstname.lastname (or Username column). Username + Email must be unique in the school." },
            new() { Kind = "info", Text = "Import runs only when EVERY row is Valid. Fix Invalid rows, then re-validate before Import." },
            new() { Kind = "warn", Text = "Delete the grey EXAMPLE row before import (or leave it — import skips rows starting with (example))." },
            new() { Kind = "tip", Text = "Lookups sheet is reference only — do not type employee data there." },
        };

        var lookupNotes = new List<ExcelNoteLine>
        {
            new() { Kind = "info", Text = "Ready-made list of UserType, PortalRole, Department, ReportingManager, Gender, and PortalAccess values." },
            new() { Kind = "tip", Text = "Copy a Name from column B into the Employees sheet. Do not invent names that are not listed." },
            new() { Kind = "warn", Text = "Do not edit this sheet for import — it is reference only." },
        };

        return excelHelper.CreateImportTemplate(
        [
            new ExcelTemplateSheet
            {
                Name = InstructionsSheet,
                TabColorHex = "1565C0",
                BannerTitle = "SmartOps — Employee Bulk Import Guide",
                BannerSubtitle = "Follow these steps, then fill the Employees sheet.",
                Notes = instructionNotes,
                AddExampleRow = false,
                FreezeHeader = false
            },
            new ExcelTemplateSheet
            {
                Name = EmployeesSheet,
                TabColorHex = "2E7D32",
                BannerTitle = "Employees — enter one employee per row",
                BannerSubtitle = "Red = required  |  Green = optional  |  Copy UserType / PortalRole from Lookups",
                Columns = employeeColumns,
                AddExampleRow = true,
                FreezeHeader = true
            },
            new ExcelTemplateSheet
            {
                Name = LookupsSheet,
                TabColorHex = "6A1B9A",
                BannerTitle = "Lookups — copy these values",
                BannerSubtitle = "Reference list only — not for typing employee rows.",
                Notes = lookupNotes,
                Columns =
                [
                    new() { Header = "Type", Required = false, Width = 18 },
                    new() { Header = "Name", Required = false, Width = 28 },
                    new() { Header = "Extra", Required = false, Width = 40 },
                ],
                DataRows = lookupRows,
                AddExampleRow = false,
                ShowLegend = false,
                FreezeHeader = true
            }
        ]);
    }

    public async Task<EmployeeImportValidateResultDto> ValidateAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAndValidateCoreAsync(fileStream, cancellationToken).ConfigureAwait(false);

        if (parsed.Result.FileError is null
            && (parsed.Result.InvalidEmployees > 0 || parsed.Result.Employees.Any(e => e.Status != "Valid")))
        {
            fileStream.Position = 0;
            var bySheet = new Dictionary<string, IReadOnlyList<(int, string, string)>>(StringComparer.OrdinalIgnoreCase)
            {
                [EmployeesSheet] = parsed.Result.Employees
                    .Select(s => (s.RowNumber, s.Status, string.Join(", ", s.Errors)))
                    .ToList()
            };
            byte[] errorBytes = excelHelper.AppendStatusColumns(fileStream, bySheet);
            parsed.Result.ErrorFileBase64 = Convert.ToBase64String(errorBytes);
        }

        return parsed.Result;
    }

    public async Task<EmployeeImportCommitResultDto> CommitAsync(
        Stream fileStream,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAndValidateCoreAsync(fileStream, cancellationToken).ConfigureAwait(false);

        var commit = new EmployeeImportCommitResultDto
        {
            Validation = parsed.Result,
            FileError = parsed.Result.FileError,
            SkippedInvalidEmployees = parsed.Result.InvalidEmployees
        };

        if (parsed.Result.FileError is not null)
        {
            return commit;
        }

        if (parsed.Result.InvalidEmployees > 0)
        {
            commit.FileError =
                "Import cancelled. The entire file must be valid — fix all Invalid rows and validate again.";
            return commit;
        }

        if (parsed.ValidRows.Count == 0)
        {
            commit.FileError = "No valid rows to import.";
            return commit;
        }

        foreach (var row in parsed.ValidRows)
        {
            try
            {
                var entity = row.Dto.ToEntity();
                string username = UserProvisioningService.BuildUsername(
                    row.Dto.Personal.FirstName,
                    row.Dto.Personal.LastName,
                    row.Dto.Access.Username);
                Guid id = await employeeRepository
                    .CreateEmployeeAsync(entity, schoolId, cancellationToken)
                    .ConfigureAwait(false);
                _ = id;
                commit.CreatedEmployees++;
                commit.Created.Add(new EmployeeImportCreatedDto
                {
                    RowNumber = row.RowNumber,
                    EmployeeCode = row.Dto.Professional.EmployeeCode,
                    DisplayName = $"{row.Dto.Personal.FirstName} {row.Dto.Personal.LastName}".Trim(),
                    Username = username,
                    Status = "Active"
                });
            }
            catch (Exception ex)
            {
                commit.Failures.Add(new EmployeeImportCommitFailureDto
                {
                    RowNumber = row.RowNumber,
                    EmployeeCode = row.Dto.Professional.EmployeeCode,
                    DisplayName = $"{row.Dto.Personal.FirstName} {row.Dto.Personal.LastName}".Trim(),
                    Message = ex.Message
                });
            }
        }

        return commit;
    }

    private async Task<ParsedImport> ParseAndValidateCoreAsync(
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var result = new EmployeeImportValidateResultDto();

        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        if (branchContext.ActiveBranchId is null)
        {
            result.FileError = "Select a branch from the header before importing.";
            return new ParsedImport(result, []);
        }

        Guid branchId = branchContext.ActiveBranchId.Value;

        List<ExcelDataRow> employeeRows;
        try
        {
            using var copy = new MemoryStream();
            fileStream.Position = 0;
            await fileStream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
            copy.Position = 0;
            employeeRows = excelHelper.ReadSheet(copy, EmployeesSheet).ToList();
        }
        catch (Exception ex)
        {
            result.FileError = ex.Message;
            return new ParsedImport(result, []);
        }

        if (employeeRows.Count == 0)
        {
            result.FileError = "No data rows found. Fill the Employees sheet.";
            return new ParsedImport(result, []);
        }

        var departments = await departmentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var departmentByName = departments
            .Where(d => d.IsActive)
            .GroupBy(d => d.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var managers = await employeeRepository.GetReportingManagerDropdownAsync(cancellationToken).ConfigureAwait(false);
        var managerByName = managers
            .GroupBy(m => m.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var codeInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var usernameInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var emailInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var validRows = new List<ValidEmployeeRow>();

        foreach (var row in employeeRows)
        {
            var rowResult = new ImportRowResultDto { RowNumber = row.RowNumber };
            string code = Get(row, "EmployeeCode");
            string first = Get(row, "FirstName");
            string last = Get(row, "LastName");
            rowResult.EmployeeCode = code;
            rowResult.DisplayName = $"{first} {last}".Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                rowResult.Errors.Add("EmployeeCode is required.");
            }
            else if (!EmployeeCodePattern.IsMatch(code))
            {
                rowResult.Errors.Add("EmployeeCode must start with a letter or number and may contain . _ - only.");
            }
            else if (codeInFile.TryGetValue(code, out int priorCodeRow))
            {
                rowResult.Errors.Add($"Duplicate EmployeeCode in file (also on row {priorCodeRow}).");
            }
            else
            {
                codeInFile[code] = row.RowNumber;
                bool exists = await employeeRepository
                    .EmployeeCodeExistsAsync(code, branchId, null, cancellationToken)
                    .ConfigureAwait(false);
                if (exists)
                {
                    rowResult.Errors.Add("EmployeeCode already exists for this branch.");
                }
            }

            if (string.IsNullOrWhiteSpace(first))
            {
                rowResult.Errors.Add("FirstName is required.");
            }

            if (string.IsNullOrWhiteSpace(last))
            {
                rowResult.Errors.Add("LastName is required.");
            }

            string explicitUsername = Get(row, "Username");
            string? builtUsername = null;
            if (!string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(last))
            {
                try
                {
                    builtUsername = UserProvisioningService.BuildUsername(first, last, NullIfEmpty(explicitUsername));
                }
                catch (Exception)
                {
                    rowResult.Errors.Add(
                        "Username is invalid. FirstName/LastName (or Username) must contain letters or numbers.");
                }

                if (!string.IsNullOrWhiteSpace(builtUsername))
                {
                    if (usernameInFile.TryGetValue(builtUsername, out int priorUserRow))
                    {
                        rowResult.Errors.Add(
                            $"Duplicate username '{builtUsername}' in file (also on row {priorUserRow}).");
                    }
                    else
                    {
                        usernameInFile[builtUsername] = row.RowNumber;
                        var existingUser = await userRepository
                            .GetByUsernameAsync(builtUsername, cancellationToken)
                            .ConfigureAwait(false);
                        if (existingUser is not null)
                        {
                            rowResult.Errors.Add($"Username '{builtUsername}' already exists.");
                        }
                    }
                }
            }

            DateOnly? dob = null;
            string dobRaw = Get(row, "Dob");
            if (string.IsNullOrWhiteSpace(dobRaw))
            {
                rowResult.Errors.Add("Dob is required.");
            }
            else if (!TryParseDate(dobRaw, out dob) || dob is null)
            {
                rowResult.Errors.Add("Dob must be dd/MM/yyyy or yyyy-MM-dd.");
            }
            else if (dob.Value > DateOnly.FromDateTime(DateTime.Today))
            {
                rowResult.Errors.Add("Dob cannot be in the future.");
            }

            string gender = Get(row, "Gender");
            if (string.IsNullOrWhiteSpace(gender))
            {
                rowResult.Errors.Add("Gender is required.");
            }
            else if (!IsAllowedGender(gender))
            {
                rowResult.Errors.Add("Gender must be Male, Female, or Other.");
            }

            string mobile = Get(row, "Mobile");
            if (string.IsNullOrWhiteSpace(mobile))
            {
                rowResult.Errors.Add("Mobile is required.");
            }
            else if (!MobilePattern.IsMatch(mobile))
            {
                rowResult.Errors.Add("Mobile must be 10 digits.");
            }

            string email = Get(row, "Email");
            if (string.IsNullOrWhiteSpace(email))
            {
                rowResult.Errors.Add("Email is required.");
            }
            else if (!EmailPattern.IsMatch(email))
            {
                rowResult.Errors.Add("Email format is invalid.");
            }
            else if (emailInFile.TryGetValue(email, out int priorEmailRow))
            {
                rowResult.Errors.Add($"Duplicate Email in file (also on row {priorEmailRow}).");
            }
            else
            {
                emailInFile[email] = row.RowNumber;
                var existingEmail = await userRepository
                    .GetByEmailAsync(email, cancellationToken)
                    .ConfigureAwait(false);
                if (existingEmail is not null)
                {
                    rowResult.Errors.Add($"Email '{email}' already exists.");
                }
            }

            DateOnly? joiningDate = null;
            string joiningRaw = Get(row, "JoiningDate");
            if (string.IsNullOrWhiteSpace(joiningRaw))
            {
                rowResult.Errors.Add("JoiningDate is required.");
            }
            else if (!TryParseDate(joiningRaw, out joiningDate) || joiningDate is null)
            {
                rowResult.Errors.Add("JoiningDate must be dd/MM/yyyy or yyyy-MM-dd.");
            }

            string userType = Get(row, "UserType");
            if (string.IsNullOrWhiteSpace(userType))
            {
                rowResult.Errors.Add("UserType is required.");
            }
            else if (!UserTypeCodes.IsStaff(userType))
            {
                rowResult.Errors.Add($"UserType '{userType}' is not a valid staff type. See Lookups.");
            }
            else
            {
                userType = CanonicalStaffUserType(userType)!;
            }

            string portalAccessRaw = Get(row, "PortalAccess");
            bool portalEnabled = string.IsNullOrWhiteSpace(portalAccessRaw) || IsYes(portalAccessRaw);
            if (!string.IsNullOrWhiteSpace(portalAccessRaw) && !IsYes(portalAccessRaw) && !IsNo(portalAccessRaw))
            {
                rowResult.Errors.Add("PortalAccess must be Enabled/Disabled (or Y/N).");
                portalEnabled = true;
            }

            string portalRole = Get(row, "PortalRole");
            if (portalEnabled)
            {
                if (string.IsNullOrWhiteSpace(portalRole))
                {
                    rowResult.Errors.Add("PortalRole is required when PortalAccess is Enabled.");
                }
                else if (!RoleNames.IsDefaultRole(portalRole)
                         || string.Equals(portalRole, RoleNames.SmartOpsAdmin, StringComparison.OrdinalIgnoreCase))
                {
                    rowResult.Errors.Add($"PortalRole '{portalRole}' was not found. See Lookups.");
                }
                else
                {
                    portalRole = CanonicalRoleName(portalRole)!;
                }
            }

            string aadhaar = Get(row, "AadhaarNo");
            if (!string.IsNullOrWhiteSpace(aadhaar) && !Regex.IsMatch(aadhaar, @"^\d{12}$"))
            {
                rowResult.Errors.Add("AadhaarNo must be 12 digits.");
            }

            string pan = Get(row, "PanNo");
            if (!string.IsNullOrWhiteSpace(pan) && !PanPattern.IsMatch(pan))
            {
                rowResult.Errors.Add("PanNo format is invalid (e.g. ABCDE1234F).");
            }

            string altMobile = Get(row, "AlternateMobile");
            if (!string.IsNullOrWhiteSpace(altMobile) && !MobilePattern.IsMatch(altMobile))
            {
                rowResult.Errors.Add("AlternateMobile must be 10 digits.");
            }

            string departmentName = Get(row, "Department");
            Guid? departmentId = null;
            if (!string.IsNullOrWhiteSpace(departmentName))
            {
                if (!departmentByName.TryGetValue(departmentName, out Guid deptId))
                {
                    rowResult.Errors.Add($"Department '{departmentName}' was not found.");
                }
                else
                {
                    departmentId = deptId;
                }
            }

            string managerName = Get(row, "ReportingManager");
            Guid? managerId = null;
            if (!string.IsNullOrWhiteSpace(managerName))
            {
                if (!managerByName.TryGetValue(managerName, out Guid mgrId))
                {
                    rowResult.Errors.Add($"ReportingManager '{managerName}' was not found.");
                }
                else
                {
                    managerId = mgrId;
                }
            }

            int experience = 0;
            string expRaw = Get(row, "ExperienceYears");
            if (!string.IsNullOrWhiteSpace(expRaw))
            {
                if (!int.TryParse(expRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out experience) || experience < 0)
                {
                    rowResult.Errors.Add("ExperienceYears must be a non-negative whole number.");
                }
            }

            if (rowResult.Errors.Count > 0)
            {
                rowResult.Status = "Invalid";
                result.Employees.Add(rowResult);
                continue;
            }

            string? qualificationsRaw = NullIfEmpty(Get(row, "Qualifications"));
            List<string>? qualifications = qualificationsRaw is null
                ? null
                : qualificationsRaw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            var dto = new CreateEmployeeDto
            {
                Personal = new EmployeePersonalInfo
                {
                    FirstName = first,
                    LastName = last,
                    Dob = dob!.Value,
                    Gender = CanonicalGender(gender),
                    BloodGroup = NullIfEmpty(Get(row, "BloodGroup")),
                    AadhaarNumber = NullIfEmpty(aadhaar),
                    PanNumber = NullIfEmpty(pan)?.ToUpperInvariant(),
                    Mobile = mobile,
                    AlternateMobile = NullIfEmpty(altMobile),
                    Email = email,
                    Address = NullIfEmpty(Get(row, "Address"))
                },
                Professional = new EmployeeProfessionalInfo
                {
                    EmployeeCode = code,
                    JoiningDate = joiningDate!.Value,
                    Designation = NullIfEmpty(Get(row, "Designation")),
                    Experience = experience,
                    Qualifications = qualifications,
                    BankDetails = HasAnyBank(row)
                        ? new EmployeeBankDetails
                        {
                            AccountNumber = NullIfEmpty(Get(row, "BankAccountNumber")),
                            IfscCode = NullIfEmpty(Get(row, "BankIfscCode"))?.ToUpperInvariant(),
                            BankName = NullIfEmpty(Get(row, "BankName"))
                        }
                        : null
                },
                Access = new EmployeeAccessInfo
                {
                    UserTypeCode = userType,
                    PortalRoleName = string.IsNullOrWhiteSpace(portalRole)
                        ? RoleNames.FromUserType(userType) ?? RoleNames.Teacher
                        : portalRole,
                    PortalAccess = portalEnabled ? "Enabled" : "Disabled",
                    Username = NullIfEmpty(explicitUsername)
                },
                Organization = new EmployeeOrganizationInfo
                {
                    DepartmentId = departmentId,
                    ReportingManagerId = managerId
                },
                Schedule = new EmployeeScheduleInfo
                {
                    ShiftStartTime = NullIfEmpty(Get(row, "ShiftStartTime")),
                    ShiftEndTime = NullIfEmpty(Get(row, "ShiftEndTime"))
                }
            };

            rowResult.Status = "Valid";
            result.Employees.Add(rowResult);
            validRows.Add(new ValidEmployeeRow(row.RowNumber, dto));
        }

        result.TotalEmployees = result.Employees.Count;
        result.ValidEmployees = result.Employees.Count(e => e.Status == "Valid");
        result.InvalidEmployees = result.Employees.Count(e => e.Status != "Valid");

        return new ParsedImport(result, validRows);
    }

    private static IEnumerable<string> StaffUserTypes() =>
        UserTypeCodes.All.Select(x => x.Name).Where(UserTypeCodes.IsStaff);

    private static string? CanonicalStaffUserType(string raw)
    {
        foreach (string name in StaffUserTypes())
        {
            if (string.Equals(name, raw.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static string? CanonicalRoleName(string raw)
    {
        foreach ((Guid _, string name, string _) in RoleNames.Defaults)
        {
            if (string.Equals(name, raw.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static bool IsAllowedGender(string gender) =>
        string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase)
        || string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase)
        || string.Equals(gender, "Other", StringComparison.OrdinalIgnoreCase);

    private static string CanonicalGender(string gender)
    {
        if (string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase)) return "Male";
        if (string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase)) return "Female";
        return "Other";
    }

    private static bool HasAnyBank(ExcelDataRow row) =>
        !string.IsNullOrWhiteSpace(Get(row, "BankAccountNumber"))
        || !string.IsNullOrWhiteSpace(Get(row, "BankIfscCode"))
        || !string.IsNullOrWhiteSpace(Get(row, "BankName"));

    private sealed record ValidEmployeeRow(int RowNumber, CreateEmployeeDto Dto);

    private sealed record ParsedImport(
        EmployeeImportValidateResultDto Result,
        List<ValidEmployeeRow> ValidRows);
}

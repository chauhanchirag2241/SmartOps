using System.Globalization;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text.RegularExpressions;
using SmartOps.Application.Common.Excel;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.BulkImport;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Student;
using SmartOps.Application.Modules.Student.Import;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Modules.Class;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;
using SmartOps.Domain.Modules.Student;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Infrastructure.Modules.Identity.Services;

namespace SmartOps.Infrastructure.Modules.Student;

public sealed class StudentImportService(
    IExcelHelper excelHelper,
    IStudentRepository studentRepository,
    IClassRepository classRepository,
    IFeeMasterRepository feeMasterRepository,
    IFeeHeadRepository feeHeadRepository,
    IFeeStudentAmountRepository feeStudentAmountRepository,
    IAcademicYearRepository academicYearRepository,
    IUserRepository userRepository,
    IBranchContext branchContext) : IStudentImportService
{
    public const string StudentsSheet = "Students";
    public const string FeeAssignmentsSheet = "FeeAssignments";
    public const string InstructionsSheet = "Instructions";
    public const string LookupsSheet = "Lookups";

    private static readonly Regex AdmissionNoPattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var classGroups = await classRepository
            .GetClassGroupDropdownAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var classesPage = await classRepository
            .GetAllClassesAsync(1, 2000, null, null, null, ClassFilter.Active, null, cancellationToken)
            .ConfigureAwait(false);

        var feeMasters = await feeMasterRepository
            .GetAllAsync(1, 500, null, null, null, "Active", cancellationToken)
            .ConfigureAwait(false);

        var lookupRows = new List<IReadOnlyList<string>>();
        foreach (var g in classGroups)
        {
            lookupRows.Add(["ClassGroup", g.Name, "Use this exact name in Students → ClassGroup"]);
        }

        foreach (var c in classesPage.Items)
        {
            lookupRows.Add(["Section", c.Section, $"Class group: {c.ClassName}"]);
        }

        foreach (var fee in feeMasters.Items.Where(f =>
                     string.Equals(f.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase)))
        {
            lookupRows.Add(["FeeMaster (StudentWise)", fee.FeeName, "Use in FeeAssignments → FeeMasterName"]);
            var heads = await feeHeadRepository
                .GetByFeeMasterAsync(fee.Id, 1, 200, null, null, null, "Active", cancellationToken)
                .ConfigureAwait(false);
            foreach (var h in heads.Items)
            {
                lookupRows.Add(["FeeHead", h.FeeHeadName, $"Under fee master: {fee.FeeName}"]);
            }
        }

        var studentColumns = new List<ExcelColumnSpec>
        {
            new() { Header = "AdmissionNo", Required = true, Example = "ADM-2026-001", Width = 16 },
            new() { Header = "FirstName", Required = true, Example = "Riya", Width = 14 },
            new() { Header = "MiddleName", Required = false, Example = "K", Width = 12 },
            new() { Header = "LastName", Required = true, Example = "Patel", Width = 14 },
            new() { Header = "Dob", Required = false, Example = "15/08/2015", Width = 12 },
            new() { Header = "Gender", Required = false, Example = "Female", Width = 10 },
            new() { Header = "BloodGroup", Required = false, Example = "B+", Width = 11 },
            new() { Header = "Mobile", Required = false, Example = "9876543210", Width = 13 },
            new() { Header = "Email", Required = true, Example = "parent@email.com", Width = 20 },
            new() { Header = "AadhaarNo", Required = false, Example = "123456789012", Width = 14 },
            new() { Header = "Caste", Required = false, Example = "", Width = 10 },
            new() { Header = "Category", Required = false, Example = "General", Width = 11 },
            new() { Header = "Address", Required = false, Example = "Ahmedabad", Width = 18 },
            new() { Header = "Remarks", Required = false, Example = "", Width = 14 },
            new() { Header = "ClassGroup", Required = true, Example = classGroups.FirstOrDefault()?.Name ?? "Class 1", Width = 14 },
            new() { Header = "Section", Required = false, Example = classesPage.Items.FirstOrDefault()?.Section ?? "A", Width = 10 },
            new() { Header = "RollNumber", Required = false, Example = "1", Width = 11 },
            new() { Header = "AdmissionDate", Required = false, Example = "01/04/2026", Width = 13 },
            new() { Header = "FatherName", Required = false, Example = "Amit Patel", Width = 14 },
            new() { Header = "FatherMobile", Required = false, Example = "9876500001", Width = 13 },
            new() { Header = "FatherEmail", Required = false, Example = "", Width = 16 },
            new() { Header = "FatherOccupation", Required = false, Example = "Business", Width = 15 },
            new() { Header = "MotherName", Required = false, Example = "Neha Patel", Width = 14 },
            new() { Header = "MotherMobile", Required = false, Example = "", Width = 13 },
            new() { Header = "MotherEmail", Required = false, Example = "", Width = 16 },
            new() { Header = "MotherOccupation", Required = false, Example = "", Width = 15 },
        };

        var feeColumns = new List<ExcelColumnSpec>
        {
            new() { Header = "AdmissionNo", Required = true, Example = "ADM-2026-001", Width = 16 },
            new() { Header = "FeeMasterName", Required = true, Example = "Transport Fee", Width = 18 },
            new() { Header = "FeeHeadName", Required = false, Example = "Monthly", Width = 14 },
            new() { Header = "Amount", Required = false, Example = "1200", Width = 10 },
            new() { Header = "Exclude", Required = false, Example = "N", Width = 10 },
        };

        var instructionNotes = new List<ExcelNoteLine>
        {
            new() { Kind = "warn", Text = "READ FIRST: Fill the Students sheet, then Validate in SmartOps before Import. Do not rename sheet names or header titles." },
            new() { Kind = "required", Text = "RED headers = Required columns (AdmissionNo, FirstName, LastName, Email, ClassGroup on Students; AdmissionNo + FeeMasterName on FeeAssignments)." },
            new() { Kind = "optional", Text = "GREEN headers = Optional columns. You can leave them blank." },
            new() { Kind = "info", Text = "Academic year is taken from the year selected in SmartOps Settings / header. There is NO AcademicYear column in this file." },
            new() { Kind = "info", Text = "Dates must be dd/MM/yyyy or yyyy-MM-dd (example: 15/08/2015)." },
            new() { Kind = "tip", Text = "ClassGroup and Section names must match the Lookups sheet exactly (copy-paste recommended)." },
            new() { Kind = "tip", Text = "ClassWise fees apply automatically from the student's class group. Use FeeAssignments only for StudentWise fee masters / amount overrides." },
            new() { Kind = "warn", Text = "Delete the grey EXAMPLE row before import (or leave it — import skips rows starting with (example))." },
            new() { Kind = "info", Text = "AdmissionNo: letters, numbers, hyphen (-), underscore (_) only. Must be unique per branch." },
            new() { Kind = "warn", Text = "Email is required. Portal username is auto-built as firstname.lastname and must be unique (also unique within this file)." },
            new() { Kind = "info", Text = "Import runs only when EVERY row is Valid. Fix all Invalid rows, then re-validate before Import." },
            new() { Kind = "info", Text = "After Validate, download the error Excel if any row is Invalid, fix Status/ErrorMessages, and re-upload." },
            new() { Kind = "tip", Text = "Lookups sheet is a helper list only — do not type student data there." },
        };

        var lookupNotes = new List<ExcelNoteLine>
        {
            new() { Kind = "info", Text = "What is this sheet? Ready-made list of ClassGroup, Section, and StudentWise FeeMaster / FeeHead names from your school." },
            new() { Kind = "tip", Text = "How to use: copy a Name from column B into Students or FeeAssignments. Do not invent names that are not listed." },
            new() { Kind = "warn", Text = "Do not edit or delete this sheet for import — it is reference only. Student data goes in the Students sheet." },
            new() { Kind = "optional", Text = "Type = ClassGroup | Section | FeeMaster (StudentWise) | FeeHead. Extra = parent class group or fee master hint." },
        };

        return excelHelper.CreateImportTemplate(
        [
            new ExcelTemplateSheet
            {
                Name = InstructionsSheet,
                TabColorHex = "1565C0",
                BannerTitle = "SmartOps — Student Bulk Import Guide",
                BannerSubtitle = "Follow these steps, then fill Students (and optional FeeAssignments).",
                Notes = instructionNotes,
                AddExampleRow = false,
                FreezeHeader = false
            },
            new ExcelTemplateSheet
            {
                Name = StudentsSheet,
                TabColorHex = "C62828",
                BannerTitle = "Students — enter one student per row",
                BannerSubtitle = "Red = required  |  Green = optional  |  Academic year = SmartOps header selection",
                Columns = studentColumns,
                AddExampleRow = true,
                FreezeHeader = true
            },
            new ExcelTemplateSheet
            {
                Name = FeeAssignmentsSheet,
                TabColorHex = "2E7D32",
                BannerTitle = "FeeAssignments — optional (StudentWise fees only)",
                BannerSubtitle = "Leave this sheet empty if you only use ClassWise fees. ClassWise fees apply automatically.",
                Columns = feeColumns,
                AddExampleRow = true,
                FreezeHeader = true
            },
            new ExcelTemplateSheet
            {
                Name = LookupsSheet,
                TabColorHex = "6A1B9A",
                BannerTitle = "Lookups — copy valid names from here",
                BannerSubtitle = "Reference list for ClassGroup, Section, and StudentWise fees. Not for typing student rows.",
                Notes = lookupNotes,
                Columns =
                [
                    new ExcelColumnSpec { Header = "Type", Required = false, Width = 22 },
                    new ExcelColumnSpec { Header = "Name", Required = false, Width = 28 },
                    new ExcelColumnSpec { Header = "Extra", Required = false, Width = 40 },
                ],
                DataRows = lookupRows,
                AddExampleRow = false,
                ShowLegend = false,
                FreezeHeader = true
            },
        ]);
    }

    public async Task<StudentImportValidateResultDto> ValidateAsync(
        Stream fileStream,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAndValidateCoreAsync(fileStream, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        if (parsed.Result.FileError is null && (parsed.Result.Students.Count > 0 || parsed.Result.FeeAssignments.Count > 0))
        {
            fileStream.Position = 0;
            var bySheet = new Dictionary<string, IReadOnlyList<(int, string, string)>>(StringComparer.OrdinalIgnoreCase)
            {
                [StudentsSheet] = parsed.Result.Students
                    .Select(s => (s.RowNumber, s.Status, string.Join(", ", s.Errors)))
                    .ToList(),
                [FeeAssignmentsSheet] = parsed.Result.FeeAssignments
                    .Select(s => (s.RowNumber, s.Status, string.Join(", ", s.Errors)))
                    .ToList()
            };
            byte[] errorBytes = excelHelper.AppendStatusColumns(fileStream, bySheet);
            parsed.Result.ErrorFileBase64 = Convert.ToBase64String(errorBytes);
        }

        return parsed.Result;
    }

    public async Task<StudentImportCommitResultDto> CommitAsync(
        Stream fileStream,
        Guid academicYearId,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAndValidateCoreAsync(fileStream, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        var commit = new StudentImportCommitResultDto
        {
            Validation = parsed.Result,
            FileError = parsed.Result.FileError,
            SkippedInvalidStudents = parsed.Result.InvalidStudents,
            SkippedInvalidFeeAssignments = parsed.Result.InvalidFeeAssignments
        };

        if (parsed.Result.FileError is not null)
        {
            return commit;
        }

        if (parsed.Result.InvalidStudents > 0 || parsed.Result.InvalidFeeAssignments > 0)
        {
            commit.FileError =
                "Import cancelled. The entire file must be valid — fix all Invalid rows and validate again.";
            return commit;
        }

        if (parsed.ValidStudentRows.Count == 0 && parsed.ValidFeeRows.Count == 0)
        {
            commit.FileError = "No valid rows to import.";
            return commit;
        }

        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        Guid branchId = branchContext.ActiveBranchId
            ?? throw new InvalidOperationException("Select a branch from the header before importing.");

        var admissionToStudentId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in parsed.ValidStudentRows)
        {
            try
            {
                var dto = row.Dto;
                var entity = dto.ToEntity();
                string username = UserProvisioningService.BuildUsername(dto.FirstName!, dto.LastName!);
                Guid studentId = await studentRepository
                    .CreateStudentAsync(entity, schoolId, cancellationToken)
                    .ConfigureAwait(false);
                admissionToStudentId[dto.AdmissionNo!.Trim()] = studentId;
                commit.CreatedStudents++;
                commit.Created.Add(new StudentImportCreatedDto
                {
                    RowNumber = row.RowNumber,
                    AdmissionNo = dto.AdmissionNo,
                    DisplayName = $"{dto.FirstName} {dto.LastName}".Trim(),
                    Username = username,
                    Status = "Active"
                });
            }
            catch (Exception ex)
            {
                commit.Failures.Add(new StudentImportCommitFailureDto
                {
                    RowNumber = row.RowNumber,
                    AdmissionNo = row.Dto.AdmissionNo,
                    DisplayName = $"{row.Dto.FirstName} {row.Dto.LastName}".Trim(),
                    Message = ex.Message
                });
            }
        }

        foreach (var feeRow in parsed.ValidFeeRows)
        {
            try
            {
                string admission = feeRow.AdmissionNo;
                if (!admissionToStudentId.TryGetValue(admission, out Guid studentId))
                {
                    Guid? existing = await studentRepository
                        .GetStudentIdByAdmissionNoAsync(admission, branchId, cancellationToken)
                        .ConfigureAwait(false);
                    if (existing is null)
                    {
                        commit.Failures.Add(new StudentImportCommitFailureDto
                        {
                            RowNumber = feeRow.RowNumber,
                            AdmissionNo = admission,
                            Message = "Student was not created and does not exist for this admission number."
                        });
                        continue;
                    }

                    studentId = existing.Value;
                }

                var headsPage = await feeHeadRepository
                    .GetByFeeMasterAsync(feeRow.FeeMasterId, 1, 500, null, null, null, "Active", cancellationToken)
                    .ConfigureAwait(false);
                var heads = headsPage.Items.ToList();
                if (heads.Count == 0)
                {
                    commit.Failures.Add(new StudentImportCommitFailureDto
                    {
                        RowNumber = feeRow.RowNumber,
                        AdmissionNo = admission,
                        Message = "Fee master has no active fee heads."
                    });
                    continue;
                }

                var rows = new List<FeeStudentAmountEntity>();
                if (!string.IsNullOrWhiteSpace(feeRow.FeeHeadName))
                {
                    var head = heads.FirstOrDefault(h =>
                        string.Equals(h.FeeHeadName, feeRow.FeeHeadName, StringComparison.OrdinalIgnoreCase));
                    if (head is null)
                    {
                        commit.Failures.Add(new StudentImportCommitFailureDto
                        {
                            RowNumber = feeRow.RowNumber,
                            AdmissionNo = admission,
                            Message = $"Fee head '{feeRow.FeeHeadName}' not found."
                        });
                        continue;
                    }

                    foreach (var h in heads)
                    {
                        bool isTarget = h.Id == head.Id;
                        rows.Add(new FeeStudentAmountEntity
                        {
                            FeeHeadId = h.Id,
                            Amount = isTarget ? (feeRow.Amount ?? h.Amount) : h.Amount,
                            IsExcluded = isTarget && feeRow.Exclude
                        });
                    }
                }
                else
                {
                    foreach (var h in heads)
                    {
                        rows.Add(new FeeStudentAmountEntity
                        {
                            FeeHeadId = h.Id,
                            Amount = feeRow.Amount ?? h.Amount,
                            IsExcluded = false
                        });
                    }
                }

                await feeStudentAmountRepository
                    .UpsertOverridesAsync(feeRow.FeeMasterId, studentId, branchId, rows, cancellationToken)
                    .ConfigureAwait(false);
                commit.FeeAssignmentsApplied++;
            }
            catch (Exception ex)
            {
                commit.Failures.Add(new StudentImportCommitFailureDto
                {
                    RowNumber = feeRow.RowNumber,
                    AdmissionNo = feeRow.AdmissionNo,
                    Message = ex.Message
                });
            }
        }

        return commit;
    }

    private async Task<ParsedImport> ParseAndValidateCoreAsync(
        Stream fileStream,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var result = new StudentImportValidateResultDto { AcademicYearId = academicYearId };
        if (academicYearId == Guid.Empty)
        {
            result.FileError = "Academic year is required. Select a year in Settings / header.";
            return new ParsedImport(result, [], []);
        }

        var year = await academicYearRepository
            .GetAcademicYearByIdAsync(academicYearId, cancellationToken)
            .ConfigureAwait(false);
        if (year is null)
        {
            result.FileError = "Selected academic year was not found.";
            return new ParsedImport(result, [], []);
        }

        result.AcademicYearName = year.Title;

        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        if (branchContext.ActiveBranchId is null)
        {
            result.FileError = "Select a branch from the header before importing.";
            return new ParsedImport(result, [], []);
        }

        Guid branchId = branchContext.ActiveBranchId.Value;

        List<ExcelDataRow> studentRows;
        List<ExcelDataRow> feeRows;
        try
        {
            using var copy = new MemoryStream();
            fileStream.Position = 0;
            await fileStream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
            copy.Position = 0;
            studentRows = excelHelper.ReadSheet(copy, StudentsSheet).ToList();
            copy.Position = 0;
            feeRows = excelHelper.SheetExists(copy, FeeAssignmentsSheet)
                ? excelHelper.ReadSheet(copy, FeeAssignmentsSheet, requireSheet: false).ToList()
                : [];
        }
        catch (Exception ex)
        {
            result.FileError = ex.Message;
            return new ParsedImport(result, [], []);
        }

        if (studentRows.Count == 0 && feeRows.Count == 0)
        {
            result.FileError = "No data rows found. Fill the Students sheet (and optionally FeeAssignments).";
            return new ParsedImport(result, [], []);
        }

        var classGroups = await classRepository
            .GetClassGroupDropdownAsync(academicYearId, cancellationToken)
            .ConfigureAwait(false);
        var classGroupByName = classGroups
            .GroupBy(g => g.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var classesPage = await classRepository
            .GetAllClassesAsync(1, 5000, null, null, null, ClassFilter.Active, null, cancellationToken)
            .ConfigureAwait(false);
        var sections = classesPage.Items.ToList();

        var feeMastersPage = await feeMasterRepository
            .GetAllAsync(1, 1000, null, null, null, "Active", cancellationToken)
            .ConfigureAwait(false);
        var feeMasters = feeMastersPage.Items.ToList();
        var feeMasterByName = feeMasters
            .GroupBy(f => f.FeeName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var headsByMaster = new Dictionary<Guid, List<FeeHeadListModel>>();
        foreach (var fm in feeMasters.Where(f =>
                     string.Equals(f.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase)))
        {
            var heads = await feeHeadRepository
                .GetByFeeMasterAsync(fm.Id, 1, 500, null, null, null, "Active", cancellationToken)
                .ConfigureAwait(false);
            headsByMaster[fm.Id] = heads.Items.ToList();
        }

        var admissionInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var usernameInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var emailInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var validStudents = new List<ValidStudentRow>();
        var validFees = new List<ValidFeeRow>();
        var validAdmissionNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in studentRows)
        {
            var rowResult = new ImportRowResultDto { RowNumber = row.RowNumber };
            string admission = Get(row, "AdmissionNo");
            string first = Get(row, "FirstName");
            string last = Get(row, "LastName");
            rowResult.AdmissionNo = admission;
            rowResult.DisplayName = $"{first} {last}".Trim();

            if (string.IsNullOrWhiteSpace(admission))
            {
                rowResult.Errors.Add("AdmissionNo is required.");
            }
            else if (!AdmissionNoPattern.IsMatch(admission))
            {
                rowResult.Errors.Add("AdmissionNo can contain only letters, numbers, hyphen (-), and underscore (_).");
            }
            else if (admissionInFile.TryGetValue(admission, out int priorRow))
            {
                rowResult.Errors.Add($"Duplicate AdmissionNo in file (also on row {priorRow}).");
            }
            else
            {
                admissionInFile[admission] = row.RowNumber;
                bool exists = await studentRepository
                    .AdmissionNoExistsAsync(admission, branchId, null, cancellationToken)
                    .ConfigureAwait(false);
                if (exists)
                {
                    rowResult.Errors.Add("AdmissionNo already exists for this branch.");
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

            if (!string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(last))
            {
                string? builtUsername = null;
                try
                {
                    builtUsername = UserProvisioningService.BuildUsername(first, last);
                }
                catch (Exception)
                {
                    rowResult.Errors.Add(
                        "Username is invalid. FirstName/LastName must contain letters or numbers to build a portal username.");
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

            string classGroupName = Get(row, "ClassGroup");
            Guid classGroupId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(classGroupName))
            {
                rowResult.Errors.Add("ClassGroup is required.");
            }
            else if (!classGroupByName.TryGetValue(classGroupName, out classGroupId))
            {
                rowResult.Errors.Add($"ClassGroup '{classGroupName}' was not found.");
            }

            string sectionName = Get(row, "Section");
            Guid? classId = null;
            if (!string.IsNullOrWhiteSpace(sectionName) && classGroupId != Guid.Empty)
            {
                var match = sections.FirstOrDefault(c =>
                    c.ClassGroupId == classGroupId
                    && string.Equals(c.Section, sectionName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    rowResult.Errors.Add($"Section '{sectionName}' was not found under ClassGroup '{classGroupName}'.");
                }
                else
                {
                    classId = match.Id;
                }
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

            string fatherEmail = Get(row, "FatherEmail");
            if (!string.IsNullOrWhiteSpace(fatherEmail) && !EmailPattern.IsMatch(fatherEmail))
            {
                rowResult.Errors.Add("FatherEmail format is invalid.");
            }

            string motherEmail = Get(row, "MotherEmail");
            if (!string.IsNullOrWhiteSpace(motherEmail) && !EmailPattern.IsMatch(motherEmail))
            {
                rowResult.Errors.Add("MotherEmail format is invalid.");
            }

            DateOnly? dob = null;
            string dobRaw = Get(row, "Dob");
            if (!string.IsNullOrWhiteSpace(dobRaw) && !TryParseDate(dobRaw, out dob))
            {
                rowResult.Errors.Add("Dob must be dd/MM/yyyy or yyyy-MM-dd.");
            }

            DateOnly? admissionDate = null;
            string admissionDateRaw = Get(row, "AdmissionDate");
            if (!string.IsNullOrWhiteSpace(admissionDateRaw) && !TryParseDate(admissionDateRaw, out admissionDate))
            {
                rowResult.Errors.Add("AdmissionDate must be dd/MM/yyyy or yyyy-MM-dd.");
            }

            string aadhaar = Get(row, "AadhaarNo");
            if (!string.IsNullOrWhiteSpace(aadhaar) && !Regex.IsMatch(aadhaar, @"^\d{12}$"))
            {
                rowResult.Errors.Add("AadhaarNo must be 12 digits.");
            }

            if (rowResult.Errors.Count > 0)
            {
                rowResult.Status = "Invalid";
                result.Students.Add(rowResult);
                continue;
            }

            var dto = new CreateStudentDto
            {
                AdmissionNo = admission,
                FirstName = first,
                MiddleName = NullIfEmpty(Get(row, "MiddleName")),
                LastName = last,
                Dob = dob,
                Gender = NullIfEmpty(Get(row, "Gender")),
                BloodGroup = NullIfEmpty(Get(row, "BloodGroup")),
                Mobile = NullIfEmpty(Get(row, "Mobile")),
                Email = NullIfEmpty(email),
                AadhaarNo = NullIfEmpty(aadhaar),
                Caste = NullIfEmpty(Get(row, "Caste")),
                Category = NullIfEmpty(Get(row, "Category")),
                Address = NullIfEmpty(Get(row, "Address")),
                Remarks = NullIfEmpty(Get(row, "Remarks")),
                PortalAccess = false,
                Academics =
                [
                    new CreateStudentAcademicDto
                    {
                        AcademicYearId = academicYearId,
                        ClassGroupId = classGroupId,
                        ClassId = classId,
                        RollNumber = classId is null ? null : NullIfEmpty(Get(row, "RollNumber")),
                        AdmissionDate = admissionDate
                    }
                ]
            };

            string fatherName = Get(row, "FatherName");
            if (!string.IsNullOrWhiteSpace(fatherName))
            {
                dto.Parents.Add(new CreateStudentParentDto
                {
                    RelationType = "Father",
                    Name = fatherName,
                    Mobile = NullIfEmpty(Get(row, "FatherMobile")),
                    Email = NullIfEmpty(fatherEmail),
                    Occupation = NullIfEmpty(Get(row, "FatherOccupation"))
                });
            }

            string motherName = Get(row, "MotherName");
            if (!string.IsNullOrWhiteSpace(motherName))
            {
                dto.Parents.Add(new CreateStudentParentDto
                {
                    RelationType = "Mother",
                    Name = motherName,
                    Mobile = NullIfEmpty(Get(row, "MotherMobile")),
                    Email = NullIfEmpty(motherEmail),
                    Occupation = NullIfEmpty(Get(row, "MotherOccupation"))
                });
            }

            rowResult.Status = "Valid";
            result.Students.Add(rowResult);
            validStudents.Add(new ValidStudentRow(row.RowNumber, dto));
            validAdmissionNos.Add(admission);
        }

        foreach (var row in feeRows)
        {
            var rowResult = new ImportRowResultDto { RowNumber = row.RowNumber };
            string admission = Get(row, "AdmissionNo");
            string feeMasterName = Get(row, "FeeMasterName");
            string feeHeadName = Get(row, "FeeHeadName");
            rowResult.AdmissionNo = admission;
            rowResult.DisplayName = feeMasterName;

            if (string.IsNullOrWhiteSpace(admission))
            {
                rowResult.Errors.Add("AdmissionNo is required.");
            }

            if (string.IsNullOrWhiteSpace(feeMasterName))
            {
                rowResult.Errors.Add("FeeMasterName is required.");
            }

            FeeMasterListModel? feeMaster = null;
            if (!string.IsNullOrWhiteSpace(feeMasterName))
            {
                if (!feeMasterByName.TryGetValue(feeMasterName, out feeMaster))
                {
                    rowResult.Errors.Add($"FeeMaster '{feeMasterName}' was not found.");
                }
                else if (!string.Equals(feeMaster.ApplicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase))
                {
                    rowResult.Errors.Add($"FeeMaster '{feeMasterName}' is not StudentWise (ClassWise fees apply automatically).");
                }
            }

            decimal? amount = null;
            string amountRaw = Get(row, "Amount");
            if (!string.IsNullOrWhiteSpace(amountRaw))
            {
                if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedAmount)
                    && !decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedAmount))
                {
                    rowResult.Errors.Add("Amount must be a number.");
                }
                else
                {
                    amount = parsedAmount;
                }
            }

            bool exclude = IsYes(Get(row, "Exclude"));

            if (feeMaster is not null
                && !string.IsNullOrWhiteSpace(feeHeadName)
                && headsByMaster.TryGetValue(feeMaster.Id, out var heads)
                && !heads.Any(h => string.Equals(h.FeeHeadName, feeHeadName, StringComparison.OrdinalIgnoreCase)))
            {
                rowResult.Errors.Add($"FeeHead '{feeHeadName}' was not found under '{feeMasterName}'.");
            }

            if (!string.IsNullOrWhiteSpace(admission))
            {
                bool inValidFile = validAdmissionNos.Contains(admission);
                bool inDb = false;
                if (!inValidFile)
                {
                    inDb = await studentRepository
                        .AdmissionNoExistsAsync(admission, branchId, null, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (!inValidFile && !inDb)
                {
                    rowResult.Errors.Add("AdmissionNo must match a valid Students row or an existing student.");
                }
            }

            if (rowResult.Errors.Count > 0)
            {
                rowResult.Status = "Invalid";
                result.FeeAssignments.Add(rowResult);
                continue;
            }

            rowResult.Status = "Valid";
            result.FeeAssignments.Add(rowResult);
            validFees.Add(new ValidFeeRow(
                row.RowNumber,
                admission,
                feeMaster!.Id,
                NullIfEmpty(feeHeadName),
                amount,
                exclude));
        }

        result.TotalStudents = result.Students.Count;
        result.ValidStudents = result.Students.Count(s => s.Status == "Valid");
        result.InvalidStudents = result.Students.Count(s => s.Status != "Valid");
        result.TotalFeeAssignments = result.FeeAssignments.Count;
        result.ValidFeeAssignments = result.FeeAssignments.Count(s => s.Status == "Valid");
        result.InvalidFeeAssignments = result.FeeAssignments.Count(s => s.Status != "Valid");

        return new ParsedImport(result, validStudents, validFees);
    }

    private static string Get(ExcelDataRow row, string key) =>
        row.Values.TryGetValue(key, out string? v) ? (v ?? string.Empty).Trim() : string.Empty;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsYes(string value) =>
        string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "True", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseDate(string raw, out DateOnly? date)
    {
        date = null;
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"];
        if (DateOnly.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d))
        {
            date = d;
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime dt)
            || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }

    private sealed record ValidStudentRow(int RowNumber, CreateStudentDto Dto);

    private sealed record ValidFeeRow(
        int RowNumber,
        string AdmissionNo,
        Guid FeeMasterId,
        string? FeeHeadName,
        decimal? Amount,
        bool Exclude);

    private sealed record ParsedImport(
        StudentImportValidateResultDto Result,
        List<ValidStudentRow> ValidStudentRows,
        List<ValidFeeRow> ValidFeeRows);
}

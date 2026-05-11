using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Persistence.Repositories;

public class StudentRepository : BaseRepository, IStudentRepository
{
    public StudentRepository(DapperContext context, ICurrentUserService currentUser) : base(context, currentUser)
    {
    }

    public async Task<Guid> CreateStudentAsync(StudentEntity student)
    {
        var utcNow = DateTime.UtcNow;
        if (student.Id == Guid.Empty) student.Id = Guid.NewGuid();
        EnsureInsertAudit(student, utcNow);

        var connection = await Context.GetGlobalConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Insert Student
            string studentSql = $@"
                INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudents} 
                (id, admissionno, firstname, middlename, lastname, dob, gender, bloodgroup, mobile, email, aadhaarno, address, photourl, status, remarks, isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES 
                (@Id, @AdmissionNo, @FirstName, @MiddleName, @LastName, @Dob, @Gender, @BloodGroup, @Mobile, @Email, @AadhaarNo, @Address, @PhotoUrl, @Status, @Remarks, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
                RETURNING id;";

            var studentId = await connection.ExecuteScalarAsync<Guid>(studentSql, student, transaction);
            student.Id = studentId;

            // 2. Insert Parents
            if (student.Parents != null && student.Parents.Any())
            {
                string parentSql = $@"
                    INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentParents}
                    (id, studentid, relationtype, name, mobile, occupation, isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES 
                    (@Id, @StudentId, @RelationType, @Name, @Mobile, @Occupation, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);";
                
                foreach (var parent in student.Parents)
                {
                    parent.Id = Guid.NewGuid();
                    parent.StudentId = studentId;
                    EnsureInsertAudit(parent, utcNow);
                    await connection.ExecuteAsync(parentSql, parent, transaction);
                }
            }

            // 3. Insert Academics
            if (student.Academics != null && student.Academics.Any())
            {
                string academicSql = $@"
                    INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentAcademics}
                    (id, studentid, admissiondate, academicyear, class, section, rollnumber, isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES 
                    (@Id, @StudentId, @AdmissionDate, @AcademicYear, @Class, @Section, @RollNumber, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);";
                
                foreach (var academic in student.Academics)
                {
                    academic.Id = Guid.NewGuid();
                    academic.StudentId = studentId;
                    EnsureInsertAudit(academic, utcNow);
                    await connection.ExecuteAsync(academicSql, academic, transaction);
                }
            }

            // 4. Insert Previous Schools
            if (student.PreviousSchools != null && student.PreviousSchools.Any())
            {
                string prevSchoolSql = $@"
                    INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentPreviousSchools}
                    (id, studentid, schoolname, lastclasspassed, percentageorcgpa, tcnumber, isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES 
                    (@Id, @StudentId, @SchoolName, @LastClassPassed, @PercentageOrCgpa, @TcNumber, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);";

                foreach (var prevSchool in student.PreviousSchools)
                {
                    prevSchool.Id = Guid.NewGuid();
                    prevSchool.StudentId = studentId;
                    EnsureInsertAudit(prevSchool, utcNow);
                    await connection.ExecuteAsync(prevSchoolSql, prevSchool, transaction);
                }
            }

            // 5. Insert Fee Configs
            if (student.FeeConfigs != null && student.FeeConfigs.Any())
            {
                string feeConfigSql = $@"
                    INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentFeeConfigs}
                    (id, studentid, discounttype, discountvalue, ispercentage, discountremarks, paymentmode, firstduedate, isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES 
                    (@Id, @StudentId, @DiscountType, @DiscountValue, @IsPercentage, @DiscountRemarks, @PaymentMode, @FirstDueDate, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);";

                foreach (var feeConfig in student.FeeConfigs)
                {
                    feeConfig.Id = Guid.NewGuid();
                    feeConfig.StudentId = studentId;
                    EnsureInsertAudit(feeConfig, utcNow);
                    await connection.ExecuteAsync(feeConfigSql, feeConfig, transaction);
                }
            }

            transaction.Commit();
            return studentId;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public Task<StudentEntity?> GetStudentByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }
}

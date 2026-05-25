using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(124, "School template — semester-wise fees, academic year semesters")]
public sealed class S124_FeeSemesterRestructure : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string SemesterYearUnique = "uq_academicyearsemesters_year_index";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableAcademicYearSemesters).Exists())
        {
            Create.Table(DatabaseConfig.TableAcademicYearSemesters).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("semesterindex").AsInt32().NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("startdate").AsDate().NotNullable()
                .WithColumn("enddate").AsDate().NotNullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableAcademicYearSemesters).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableAcademicYearSemesters).Constraint(SemesterYearUnique).Exists())
        {
            Create.UniqueConstraint(SemesterYearUnique)
                .OnTable(DatabaseConfig.TableAcademicYearSemesters).WithSchema(S)
                .Columns("academicyearid", "semesterindex");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Column("studentwisedifferentamount").Exists())
        {
            Alter.Table(DatabaseConfig.TableFeeTypes).InSchema(S)
                .AddColumn("studentwisedifferentamount").AsBoolean().NotNullable().WithDefaultValue(false);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Column("semester1amount").Exists())
        {
            Alter.Table(DatabaseConfig.TableClassFeeAmounts).InSchema(S)
                .AddColumn("semester1amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
                .AddColumn("semester2amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0);
        }

        // Map legacy frequency values to SemesterWise (0) / OneTime (1).
        Execute.Sql($"""
            UPDATE {S}.{DatabaseConfig.TableFeeTypes}
            SET frequency = CASE WHEN frequency = 4 THEN 1 ELSE 0 END
            WHERE frequency NOT IN (0, 1);
            """);

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Column("semester1amount").Exists())
        {
            Execute.Sql($"""
                UPDATE {S}.{DatabaseConfig.TableClassFeeAmounts} cfa
                SET semester1amount = CASE WHEN ft.frequency = 0 THEN cfa.amount / 2 ELSE 0 END,
                    semester2amount = CASE WHEN ft.frequency = 0 THEN cfa.amount - (cfa.amount / 2) ELSE 0 END
                FROM {S}.{DatabaseConfig.TableFeeTypes} ft
                WHERE ft.id = cfa.feetypeid
                  AND cfa.amount > 0
                  AND ft.frequency = 0
                  AND cfa.semester1amount = 0
                  AND cfa.semester2amount = 0;
                """);
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Column("semester1amount").Exists())
        {
            Delete.Column("semester2amount").FromTable(DatabaseConfig.TableClassFeeAmounts).InSchema(S);
            Delete.Column("semester1amount").FromTable(DatabaseConfig.TableClassFeeAmounts).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Column("studentwisedifferentamount").Exists())
        {
            Delete.Column("studentwisedifferentamount").FromTable(DatabaseConfig.TableFeeTypes).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableAcademicYearSemesters).Exists())
        {
            Delete.Table(DatabaseConfig.TableAcademicYearSemesters).InSchema(S);
        }
    }
}

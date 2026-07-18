using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(124, "School template — class-wise academic periods and period-wise fees")]
public sealed class S124_FeeSemesterRestructure : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string ClassPeriodUnique = "uq_classacademicperiods_class_index";
    private const string FeePeriodUnique = "uq_classfeeperiodamounts_amount_period";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassAcademicPeriods).Exists())
        {
            Create.Table(DatabaseConfig.TableClassAcademicPeriods).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                // 1=Semester, 2=Term, 3=Quarter, 4=Custom
                .WithColumn("periodtype").AsInt16().NotNullable()
                .WithColumn("periodindex").AsInt32().NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("startdate").AsDate().NotNullable()
                .WithColumn("enddate").AsDate().NotNullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableClassAcademicPeriods}
    ADD CONSTRAINT fk_classacademicperiods_class FOREIGN KEY (classid)
    REFERENCES {S}.{DatabaseConfig.TableClasses}(id),
    ADD CONSTRAINT fk_classacademicperiods_year FOREIGN KEY (academicyearid)
    REFERENCES {S}.{DatabaseConfig.TableAcademicYears}(id),
    ADD CONSTRAINT ck_classacademicperiods_index CHECK (periodindex > 0),
    ADD CONSTRAINT ck_classacademicperiods_dates CHECK (enddate >= startdate),
    ADD CONSTRAINT ck_classacademicperiods_type CHECK (periodtype BETWEEN 1 AND 4);

CREATE UNIQUE INDEX {ClassPeriodUnique}
    ON {S}.{DatabaseConfig.TableClassAcademicPeriods} (classid, periodindex)
    WHERE isactive = true;

CREATE UNIQUE INDEX uq_classacademicperiods_class_name
    ON {S}.{DatabaseConfig.TableClassAcademicPeriods} (classid, LOWER(name))
    WHERE isactive = true;
""");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Column("studentwisedifferentamount").Exists())
        {
            Alter.Table(DatabaseConfig.TableFeeTypes).InSchema(S)
                .AddColumn("studentwisedifferentamount").AsBoolean().NotNullable().WithDefaultValue(false);
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeePeriodAmounts).Exists())
        {
            Create.Table(DatabaseConfig.TableClassFeePeriodAmounts).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classfeeamountid").AsGuid().NotNullable()
                .WithColumn("periodindex").AsInt32().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableClassFeePeriodAmounts}
    ADD CONSTRAINT fk_classfeeperiodamounts_amount FOREIGN KEY (classfeeamountid)
    REFERENCES {S}.{DatabaseConfig.TableClassFeeAmounts}(id),
    ADD CONSTRAINT ck_classfeeperiodamounts_index CHECK (periodindex > 0),
    ADD CONSTRAINT ck_classfeeperiodamounts_amount CHECK (amount >= 0);

CREATE UNIQUE INDEX {FeePeriodUnique}
    ON {S}.{DatabaseConfig.TableClassFeePeriodAmounts} (classfeeamountid, periodindex)
    WHERE isactive = true;
""");
        }

        // Map legacy frequency values to PeriodWise (0) / OneTime (1).
        Execute.Sql($"""
            UPDATE {S}.{DatabaseConfig.TableFeeTypes}
            SET frequency = CASE WHEN frequency = 4 THEN 1 ELSE 0 END
            WHERE frequency NOT IN (0, 1);
            """);
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeePeriodAmounts).Exists())
        {
            Delete.Table(DatabaseConfig.TableClassFeePeriodAmounts).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Column("studentwisedifferentamount").Exists())
        {
            Delete.Column("studentwisedifferentamount").FromTable(DatabaseConfig.TableFeeTypes).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassAcademicPeriods).Exists())
        {
            Delete.Table(DatabaseConfig.TableClassAcademicPeriods).InSchema(S);
        }
    }
}

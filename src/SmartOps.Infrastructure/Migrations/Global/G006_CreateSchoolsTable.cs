using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(6, "Global — schools registry")]
public sealed class G006_CreateSchoolsTable : Migration
{
    public override void Up()
    {
        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSchools).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("subdomain").AsString(100).NotNullable().Unique()
            .WithColumn("schoolcode").AsString(50).Nullable()
            .WithColumn("registrationnumber").AsString(100).Nullable()
            .WithColumn("affiliatedboard").AsString(50).Nullable()
            .WithColumn("schooltype").AsString(50).Nullable()
            .WithColumn("establishedyear").AsInt32().Nullable()
            .WithColumn("aboutschool").AsString(int.MaxValue).Nullable()
            .WithColumn("streetaddress").AsString(500).Nullable()
            .WithColumn("city").AsString(100).Nullable()
            .WithColumn("state").AsString(100).Nullable()
            .WithColumn("pincode").AsString(10).Nullable()
            .WithColumn("country").AsString(100).Nullable().WithDefaultValue("India")
            .WithColumn("timezone").AsString(50).Nullable()
            .WithColumn("googlemapslink").AsString(500).Nullable()
            .WithColumn("latitude").AsDecimal(10, 6).Nullable()
            .WithColumn("longitude").AsDecimal(10, 6).Nullable()
            .WithColumn("primaryphone").AsString(20).Nullable()
            .WithColumn("alternatephone").AsString(20).Nullable()
            .WithColumn("fax").AsString(20).Nullable()
            .WithColumn("primaryemail").AsString(256).Nullable()
            .WithColumn("principalemail").AsString(256).Nullable()
            .WithColumn("website").AsString(500).Nullable()
            .WithColumn("logourl").AsString(500).Nullable()
            .WithColumn("faviconurl").AsString(500).Nullable()
            .WithColumn("tagline").AsString(200).Nullable()
            .WithColumn("shortname").AsString(20).Nullable()
            .WithColumn("primarycolor").AsString(20).Nullable().WithDefaultValue("#639922")
            .WithColumn("secondarycolor").AsString(20).Nullable()
            .WithColumn("accentcolor").AsString(20).Nullable()
            .WithColumn("textonprimary").AsString(20).Nullable()
            .WithColumn("customdomain").AsString(200).Nullable()
            .WithColumn("sslcertificate").AsString(50).Nullable()
            .WithColumn("academicyearformat").AsString(100).Nullable()
            .WithColumn("currentacademicyear").AsString(20).Nullable()
            .WithColumn("gradingsystem").AsString(50).Nullable()
            .WithColumn("passingpercentage").AsInt32().Nullable()
            .WithColumn("workingdaysperweek").AsString(50).Nullable()
            .WithColumn("schooltiming").AsString(100).Nullable()
            .WithColumn("classesfrom").AsString(50).Nullable()
            .WithColumn("classesto").AsString(50).Nullable()
            .WithColumn("sectionsperclass").AsInt32().Nullable()
            .WithColumn("sectionnaming").AsString(50).Nullable()
            .WithColumn("maxstudentspersection").AsInt32().Nullable()
            .WithColumn("admissionnumberformat").AsString(100).Nullable()
            .WithColumn("attendancetype").AsString(50).Nullable()
            .WithColumn("minimumattendancepercent").AsInt32().Nullable()
            .WithColumn("latemarkafterminutes").AsInt32().Nullable()
            .WithColumn("autonotifyparentsonabsence").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("allowbackdatedattendance").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("currency").AsString(10).Nullable().WithDefaultValue("INR")
            .WithColumn("paymentcycle").AsString(50).Nullable()
            .WithColumn("feedueday").AsInt32().Nullable()
            .WithColumn("latefeetype").AsString(50).Nullable()
            .WithColumn("latefeevalue").AsDecimal(12, 2).Nullable()
            .WithColumn("graceperioddays").AsInt32().Nullable()
            .WithColumn("feeheadsjson").AsCustom("jsonb").Nullable()
            .WithColumn("discounttypesjson").AsCustom("jsonb").Nullable()
            .WithColumn("paymentmethodsjson").AsCustom("jsonb").Nullable()
            .WithColumn("portalsettingsjson").AsCustom("jsonb").Nullable()
            .WithColumn("schemaname").AsString(100).Nullable()
            .WithColumn("storageplan").AsString(50).Nullable()
            .WithColumn("dataregion").AsString(50).Nullable()
            .WithColumn("sessiontimeoutminutes").AsInt32().Nullable()
            .WithColumn("passwordpolicy").AsString(50).Nullable()
            .WithColumn("loginattemptsbeforelock").AsInt32().Nullable()
            .WithColumn("twofactorenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ipwhitelistenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("branchdataisolation").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("sharedfeestructure").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("centraladminviewallbranches").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithAuditColumns();
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global);
}

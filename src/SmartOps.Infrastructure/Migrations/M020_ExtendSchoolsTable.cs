using FluentMigrator;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(020)]
public sealed class M020_ExtendSchoolsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSchools).Exists())
        {
            return;
        }

        Alter.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global)
            .AddColumn("schoolcode").AsString(50).Nullable()
            .AddColumn("registrationnumber").AsString(100).Nullable()
            .AddColumn("affiliatedboard").AsString(50).Nullable()
            .AddColumn("schooltype").AsString(50).Nullable()
            .AddColumn("establishedyear").AsInt32().Nullable()
            .AddColumn("aboutschool").AsString(int.MaxValue).Nullable()
            .AddColumn("streetaddress").AsString(500).Nullable()
            .AddColumn("city").AsString(100).Nullable()
            .AddColumn("state").AsString(100).Nullable()
            .AddColumn("pincode").AsString(10).Nullable()
            .AddColumn("country").AsString(100).Nullable().WithDefaultValue("India")
            .AddColumn("timezone").AsString(50).Nullable()
            .AddColumn("googlemapslink").AsString(500).Nullable()
            .AddColumn("latitude").AsDecimal(10, 6).Nullable()
            .AddColumn("longitude").AsDecimal(10, 6).Nullable()
            .AddColumn("primaryphone").AsString(20).Nullable()
            .AddColumn("alternatephone").AsString(20).Nullable()
            .AddColumn("fax").AsString(20).Nullable()
            .AddColumn("primaryemail").AsString(256).Nullable()
            .AddColumn("principalemail").AsString(256).Nullable()
            .AddColumn("website").AsString(500).Nullable()
            .AddColumn("logourl").AsString(500).Nullable()
            .AddColumn("faviconurl").AsString(500).Nullable()
            .AddColumn("tagline").AsString(200).Nullable()
            .AddColumn("shortname").AsString(20).Nullable()
            .AddColumn("primarycolor").AsString(20).Nullable().WithDefaultValue("#639922")
            .AddColumn("secondarycolor").AsString(20).Nullable()
            .AddColumn("accentcolor").AsString(20).Nullable()
            .AddColumn("textonprimary").AsString(20).Nullable()
            .AddColumn("customdomain").AsString(200).Nullable()
            .AddColumn("sslcertificate").AsString(50).Nullable()
            .AddColumn("academicyearformat").AsString(100).Nullable()
            .AddColumn("currentacademicyear").AsString(20).Nullable()
            .AddColumn("gradingsystem").AsString(50).Nullable()
            .AddColumn("passingpercentage").AsInt32().Nullable()
            .AddColumn("workingdaysperweek").AsString(50).Nullable()
            .AddColumn("schooltiming").AsString(100).Nullable()
            .AddColumn("classesfrom").AsString(50).Nullable()
            .AddColumn("classesto").AsString(50).Nullable()
            .AddColumn("sectionsperclass").AsInt32().Nullable()
            .AddColumn("sectionnaming").AsString(50).Nullable()
            .AddColumn("maxstudentspersection").AsInt32().Nullable()
            .AddColumn("admissionnumberformat").AsString(100).Nullable()
            .AddColumn("attendancetype").AsString(50).Nullable()
            .AddColumn("minimumattendancepercent").AsInt32().Nullable()
            .AddColumn("latemarkafterminutes").AsInt32().Nullable()
            .AddColumn("autonotifyparentsonabsence").AsBoolean().NotNullable().WithDefaultValue(true)
            .AddColumn("allowbackdatedattendance").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("currency").AsString(10).Nullable().WithDefaultValue("INR")
            .AddColumn("paymentcycle").AsString(50).Nullable()
            .AddColumn("feedueday").AsInt32().Nullable()
            .AddColumn("latefeetype").AsString(50).Nullable()
            .AddColumn("latefeevalue").AsDecimal(12, 2).Nullable()
            .AddColumn("graceperioddays").AsInt32().Nullable()
            .AddColumn("feeheadsjson").AsCustom("jsonb").Nullable()
            .AddColumn("discounttypesjson").AsCustom("jsonb").Nullable()
            .AddColumn("paymentmethodsjson").AsCustom("jsonb").Nullable()
            .AddColumn("portalsettingsjson").AsCustom("jsonb").Nullable()
            .AddColumn("schemaname").AsString(100).Nullable()
            .AddColumn("storageplan").AsString(50).Nullable()
            .AddColumn("dataregion").AsString(50).Nullable()
            .AddColumn("sessiontimeoutminutes").AsInt32().Nullable()
            .AddColumn("passwordpolicy").AsString(50).Nullable()
            .AddColumn("loginattemptsbeforelock").AsInt32().Nullable()
            .AddColumn("twofactorenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("ipwhitelistenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("branchdataisolation").AsBoolean().NotNullable().WithDefaultValue(true)
            .AddColumn("sharedfeestructure").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("centraladminviewallbranches").AsBoolean().NotNullable().WithDefaultValue(true);
    }

    public override void Down()
    {
        // Intentionally left minimal — dropping many columns is destructive in shared environments.
    }
}

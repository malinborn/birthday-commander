using FluentMigrator;

namespace BirthdayCommander.Infrastructure.Data.Migrations;

[Migration(202506221619)]
public class AddInvisibilityFieldToEmployee : Migration
{
    public override void Up()
    {
        Alter.Table("employees").AddColumn("is_invisible").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
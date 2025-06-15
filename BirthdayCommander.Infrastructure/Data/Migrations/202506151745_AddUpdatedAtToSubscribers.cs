using FluentMigrator;

namespace BirthdayCommander.Infrastructure.Data.Migrations;

[Migration(202506151745)]
public class AddUpdatedAtToSubscribers : Migration {
    public override void Up()
    {
        Alter.Table("subscribers")
            .AddColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
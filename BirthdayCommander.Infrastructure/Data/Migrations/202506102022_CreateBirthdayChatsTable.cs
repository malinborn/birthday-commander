using FluentMigrator;
namespace BirthdayCommander.Infrastructure.Data.Migrations;

[Migration(202506102022)]
public class CreateBirthdayChatsTable : Migration
{
    public override void Up()
    {
        Create.Table("birthday_chats")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("employee_id").AsGuid().Nullable()
            .WithColumn("mattermost_channel_id").AsString(100).NotNullable().Unique()
            .WithColumn("birthday_commander_id").AsGuid().Nullable()
            .WithColumn("status").AsString(20).NotNullable().WithDefaultValue("planning")
            .WithColumn("birthday_date").AsDate().NotNullable()
            .WithColumn("is_archived").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
        
        Create.ForeignKey("FK_birthday_chats_employee_id")
            .FromTable("birthday_chats").ForeignColumn("employee_id")
            .ToTable("employees").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.SetNull);
        
        Create.ForeignKey("FK_birthday_chats_birthday_commander_id")
            .FromTable("birthday_chats").ForeignColumn("birthday_commander_id")
            .ToTable("employees").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.Index("IX_birthday_chats_is_archived_status_birthday_date")
            .OnTable("birthday_chats")
            .OnColumn("is_archived").Ascending()
            .OnColumn("status").Ascending()
            .OnColumn("birthday_date").Ascending();
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
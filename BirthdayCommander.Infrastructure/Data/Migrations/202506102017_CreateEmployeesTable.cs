using FluentMigrator;
namespace BirthdayCommander.Infrastructure.Data.Migrations;

[Migration(202506102017)]
public class CreateEmployeesTable : Migration
{
    public override void Up()
    {
        Create.Table("employees")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("email").AsString(64).NotNullable()
            .WithColumn("birthday").AsDate().NotNullable()
            .WithColumn("mattermost_user_id").AsString(100).NotNullable()
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("wishlist_link").AsString(2048).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

        // Понимаю, что на таких маленьких таблицах нет смысла от индексов,
        // но умом понимаю, что по ним все равно будем искать чаще, чем вставлять, 
        // так что пусть будут
        Create.Index("IX_employees_birthday")
            .OnTable("employees")
            .OnColumn("birthday").Ascending();

        Create.Index("IX_employees_email")
            .OnTable("employees")
            .OnColumn("email").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
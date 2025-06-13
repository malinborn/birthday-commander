using FluentMigrator;
namespace BirthdayCommander.Infrastructure.Data.Migrations;


[Migration(202506102021)]
public class CreateSubscribersTable : Migration
{
    public override void Up()
    {
        Create.Table("subscribers")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("birthday_employee_id").AsGuid().NotNullable()
            .WithColumn("subscriber_id").AsGuid().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.ForeignKey("FK_subscribers_birthday_employee_id")
            .FromTable("subscribers").ForeignColumn("birthday_employee_id")
            .ToTable("employees").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);
        
        Create.ForeignKey("FK_subscribers_subscriber_id")
            .FromTable("subscribers").ForeignColumn("subscriber_id")
            .ToTable("employees").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.UniqueConstraint("UQ_subscribers_employee_subscriber_id")
            .OnTable("subscribers")
            .Columns("birthday_employee_id", "subscriber_id");

        Create.Index("IX_subscribers_employee_id")
            .OnTable("subscribers")
            .OnColumn("birthday_employee_id").Ascending();
        
        Create.Index("IX_subscribers_subscriber_id")
            .OnTable("subscribers")
            .OnColumn("subscriber_id").Ascending();
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
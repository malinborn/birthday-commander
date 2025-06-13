using System.Security.AccessControl;

namespace BirthdayCommander.Core.Entities;

public class BirthdayChat
{
    public Guid Id { get; set; }
    public Guid BirthdayEmployeeId { get; set; }
    public string MattermostChannelId { get; set; }
    public Guid BirthdayCommanderId { get; set; }
    public string Status { get; set; }
    public DateTime BirthdayDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<Employee> Subscribers { get; set; }
}

// Create.Table("birthday_chats")
//     .WithColumn("id").AsGuid().PrimaryKey()
//     .WithColumn("employee_id").AsGuid().Nullable()
//     .WithColumn("mattermost_channel_id").AsString(100).NotNullable().Unique()
//     .WithColumn("birthday_commander_id").AsGuid().Nullable()
//     .WithColumn("status").AsString(20).NotNullable().WithDefaultValue("planning")
//     .WithColumn("birthday_date").AsDate().NotNullable()
//     .WithColumn("is_archived").AsBoolean().NotNullable().WithDefaultValue(false)
//     .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
//     .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
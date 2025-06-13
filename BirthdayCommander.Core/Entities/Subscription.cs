namespace BirthdayCommander.Core.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid BirthdayEmployeeId { get; set; }
    public Guid SubscriberId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
    
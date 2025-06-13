using System.Runtime.InteropServices.JavaScript;

namespace BirthdayCommander.Core.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public string email { get; set; }
    public DateTime Birthday { get; set; }
    public string MattermostUserId { get; set; }
    public bool IsActive { get; set; }
    public string WishlistLink { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    
}



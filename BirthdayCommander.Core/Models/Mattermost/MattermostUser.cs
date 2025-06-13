using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostUser
{
    [JsonPropertyName("id")] 
    public string Id { get; set; }
        
    [JsonPropertyName("username")]
    public string Username { get; set; }
        
    [JsonPropertyName("email")]
    public string Email { get; set; }
        
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }
        
    [JsonPropertyName("last_name")]
    public string LastName { get; set; }
        
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; }
        
    [JsonPropertyName("locale")]
    public string Locale { get; set; }
        
    [JsonPropertyName("position")]
    public string Position { get; set; }
        
    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
        
    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }
        
    [JsonPropertyName("delete_at")]
    public long DeleteAt { get; set; }
        
    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }
        
    [JsonPropertyName("notify_props")]
    public Dictionary<string, object> NotifyProps { get; set; }
        
    public string FullName => $"{FirstName} {LastName}".Trim();
}
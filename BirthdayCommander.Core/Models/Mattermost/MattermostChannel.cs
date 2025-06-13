using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
        
    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
        
    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }
        
    [JsonPropertyName("delete_at")]
    public long DeleteAt { get; set; }
        
    [JsonPropertyName("team_id")]
    public string TeamId { get; set; }
        
    [JsonPropertyName("type")]
    public string Type { get; set; }
        
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }
        
    [JsonPropertyName("name")]
    public string Name { get; set; }
        
    [JsonPropertyName("header")]
    public string Header { get; set; }
        
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; }
        
    [JsonPropertyName("creator_id")]
    public string CreatorId { get; set; }
}
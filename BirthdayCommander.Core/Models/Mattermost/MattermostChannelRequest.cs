using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostChannelRequest
{
    [JsonPropertyName("team_id")]
    public string TeamId { get; set; }
        
    [JsonPropertyName("name")]
    public string Name { get; set; }
        
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }
        
    [JsonPropertyName("type")]
    public string Type { get; set; }
        
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; }
        
    [JsonPropertyName("header")]
    public string Header { get; set; }
}
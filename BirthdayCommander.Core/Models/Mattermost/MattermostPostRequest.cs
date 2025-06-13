using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostPostRequest
{
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
        
    [JsonPropertyName("message")]
    public string Message { get; set; }
        
    [JsonPropertyName("root_id")]
    public string RootId { get; set; }
        
    [JsonPropertyName("file_ids")]
    public List<string> FileIds { get; set; }
        
    [JsonPropertyName("props")]
    public Dictionary<string, object> Props { get; set; }
}
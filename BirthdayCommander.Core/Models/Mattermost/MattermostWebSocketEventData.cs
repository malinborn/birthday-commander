using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostWebSocketEventData
{
    [JsonPropertyName("post")]
    public string PostJson { get; set; }
        
    [JsonPropertyName("channel_type")]
    public string ChannelType { get; set; }
        
    [JsonPropertyName("team_id")]
    public string TeamId { get; set; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
        
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
}
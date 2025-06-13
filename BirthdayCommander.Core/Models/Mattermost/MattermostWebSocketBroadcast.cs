using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostWebSocketBroadcast
{
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
        
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
}
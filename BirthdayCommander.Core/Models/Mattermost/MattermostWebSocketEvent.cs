using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostWebSocketEvent
{
    [JsonPropertyName("event")]
    public string Event { get; set; }

    [JsonPropertyName("data")]
    public MattermostWebSocketEventData Data { get; set; }
    
    [JsonPropertyName("broadcast")]
    public MattermostWebSocketBroadcast Broadcast { get; set; }
}
using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostPostFromWebSocket
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
        
    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
        
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
        
    [JsonPropertyName("message")]
    public string Message { get; set; }
        
    [JsonPropertyName("type")]
    public string Type { get; set; }
}
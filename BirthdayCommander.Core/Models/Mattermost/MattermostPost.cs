using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostPost
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
        
    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
        
    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }
        
    [JsonPropertyName("delete_at")]
    public long DeleteAt { get; set; }
        
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
        
    [JsonPropertyName("root_id")]
    public string RootId { get; set; }
        
    [JsonPropertyName("message")]
    public string Message { get; set; }
        
    [JsonPropertyName("type")]
    public string Type { get; set; }
        
    [JsonPropertyName("props")]
    public Dictionary<string, object> Props { get; set; }
        
    [JsonPropertyName("hashtags")]
    public string Hashtags { get; set; }
        
    [JsonPropertyName("pending_post_id")]
    public string PendingPostId { get; set; }
}
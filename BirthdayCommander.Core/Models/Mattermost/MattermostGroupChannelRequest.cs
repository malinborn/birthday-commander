using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostGroupChannelRequest
{
    [JsonPropertyName("user_ids")]
    public List<string> UserIds { get; set; }
}
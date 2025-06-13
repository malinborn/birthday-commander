using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostDirectChannelRequest
{
    [JsonPropertyName("user_ids")]
    public string[] UserIds { get; set; }
}
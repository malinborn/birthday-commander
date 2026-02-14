# Mattermost Models (DTOs)

Модели данных для работы с Mattermost API и WebSocket событиями.

## WebSocket Event Models

### MattermostWebSocketEvent

Корневая модель для всех WebSocket событий:

```csharp
using System.Text.Json.Serialization;

namespace BirthdayCommander.Core.Models.Mattermost;

public class MattermostWebSocketEvent
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("seq")]
    public int? Seq { get; set; }

    [JsonPropertyName("data")]
    public MattermostWebSocketEventData? Data { get; set; }

    [JsonPropertyName("broadcast")]
    public MattermostWebSocketBroadcast? Broadcast { get; set; }
}
```

### MattermostWebSocketEventData

Данные события:

```csharp
public class MattermostWebSocketEventData
{
    /// <summary>
    /// JSON строка с данными поста (нужна десериализация)
    /// </summary>
    [JsonPropertyName("post")]
    public string? PostJson { get; set; }

    /// <summary>
    /// Тип канала: "D" (direct), "O" (open), "P" (private), "G" (group)
    /// </summary>
    [JsonPropertyName("channel_type")]
    public string? ChannelType { get; set; }

    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }

    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }
}
```

### MattermostWebSocketBroadcast

Информация о broadcast:

```csharp
public class MattermostWebSocketBroadcast
{
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }
}
```

## Post Models

### MattermostPostFromWebSocket

Модель поста из WebSocket (для десериализации из PostJson):

```csharp
public class MattermostPostFromWebSocket
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }

    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("root_id")]
    public string? RootId { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("props")]
    public Dictionary<string, object>? Props { get; set; }

    [JsonPropertyName("hashtags")]
    public string? Hashtags { get; set; }

    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    /// DateTime из Unix timestamp (миллисекунды)
    /// </summary>
    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreateAt).DateTime;
}
```

### MattermostPostRequest

Модель для создания поста:

```csharp
public class MattermostPostRequest
{
    [JsonPropertyName("channel_id")]
    public required string ChannelId { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("root_id")]
    public string? RootId { get; set; }

    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    [JsonPropertyName("props")]
    public Dictionary<string, object>? Props { get; set; }

    [JsonPropertyName("metadata")]
    public MattermostPostMetadata? Metadata { get; set; }
}
```

### MattermostPost

Полная модель поста (ответ API):

```csharp
public class MattermostPost
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }

    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }

    [JsonPropertyName("delete_at")]
    public long DeleteAt { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("root_id")]
    public string? RootId { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("props")]
    public Dictionary<string, object>? Props { get; set; }

    [JsonPropertyName("hashtags")]
    public string? Hashtags { get; set; }

    [JsonPropertyName("pending_post_id")]
    public string? PendingPostId { get; set; }

    [JsonPropertyName("metadata")]
    public MattermostPostMetadata? Metadata { get; set; }

    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreateAt).DateTime;
    public DateTime UpdatedAt => DateTimeOffset.FromUnixTimeMilliseconds(UpdateAt).DateTime;
    public bool IsDeleted => DeleteAt > 0;
}
```

### MattermostPostMetadata

```csharp
public class MattermostPostMetadata
{
    [JsonPropertyName("embeds")]
    public List<MattermostEmbed>? Embeds { get; set; }

    [JsonPropertyName("emojis")]
    public List<MattermostEmoji>? Emojis { get; set; }

    [JsonPropertyName("files")]
    public List<MattermostFile>? Files { get; set; }

    [JsonPropertyName("reactions")]
    public List<MattermostReaction>? Reactions { get; set; }
}
```

## User Model

### MattermostUser

```csharp
public class MattermostUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "en";

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("roles")]
    public string Roles { get; set; } = string.Empty;

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }

    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }

    [JsonPropertyName("delete_at")]
    public long DeleteAt { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("bot_description")]
    public string? BotDescription { get; set; }

    [JsonPropertyName("auth_service")]
    public string? AuthService { get; set; }

    [JsonPropertyName("auth_data")]
    public string? AuthData { get; set; }

    [JsonPropertyName("notify_props")]
    public Dictionary<string, object>? NotifyProps { get; set; }

    [JsonPropertyName("last_picture_update")]
    public long LastPictureUpdate { get; set; }

    /// <summary>
    /// Полное имя: "FirstName LastName"
    /// </summary>
    public string FullName => string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
        ? Username
        : $"{FirstName} {LastName}".Trim();

    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreateAt).DateTime;
    public bool IsDeleted => DeleteAt > 0;
}
```

## Channel Models

### MattermostChannel

```csharp
public class MattermostChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }

    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }

    [JsonPropertyName("delete_at")]
    public long DeleteAt { get; set; }

    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }

    /// <summary>
    /// "O" (open/public), "P" (private), "D" (direct), "G" (group)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("header")]
    public string? Header { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("creator_id")]
    public string? CreatorId { get; set; }

    [JsonPropertyName("last_post_at")]
    public long LastPostAt { get; set; }

    [JsonPropertyName("total_msg_count")]
    public long TotalMsgCount { get; set; }

    public bool IsDirect => Type == "D";
    public bool IsPrivate => Type == "P";
    public bool IsPublic => Type == "O";
    public bool IsGroup => Type == "G";
}
```

### MattermostChannelRequest

Для создания канала:

```csharp
public class MattermostChannelRequest
{
    [JsonPropertyName("team_id")]
    public required string TeamId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("display_name")]
    public required string DisplayName { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("header")]
    public string? Header { get; set; }
}
```

### MattermostDirectChannelRequest

Для создания direct channel:

```csharp
public class MattermostDirectChannelRequest
{
    [JsonPropertyName("user_ids")]
    public required string[] UserIds { get; set; }
}
```

### MattermostGroupChannelRequest

Для создания group channel:

```csharp
public class MattermostGroupChannelRequest
{
    [JsonPropertyName("user_ids")]
    public required List<string> UserIds { get; set; }
}
```

## Дополнительные модели

### MattermostReaction

```csharp
public class MattermostReaction
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("post_id")]
    public string PostId { get; set; } = string.Empty;

    [JsonPropertyName("emoji_name")]
    public string EmojiName { get; set; } = string.Empty;

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
}
```

### MattermostFile

```csharp
public class MattermostFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("extension")]
    public string Extension { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
}
```

### MattermostEmoji

```csharp
public class MattermostEmoji
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
}
```

### MattermostEmbed

```csharp
public class MattermostEmbed
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }
}
```

## Использование

### JSON Serialization Options

```csharp
private readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
};
```

### Десериализация WebSocket события

```csharp
var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(json, _jsonOptions);

if (wsEvent?.Event == "posted" && wsEvent.Data?.ChannelType == "D")
{
    var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(
        wsEvent.Data.PostJson!,
        _jsonOptions);
}
```

### Сериализация запроса

```csharp
var request = new MattermostPostRequest
{
    ChannelId = "channel-id",
    Message = "Hello!"
};

var json = JsonSerializer.Serialize(request, _jsonOptions);
```

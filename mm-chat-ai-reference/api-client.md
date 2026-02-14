# Mattermost REST API Client

HTTP клиент для взаимодействия с Mattermost REST API v4.

## Конфигурация HttpClient

### Program.cs

```csharp
var mattermostServerUrl = builder.Configuration["Mattermost:ServerUrl"]
    ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");
var mattermostBotToken = builder.Configuration["Mattermost:BotToken"]
    ?? throw new InvalidOperationException("Mattermost:BotToken not configured");

builder.Services.AddHttpClient<IMattermostService, MattermostService>(client =>
{
    client.BaseAddress = new Uri(mattermostServerUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {mattermostBotToken}");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## Интерфейс сервиса

```csharp
public interface IMattermostService
{
    // Users
    Task<string> GetMeAsync();
    Task<MattermostUser?> GetUserByIdAsync(string userId);
    Task<MattermostUser?> GetUserByEmailAsync(string email);

    // Posts
    Task SendMessageAsync(string channelId, string message);
    Task<MattermostPost?> GetPostAsync(string postId);

    // Channels
    Task<MattermostChannel?> GetChannelAsync(string channelId);
    Task<MattermostChannel?> CreateDirectChannelAsync(string[] userIds);
    Task<MattermostChannel?> CreateGroupChannelAsync(List<string> userIds);
}
```

## Реализация

```csharp
using System.Text.Json;

namespace BirthdayCommander.Infrastructure.Services;

public class MattermostService(HttpClient httpClient, ILogger<MattermostService> logger) : IMattermostService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<string> GetMeAsync()
    {
        var response = await httpClient.GetAsync("/api/v4/users/me");
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<MattermostUser>(_jsonOptions);
        return user?.Id ?? throw new InvalidOperationException("Failed to get bot user ID");
    }

    public async Task<MattermostUser?> GetUserByIdAsync(string userId)
    {
        var response = await httpClient.GetAsync($"/api/v4/users/{userId}");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get user {UserId}: {StatusCode}", userId, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MattermostUser>(_jsonOptions);
    }

    public async Task<MattermostUser?> GetUserByEmailAsync(string email)
    {
        var response = await httpClient.GetAsync($"/api/v4/users/email/{Uri.EscapeDataString(email)}");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get user by email {Email}: {StatusCode}", email, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MattermostUser>(_jsonOptions);
    }

    public async Task SendMessageAsync(string channelId, string message)
    {
        var request = new MattermostPostRequest
        {
            ChannelId = channelId,
            Message = message
        };

        var response = await httpClient.PostAsJsonAsync("/api/v4/posts", request, _jsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Failed to send message: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to send message: {response.StatusCode}");
        }

        logger.LogDebug("Message sent to channel {ChannelId}", channelId);
    }

    public async Task<MattermostPost?> GetPostAsync(string postId)
    {
        var response = await httpClient.GetAsync($"/api/v4/posts/{postId}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<MattermostPost>(_jsonOptions);
    }

    public async Task<MattermostChannel?> GetChannelAsync(string channelId)
    {
        var response = await httpClient.GetAsync($"/api/v4/channels/{channelId}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<MattermostChannel>(_jsonOptions);
    }

    public async Task<MattermostChannel?> CreateDirectChannelAsync(string[] userIds)
    {
        var request = new MattermostDirectChannelRequest { UserIds = userIds };
        var response = await httpClient.PostAsJsonAsync("/api/v4/channels/direct", request, _jsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to create direct channel: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MattermostChannel>(_jsonOptions);
    }

    public async Task<MattermostChannel?> CreateGroupChannelAsync(List<string> userIds)
    {
        var request = new MattermostGroupChannelRequest { UserIds = userIds };
        var response = await httpClient.PostAsJsonAsync("/api/v4/channels/group", request, _jsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to create group channel: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MattermostChannel>(_jsonOptions);
    }
}
```

## Основные API endpoints

### Users

| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/v4/users/me` | Текущий пользователь (бот) |
| GET | `/api/v4/users/{user_id}` | Пользователь по ID |
| GET | `/api/v4/users/email/{email}` | Пользователь по email |
| GET | `/api/v4/users/username/{username}` | Пользователь по username |

### Posts (Messages)

| Method | Endpoint | Описание |
|--------|----------|----------|
| POST | `/api/v4/posts` | Создать пост |
| GET | `/api/v4/posts/{post_id}` | Получить пост |
| PUT | `/api/v4/posts/{post_id}` | Редактировать пост |
| DELETE | `/api/v4/posts/{post_id}` | Удалить пост |

### Channels

| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/v4/channels/{channel_id}` | Получить канал |
| POST | `/api/v4/channels/direct` | Создать direct channel |
| POST | `/api/v4/channels/group` | Создать group channel |
| PUT | `/api/v4/channels/{channel_id}` | Обновить канал |

### Teams

| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/v4/teams` | Список команд |
| GET | `/api/v4/teams/{team_id}` | Получить команду |

## Отправка сообщений

### Простое сообщение

```csharp
await mattermostService.SendMessageAsync(channelId, "Привет, это ответ бота!");
```

### Thread reply (ответ в тред)

```csharp
public async Task SendThreadReplyAsync(string channelId, string rootPostId, string message)
{
    var request = new MattermostPostRequest
    {
        ChannelId = channelId,
        Message = message,
        RootId = rootPostId  // ID родительского поста
    };

    await httpClient.PostAsJsonAsync("/api/v4/posts", request, _jsonOptions);
}
```

### Сообщение с файлами

```csharp
public async Task SendMessageWithFilesAsync(string channelId, string message, List<string> fileIds)
{
    var request = new MattermostPostRequest
    {
        ChannelId = channelId,
        Message = message,
        FileIds = fileIds  // IDs загруженных файлов
    };

    await httpClient.PostAsJsonAsync("/api/v4/posts", request, _jsonOptions);
}
```

## Создание каналов

### Direct Channel (1-to-1)

```csharp
// Создаёт или возвращает существующий direct channel
var channel = await mattermostService.CreateDirectChannelAsync([botUserId, targetUserId]);
await mattermostService.SendMessageAsync(channel.Id, "Привет!");
```

### Group Channel

```csharp
// Создаёт group channel с несколькими участниками
var channel = await mattermostService.CreateGroupChannelAsync([userId1, userId2, userId3]);
await mattermostService.SendMessageAsync(channel.Id, "Всем привет!");
```

## Обработка ошибок

```csharp
public async Task<MattermostUser?> GetUserByIdSafeAsync(string userId)
{
    try
    {
        var response = await httpClient.GetAsync($"/api/v4/users/{userId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("User not found: {UserId}", userId);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError("Unauthorized - check bot token");
            throw new UnauthorizedAccessException("Invalid bot token");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MattermostUser>(_jsonOptions);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "HTTP error getting user {UserId}", userId);
        throw;
    }
    catch (JsonException ex)
    {
        logger.LogError(ex, "JSON deserialization error for user {UserId}", userId);
        throw;
    }
}
```

## Webhook Secret (опционально)

Для валидации входящих webhook запросов:

```csharp
public bool ValidateWebhookToken(string? token)
{
    var expectedSecret = _configuration["Mattermost:WebhookSecret"];
    return !string.IsNullOrEmpty(expectedSecret) && token == expectedSecret;
}
```

Использование в контроллере/endpoint:

```csharp
app.MapPost("/webhook", (HttpRequest request, [FromHeader(Name = "X-Mattermost-Webhook-Secret")] string? secret) =>
{
    if (!_mattermostService.ValidateWebhookToken(secret))
        return Results.Unauthorized();

    // Обработка webhook
    return Results.Ok();
});
```

## Retry Policy (Polly)

ДляProduction рекомендуется добавить retry политику:

```csharp
using Polly;

var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

builder.Services.AddHttpClient<IMattermostService, MattermostService>()
    .AddPolicyHandler(retryPolicy);
```

```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
```

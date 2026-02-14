# WebSocket Client

Mattermost WebSocket API используется для получения событий в реальном времени.

## Подключение

### URL Formation

```
REST URL:  https://mattermost.company.com
WebSocket: wss://mattermost.company.com/api/v4/websocket
```

```csharp
var wsUrl = serverUrl
    .Replace("https://", "wss://")
    .Replace("http://", "ws://");

wsUrl = $"{wsUrl}/api/v4/websocket";
```

### Authentication Header

```csharp
var client = new ClientWebSocket();
client.Options.SetRequestHeader("Authorization", $"Bearer {botToken}");
client.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
```

## Аутентификация

После установки соединения необходимо отправить authentication challenge:

```csharp
public class MattermostBotService(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    IMattermostService mattermostService,
    ILogger<MattermostBotService> logger) : BackgroundService
{
    private readonly string _botToken = configuration["Mattermost:BotToken"]
        ?? throw new InvalidOperationException("Mattermost:BotToken not configured");
    private readonly string _serverUrl = configuration["Mattermost:ServerUrl"]
        ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");

    private string? _botUserId;
    private int _sequenceNumber = 1;
    private readonly object _sequenceLock = new();

    private int GetNextSequence()
    {
        lock (_sequenceLock)
        {
            return _sequenceNumber++;
        }
    }

    private async Task SendAuthenticationChallenge(ClientWebSocket client, CancellationToken ct)
    {
        var authMessage = new
        {
            seq = GetNextSequence(),
            action = "authentication_challenge",
            data = new { token = _botToken }
        };

        var json = JsonSerializer.Serialize(authMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

        logger.LogInformation("Sent authentication challenge");
    }
}
```

## Keep-Alive механизм

Mattermost требует периодических ping сообщений для поддержания соединения:

```csharp
private Timer? _pingTimer;
private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(30);

private void StartPingTimer(ClientWebSocket client)
{
    _pingTimer = new Timer(
        callback: _ => _ = SendPingAsync(client),
        state: null,
        dueTime: _pingInterval,
        period: _pingInterval);
}

private async Task SendPingAsync(ClientWebSocket client)
{
    if (client.State != WebSocketState.Open)
        return;

    try
    {
        var pingMessage = new
        {
            seq = GetNextSequence(),
            action = "ping"
        };

        var json = JsonSerializer.Serialize(pingMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        logger.LogDebug("Sent ping, seq: {Seq}", pingMessage.seq);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send ping");
    }
}

private void StopPingTimer()
{
    _pingTimer?.Dispose();
    _pingTimer = null;
}
```

## Цикл получения сообщений

```csharp
private readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

private async Task ReceiveMessagesLoop(ClientWebSocket client, CancellationToken stoppingToken)
{
    var buffer = new byte[8192];

    while (!stoppingToken.IsCancellationRequested && client.State == WebSocketState.Open)
    {
        try
        {
            var result = await client.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                stoppingToken);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessWebSocketMessage(json);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.LogWarning("WebSocket close received");
                break;
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving WebSocket message");
        }
    }
}
```

## Обработка событий

### Основные типы событий

| Event | Описание |
|-------|----------|
| `posted` | Новое сообщение в канале |
| `post_edited` | Сообщение отредактировано |
| `post_deleted` | Сообщение удалено |
| `user_added` | Пользователь добавлен в канал |
| `user_removed` | Пользователь удалён из канала |
| `hello` | Ответ на аутентификацию |

### Фильтрация direct messages

```csharp
private async Task ProcessWebSocketMessage(string json)
{
    var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(json, _jsonOptions);

    if (wsEvent is null)
        return;

    logger.LogDebug("Received WebSocket event: {Event}", wsEvent.Event);

    // Обрабатываем только новые сообщения
    if (wsEvent.Event != "posted")
        return;

    // Проверяем тип канала
    if (wsEvent.Data?.ChannelType != "D")  // "D" = Direct Message
        return;

    // Десериализуем пост из JSON строки
    var postJson = wsEvent.Data.PostJson;
    if (string.IsNullOrEmpty(postJson))
        return;

    var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(postJson, _jsonOptions);
    if (post is null)
        return;

    // Игнорируем сообщения от бота
    if (post.UserId == _botUserId)
    {
        logger.LogDebug("Ignoring message from bot itself");
        return;
    }

    logger.LogInformation(
        "Direct message from {UserId} in channel {ChannelId}: {Message}",
        post.UserId, post.ChannelId, post.Message);

    // Обрабатываем в scoped контексте
    _ = Task.Run(async () =>
    {
        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IDirectMessageHandler>();
        await handler.HandleDirectMessage(post.UserId, post.ChannelId, post.Message);
    });
}
```

## Полный пример BackgroundService

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BirthdayCommander.Infrastructure.Services.BackgroundServices;

public class MattermostBotService(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    IMattermostService mattermostService,
    ILogger<MattermostBotService> logger) : BackgroundService
{
    private readonly string _botToken = configuration["Mattermost:BotToken"]
        ?? throw new InvalidOperationException("Mattermost:BotToken not configured");
    private readonly string _serverUrl = configuration["Mattermost:ServerUrl"]
        ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");

    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(30);

    private string? _botUserId;
    private Timer? _pingTimer;
    private int _sequenceNumber = 1;
    private readonly object _sequenceLock = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MattermostBotService starting...");

        // Получаем ID бота
        _botUserId = await mattermostService.GetMeAsync();
        logger.LogInformation("Bot user ID: {BotUserId}", _botUserId);

        while (!stoppingToken.IsCancellationRequested)
        {
            ClientWebSocket? client = null;

            try
            {
                client = await ConnectAsync(stoppingToken);
                await SendAuthenticationChallenge(client, stoppingToken);
                StartPingTimer(client);
                await ReceiveMessagesLoop(client, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Service stopping...");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WebSocket connection error");
            }
            finally
            {
                StopPingTimer();
                client?.Dispose();
            }

            logger.LogInformation("Reconnecting in {Delay}...", _reconnectDelay);
            await Task.Delay(_reconnectDelay, stoppingToken);
        }
    }

    private async Task<ClientWebSocket> ConnectAsync(CancellationToken ct)
    {
        var wsUrl = _serverUrl
            .Replace("https://", "wss://")
            .Replace("http://", "ws://") + "/api/v4/websocket";

        logger.LogInformation("Connecting to WebSocket: {Url}", wsUrl);

        var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Authorization", $"Bearer {_botToken}");
        client.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        await client.ConnectAsync(new Uri(wsUrl), ct);
        logger.LogInformation("WebSocket connected");

        return client;
    }

    private async Task SendAuthenticationChallenge(ClientWebSocket client, CancellationToken ct)
    {
        var authMessage = new
        {
            seq = GetNextSequence(),
            action = "authentication_challenge",
            data = new { token = _botToken }
        };

        var json = JsonSerializer.Serialize(authMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

        logger.LogInformation("Authentication challenge sent");
    }

    private void StartPingTimer(ClientWebSocket client) =>
        _pingTimer = new Timer(
            _ => _ = SendPingAsync(client),
            null,
            _pingInterval,
            _pingInterval);

    private void StopPingTimer()
    {
        _pingTimer?.Dispose();
        _pingTimer = null;
    }

    private async Task SendPingAsync(ClientWebSocket client)
    {
        if (client.State != WebSocketState.Open)
            return;

        try
        {
            var ping = new { seq = GetNextSequence(), action = "ping" };
            var json = JsonSerializer.Serialize(ping);
            var bytes = Encoding.UTF8.GetBytes(json);
            await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ping failed");
        }
    }

    private int GetNextSequence()
    {
        lock (_sequenceLock)
        {
            return _sequenceNumber++;
        }
    }

    private async Task ReceiveMessagesLoop(ClientWebSocket client, CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested && client.State == WebSocketState.Open)
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessWebSocketMessage(json);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }

    private async Task ProcessWebSocketMessage(string json)
    {
        try
        {
            var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(json, _jsonOptions);

            if (wsEvent?.Event != "posted" || wsEvent.Data?.ChannelType != "D")
                return;

            var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(
                wsEvent.Data.PostJson, _jsonOptions);

            if (post?.UserId == _botUserId)
                return;

            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IDirectMessageHandler>();
            await handler.HandleDirectMessage(post.UserId, post.ChannelId, post.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message");
        }
    }
}
```

## Альтернатива: Websocket.Client library

Для упрощения работы можно использовать библиотеку `Websocket.Client`:

```csharp
using Websocket.Client;

var client = new WebsocketClient(new Uri(wsUrl));

client.ReconnectionHappened.Subscribe(info =>
{
    logger.LogInformation("Reconnection: {Type}", info.Type);
    _ = SendAuthenticationAsync();
});

client.DisconnectionHappened.Subscribe(info =>
{
    logger.LogWarning("Disconnection: {Type}", info.CloseStatus}");
});

client.MessageReceived.Subscribe(msg =>
{
    logger.LogDebug("Message: {Text}", msg.Text);
    _ = ProcessMessageAsync(msg.Text);
});

await client.Start();
```

### Преимущества Websocket.Client

- Автоматический reconnect
- Потокобезопасная отправка
- Интеграция с Reactive Extensions
- Упрощённая обработка событий

### NuGet

```xml
<PackageReference Include="Websocket.Client" Version="5.2.0" />
```

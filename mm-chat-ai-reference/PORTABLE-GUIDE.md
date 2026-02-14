# Mattermost Bot Integration Guide (Portable)

Полное руководство по созданию бота для Mattermost на .NET 8.0. Этот документ самодостаточен — не требует доступа к исходному коду проекта.

---

## Оглавление

1. [Архитектура](#архитектура)
2. [Конфигурация](#конфигурация)
3. [Модели данных (DTOs)](#модели-данных-dtos)
4. [WebSocket Service](#websocket-service)
5. [REST API Service](#rest-api-service)
6. [Command Handler](#command-handler)
7. [DI Registration (Program.cs)](#di-registration-programcs)
8. [Полный минимальный пример](#полный-минимальный-пример)
9. [Docker](#docker)

---

## Архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core Application                  │
├─────────────────────────────────────────────────────────────┤
│  Program.cs                                                  │
│  ├── HttpClient → MattermostService (REST API)              │
│  └── HostedService → MattermostBotService (WebSocket)       │
├─────────────────────────────────────────────────────────────┤
│  MattermostBotService (BackgroundService)                   │
│  ├── WebSocket: wss://server/api/v4/websocket               │
│  ├── Authentication Challenge                                │
│  ├── Ping/Pong Keep-Alive (30 sec)                          │
│  └── Event → DirectMessageHandler (scoped)                  │
├─────────────────────────────────────────────────────────────┤
│  DirectMessageHandler (scoped)                               │
│  ├── MessageParser → Command                                 │
│  ├── Business Logic                                         │
│  └── MattermostService.SendMessageAsync()                   │
├─────────────────────────────────────────────────────────────┤
│  MattermostService (HttpClient, singleton)                   │
│  ├── GET /api/v4/users/{id}                                 │
│  ├── GET /api/v4/users/email/{email}                        │
│  └── POST /api/v4/posts                                     │
└─────────────────────────────────────────────────────────────┘
```

### Поток обработки сообщения

```
1. User → Bot DM в Mattermost
2. Mattermost → WebSocket event ("posted", channel_type="D")
3. MattermostBotService получает событие
4. Десериализация PostJson → MattermostPostFromWebSocket
5. Фильтр: игнорировать сообщения от бота
6. DirectMessageHandler.HandleDirectMessage(userId, channelId, message)
7. MessageParser.Parse(message) → MessageCommand
8. Выполнение бизнес-логики
9. MattermostService.SendMessageAsync(channelId, response)
```

---

## Конфигурация

### appsettings.json

```json
{
  "Mattermost": {
    "ServerUrl": "https://mattermost.company.com",
    "BotToken": "your-bot-token-here",
    "WebhookSecret": "optional-webhook-secret"
  }
}
```

### User Secrets (разработка)

```bash
dotnet user-secrets init
dotnet user-secrets set "Mattermost:ServerUrl" "https://mattermost.company.com"
dotnet user-secrets set "Mattermost:BotToken" "your-bot-token"
```

### Environment Variables (Docker)

```yaml
environment:
  - Mattermost__ServerUrl=https://mattermost.company.com
  - Mattermost__BotToken=${MATTERMOST_BOT_TOKEN}
```

### Требования к боту в Mattermost

1. Создать Bot Account в System Console → Integrations → Bot Accounts
2. Включить `Enable Bot Account Creation`
3. Создать бота с правами на чтение личных сообщений
4. Скопировать Bot Token

---

## Модели данных (DTOs)

### WebSocket Event

```csharp
using System.Text.Json.Serialization;

public class MattermostWebSocketEvent
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public MattermostWebSocketEventData? Data { get; set; }
}

public class MattermostWebSocketEventData
{
    /// <summary>JSON строка с постом (нужна десериализация)</summary>
    [JsonPropertyName("post")]
    public string? PostJson { get; set; }

    /// <summary>"D" = Direct Message</summary>
    [JsonPropertyName("channel_type")]
    public string? ChannelType { get; set; }

    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
}

public class MattermostPostFromWebSocket
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("root_id")]
    public string? RootId { get; set; }

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }
}
```

### User

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

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
```

### Post Request

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
}
```

### Channel

```csharp
public class MattermostChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;  // D, O, P, G

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    public bool IsDirect => Type == "D";
}

public class MattermostDirectChannelRequest
{
    [JsonPropertyName("user_ids")]
    public required string[] UserIds { get; set; }
}
```

---

## WebSocket Service

BackgroundService для постоянного WebSocket соединения с Mattermost.

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

        // Получаем ID бота через REST API
        _botUserId = await mattermostService.GetMeAsync();
        logger.LogInformation("Bot user ID: {BotUserId}", _botUserId);

        while (!stoppingToken.IsCancellationRequested)
        {
            ClientWebSocket? client = null;

            try
            {
                client = await ConnectAsync(stoppingToken);
                await AuthenticateAsync(client, stoppingToken);
                StartPingTimer(client);
                await ReceiveLoopAsync(client, stoppingToken);
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

            // Reconnect после 30 секунд
            logger.LogInformation("Reconnecting in 30 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task<ClientWebSocket> ConnectAsync(CancellationToken ct)
    {
        // https://server.com → wss://server.com/api/v4/websocket
        var wsUrl = _serverUrl
            .Replace("https://", "wss://")
            .Replace("http://", "ws://") + "/api/v4/websocket";

        logger.LogInformation("Connecting to: {Url}", wsUrl);

        var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Authorization", $"Bearer {_botToken}");
        client.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        await client.ConnectAsync(new Uri(wsUrl), ct);
        logger.LogInformation("WebSocket connected");

        return client;
    }

    private async Task AuthenticateAsync(ClientWebSocket client, CancellationToken ct)
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
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

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
            await client.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
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

    private async Task ReceiveLoopAsync(ClientWebSocket client, CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested && client.State == WebSocketState.Open)
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _ = ProcessMessageAsync(json);  // Fire and forget
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.LogWarning("WebSocket close received");
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(string json)
    {
        try
        {
            var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(json, _jsonOptions);

            // Обрабатываем только "posted" в direct messages
            if (wsEvent?.Event != "posted")
                return;

            if (wsEvent.Data?.ChannelType != "D")  // "D" = Direct Message
                return;

            // Десериализуем пост из JSON строки
            var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(
                wsEvent.Data.PostJson!,
                _jsonOptions);

            if (post is null)
                return;

            // Игнорируем сообщения от самого бота
            if (post.UserId == _botUserId)
                return;

            logger.LogInformation(
                "Direct message from {UserId}: {Message}",
                post.UserId, post.Message);

            // Создаём scope для scoped сервисов
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IDirectMessageHandler>();
            await handler.HandleDirectMessage(post.UserId, post.ChannelId, post.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing WebSocket message");
        }
    }
}
```

### Ключевые моменты WebSocket

| Элемент | Значение |
|---------|----------|
| URL | `wss://server/api/v4/websocket` |
| Auth Header | `Authorization: Bearer {token}` |
| Auth Challenge | `{ seq: 1, action: "authentication_challenge", data: { token } }` |
| Ping | `{ seq: N, action: "ping" }` каждые 30 сек |
| Event "posted" | Новое сообщение |
| Channel type "D" | Direct Message |

---

## REST API Service

HttpClient-обёртка для Mattermost REST API v4.

```csharp
using System.Text.Json;

public interface IMattermostService
{
    Task<string> GetMeAsync();
    Task<MattermostUser?> GetUserByIdAsync(string userId);
    Task<MattermostUser?> GetUserByEmailAsync(string email);
    Task SendMessageAsync(string channelId, string message);
    Task<MattermostChannel?> CreateDirectChannelAsync(string[] userIds);
}

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
            logger.LogWarning("Failed to get user {UserId}: {Status}", userId, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MattermostUser>(_jsonOptions);
    }

    public async Task<MattermostUser?> GetUserByEmailAsync(string email)
    {
        var response = await httpClient.GetAsync($"/api/v4/users/email/{Uri.EscapeDataString(email)}");

        if (!response.IsSuccessStatusCode)
            return null;

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
            logger.LogError("Failed to send message: {Status} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to send message: {response.StatusCode}");
        }

        logger.LogDebug("Message sent to channel {ChannelId}", channelId);
    }

    public async Task<MattermostChannel?> CreateDirectChannelAsync(string[] userIds)
    {
        var request = new MattermostDirectChannelRequest { UserIds = userIds };
        var response = await httpClient.PostAsJsonAsync("/api/v4/channels/direct", request, _jsonOptions);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<MattermostChannel>(_jsonOptions);
    }
}
```

### Основные API Endpoints

| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/v4/users/me` | Информация о боте |
| GET | `/api/v4/users/{id}` | Пользователь по ID |
| GET | `/api/v4/users/email/{email}` | Пользователь по email |
| POST | `/api/v4/posts` | Отправить сообщение |
| POST | `/api/v4/channels/direct` | Создать DM канал |

---

## Command Handler

Паттерн обработки команд из сообщений.

### Интерфейсы

```csharp
public interface IDirectMessageHandler
{
    Task HandleDirectMessage(string userId, string channelId, string message);
}

public interface IMessageParser
{
    MessageCommand Parse(string message);
}
```

### Модели команд

```csharp
public enum CommandType
{
    Unknown,
    Help,
    Echo,
    Time,
    Custom
}

public class MessageCommand
{
    public CommandType Type { get; set; } = CommandType.Unknown;
    public List<string> Parameters { get; set; } = [];
    public string? RawMessage { get; set; }
}
```

### MessageParser

```csharp
using System.Text.RegularExpressions;

public class MessageParser : IMessageParser
{
    private static readonly Dictionary<string, CommandType> Commands = new()
    {
        ["help"] = CommandType.Help,
        ["помощь"] = CommandType.Help,
        ["?"] = CommandType.Help,
        ["time"] = CommandType.Time,
        ["время"] = CommandType.Time,
        ["echo"] = CommandType.Echo,
        ["эхо"] = CommandType.Echo,
    };

    public MessageCommand Parse(string message)
    {
        var trimmed = message.Trim();
        var lower = trimmed.ToLowerInvariant();
        var parts = lower.Split(' ', 2, StringSplitOptions.TrimEntries);

        var command = new MessageCommand { RawMessage = trimmed };

        // Точное совпадение команды
        if (Commands.TryGetValue(lower, out var cmd))
        {
            command.Type = cmd;
            return command;
        }

        // Команда с параметрами
        if (parts.Length > 0 && Commands.TryGetValue(parts[0], out cmd))
        {
            command.Type = cmd;
            if (parts.Length > 1)
                command.Parameters.Add(parts[1]);
            return command;
        }

        command.Type = CommandType.Custom;
        command.Parameters.Add(trimmed);
        return command;
    }
}
```

### DirectMessageHandler

```csharp
public class DirectMessageHandler(
    IMattermostService mattermostService,
    IMessageParser messageParser,
    ILogger<DirectMessageHandler> logger) : IDirectMessageHandler
{
    public async Task HandleDirectMessage(string userId, string channelId, string message)
    {
        try
        {
            logger.LogInformation("Handling message from {UserId}: {Message}", userId, message);

            // Получаем информацию о пользователе
            var user = await mattermostService.GetUserByIdAsync(userId);
            var userName = user?.FullName ?? user?.Username ?? "Пользователь";

            // Парсим команду
            var command = messageParser.Parse(message);

            // Выполняем команду
            var response = await ExecuteCommandAsync(command, userName);

            // Отправляем ответ
            await mattermostService.SendMessageAsync(channelId, response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message");
            await mattermostService.SendMessageAsync(
                channelId,
                "Произошла ошибка. Попробуйте позже.");
        }
    }

    private Task<string> ExecuteCommandAsync(MessageCommand command, string userName)
    {
        var response = command.Type switch
        {
            CommandType.Help => GetHelpText(),
            CommandType.Time => $"🕐 Текущее время: {DateTime.Now:HH:mm:ss}",
            CommandType.Echo => command.Parameters.Count > 0
                ? $"📢 {command.Parameters[0]}"
                : "Использование: echo <текст>",
            CommandType.Custom => $"Получено: {command.RawMessage}\nНапишите 'помощь' для списка команд.",
            _ => GetHelpText()
        };

        return Task.FromResult(response);
    }

    private static string GetHelpText() =>
        """
        🤖 **Доступные команды:**

        • `помощь` - Эта справка
        • `время` - Текущее время
        • `echo <текст>` - Повторить текст
        """;
}
```

---

## DI Registration (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuration
var mattermostUrl = builder.Configuration["Mattermost:ServerUrl"]
    ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");
var mattermostToken = builder.Configuration["Mattermost:BotToken"]
    ?? throw new InvalidOperationException("Mattermost:BotToken not configured");

// HttpClient for REST API
builder.Services.AddHttpClient<IMattermostService, MattermostService>(client =>
{
    client.BaseAddress = new Uri(mattermostUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {mattermostToken}");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Background service for WebSocket
builder.Services.AddHostedService<MattermostBotService>();

// Scoped handlers
builder.Services.AddScoped<IDirectMessageHandler, DirectMessageHandler>();
builder.Services.AddScoped<IMessageParser, MessageParser>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
```

### Lifecycle Summary

| Service | Lifestyle | Причина |
|---------|-----------|---------|
| `IMattermostService` | Singleton (HttpClient) | Переиспользование соединений |
| `MattermostBotService` | Singleton (HostedService) | Один WebSocket на приложение |
| `IDirectMessageHandler` | Scoped | Возможность использовать scoped зависимости |
| `IMessageParser` | Scoped | Создаётся вместе с handler |

---

## Полный минимальный пример

### .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

### Структура файлов

```
MyBot/
├── Models/
│   ├── MattermostWebSocketEvent.cs
│   ├── MattermostUser.cs
│   ├── MattermostPost.cs
│   └── MattermostChannel.cs
├── Services/
│   ├── IMattermostService.cs
│   ├── MattermostService.cs
│   ├── MattermostBotService.cs
│   ├── IMessageParser.cs
│   ├── MessageParser.cs
│   ├── IDirectMessageHandler.cs
│   └── DirectMessageHandler.cs
├── Program.cs
├── appsettings.json
└── MyBot.csproj
```

---

## Docker

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "MyBot.dll"]
```

### docker-compose.yml

```yaml
services:
  bot:
    build: .
    environment:
      - Mattermost__ServerUrl=https://mattermost.company.com
      - Mattermost__BotToken=${MATTERMOST_BOT_TOKEN}
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

### Запуск

```bash
# Development
dotnet user-secrets set "Mattermost:ServerUrl" "https://mattermost.company.com"
dotnet user-secrets set "Mattermost:BotToken" "your-token"
dotnet run

# Docker
export MATTERMOST_BOT_TOKEN="your-token"
docker compose up -d
```

---

## Troubleshooting

### WebSocket не подключается

1. Проверьте URL: `wss://` для HTTPS, `ws://` для HTTP
2. Проверьте токен: должен быть валидный Bot Token
3. Проверьте firewall: порт 443/80 должен быть открыт

### Сообщения не приходят

1. Убедитесь что бот имеет доступ к DM
2. Проверьте `channel_type == "D"` в событии
3. Проверьте что `_botUserId` корректно получен

### Reconnection loop

1. Проверьте логи на предмет `WebSocketState`
2. Увеличьте delay между реконнектами
3. Проверьте стабильность сети

---

## Checklist для нового бота

- [ ] Создать Bot Account в Mattermost
- [ ] Скопировать Bot Token
- [ ] Настроить `appsettings.json` или user secrets
- [ ] Реализовать `IDirectMessageHandler`
- [ ] Добавить команды в `MessageParser`
- [ ] Протестировать локально
- [ ] Создать Dockerfile
- [ ] Настроить CI/CD

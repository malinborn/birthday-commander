# Minimal Mattermost Bot Example

Минимальный рабочий пример бота для Mattermost.

## Структура проекта

```
MinimalMattermostBot/
├── Core/
│   ├── Interfaces/
│   │   ├── IMattermostService.cs
│   │   └── IDirectMessageHandler.cs
│   └── Models/
│       └── Mattermost/
│           ├── MattermostWebSocketEvent.cs
│           ├── MattermostPost.cs
│           └── MattermostUser.cs
├── Infrastructure/
│   └── Services/
│       ├── MattermostService.cs
│       └── MattermostBotService.cs
├── API/
│   ├── Handlers/
│   │   └── DirectMessageHandler.cs
│   └── Program.cs
└── MinimalMattermostBot.csproj
```

## MinimalMattermostBot.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Core/Interfaces/IMattermostService.cs

```csharp
namespace MinimalMattermostBot.Core.Interfaces;

public interface IMattermostService
{
    Task<string> GetMeAsync();
    Task<MattermostUser?> GetUserByIdAsync(string userId);
    Task SendMessageAsync(string channelId, string message);
}
```

## Core/Interfaces/IDirectMessageHandler.cs

```csharp
namespace MinimalMattermostBot.Core.Interfaces;

public interface IDirectMessageHandler
{
    Task HandleDirectMessage(string userId, string channelId, string message);
}
```

## Core/Models/Mattermost/MattermostWebSocketEvent.cs

```csharp
using System.Text.Json.Serialization;

namespace MinimalMattermostBot.Core.Models.Mattermost;

public class MattermostWebSocketEvent
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public MattermostWebSocketEventData? Data { get; set; }
}

public class MattermostWebSocketEventData
{
    [JsonPropertyName("post")]
    public string? PostJson { get; set; }

    [JsonPropertyName("channel_type")]
    public string? ChannelType { get; set; }
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
}
```

## Core/Models/Mattermost/MattermostUser.cs

```csharp
using System.Text.Json.Serialization;

namespace MinimalMattermostBot.Core.Models.Mattermost;

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

    public string FullName => $"{FirstName} {LastName}".Trim();
}
```

## Core/Models/Mattermost/MattermostPost.cs

```csharp
using System.Text.Json.Serialization;

namespace MinimalMattermostBot.Core.Models.Mattermost;

public class MattermostPostRequest
{
    [JsonPropertyName("channel_id")]
    public required string ChannelId { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}
```

## Infrastructure/Services/MattermostService.cs

```csharp
using System.Text.Json;
using MinimalMattermostBot.Core.Interfaces;
using MinimalMattermostBot.Core.Models.Mattermost;

namespace MinimalMattermostBot.Infrastructure.Services;

public class MattermostService(HttpClient httpClient) : IMattermostService
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
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MattermostUser>(_jsonOptions)
            : null;
    }

    public async Task SendMessageAsync(string channelId, string message)
    {
        var request = new MattermostPostRequest
        {
            ChannelId = channelId,
            Message = message
        };

        var response = await httpClient.PostAsJsonAsync("/api/v4/posts", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
    }
}
```

## Infrastructure/Services/MattermostBotService.cs

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MinimalMattermostBot.Core.Interfaces;
using MinimalMattermostBot.Core.Models.Mattermost;

namespace MinimalMattermostBot.Infrastructure.Services;

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
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WebSocket error");
            }
            finally
            {
                StopPingTimer();
                client?.Dispose();
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task<ClientWebSocket> ConnectAsync(CancellationToken ct)
    {
        var wsUrl = _serverUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/api/v4/websocket";

        var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Authorization", $"Bearer {_botToken}");
        await client.ConnectAsync(new Uri(wsUrl), ct);

        logger.LogInformation("WebSocket connected");
        return client;
    }

    private async Task AuthenticateAsync(ClientWebSocket client, CancellationToken ct)
    {
        var auth = new { seq = GetNextSequence(), action = "authentication_challenge", data = new { token = _botToken } };
        var json = JsonSerializer.Serialize(auth);
        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, ct);

        logger.LogInformation("Authenticated");
    }

    private void StartPingTimer(ClientWebSocket client) =>
        _pingTimer = new Timer(_ => _ = SendPingAsync(client), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

    private void StopPingTimer()
    {
        _pingTimer?.Dispose();
        _pingTimer = null;
    }

    private async Task SendPingAsync(ClientWebSocket client)
    {
        if (client.State != WebSocketState.Open) return;

        var ping = new { seq = GetNextSequence(), action = "ping" };
        var json = JsonSerializer.Serialize(ping);
        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private int GetNextSequence()
    {
        lock (_sequenceLock) { return _sequenceNumber++; }
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
                _ = ProcessMessageAsync(json);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(string json)
    {
        try
        {
            var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(json, _jsonOptions);

            if (wsEvent?.Event != "posted" || wsEvent.Data?.ChannelType != "D")
                return;

            var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(wsEvent.Data.PostJson!, _jsonOptions);

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

## API/Handlers/DirectMessageHandler.cs

```csharp
using MinimalMattermostBot.Core.Interfaces;

namespace MinimalMattermostBot.API.Handlers;

public class DirectMessageHandler(
    IMattermostService mattermostService,
    ILogger<DirectMessageHandler> logger) : IDirectMessageHandler
{
    public async Task HandleDirectMessage(string userId, string channelId, string message)
    {
        logger.LogInformation("Message from {UserId}: {Message}", userId, message);

        var user = await mattermostService.GetUserByIdAsync(userId);
        var userName = user?.FullName ?? user?.Username ?? "Пользователь";

        var response = message.ToLowerInvariant() switch
        {
            "привет" or "hello" or "hi" => $"Привет, {userName}! Чем могу помочь?",
            "помощь" or "help" => GetHelpText(),
            "время" or "time" => $"Текущее время: {DateTime.Now:HH:mm:ss}",
            _ => $"Получено: {message}\nНапишите 'помощь' для списка команд."
        };

        await mattermostService.SendMessageAsync(channelId, response);
    }

    private static string GetHelpText() =>
        """
        🤖 **Доступные команды:**
        • `привет` - Приветствие
        • `время` - Текущее время
        • `помощь` - Эта справка
        """;
}
```

## API/Program.cs

```csharp
using MinimalMattermostBot.API.Handlers;
using MinimalMattermostBot.Core.Interfaces;
using MinimalMattermostBot.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var mattermostUrl = builder.Configuration["Mattermost:ServerUrl"]
    ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");
var mattermostToken = builder.Configuration["Mattermost:BotToken"]
    ?? throw new InvalidOperationException("Mattermost:BotToken not configured");

// Services
builder.Services.AddHttpClient<IMattermostService, MattermostService>(client =>
{
    client.BaseAddress = new Uri(mattermostUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {mattermostToken}");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<MattermostBotService>();
builder.Services.AddScoped<IDirectMessageHandler, DirectMessageHandler>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
```

## appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Mattermost": {
    "ServerUrl": "",
    "BotToken": ""
  }
}
```

## appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## Запуск

```bash
# User secrets (development)
dotnet user-secrets init
dotnet user-secrets set "Mattermost:ServerUrl" "https://mattermost.company.com"
dotnet user-secrets set "Mattermost:BotToken" "your-bot-token"

# Run
dotnet run
```

## Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "MinimalMattermostBot.dll"]
```

## docker-compose.yml

```yaml
services:
  bot:
    build: .
    environment:
      - Mattermost__ServerUrl=https://mattermost.company.com
      - Mattermost__BotToken=${MATTERMOST_BOT_TOKEN}
    restart: unless-stopped
```

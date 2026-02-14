# Архитектура интеграции

## Обзор слоёв

```
┌─────────────────────────────────────────────────────────────────┐
│                        Core Layer                                │
│  (BirthdayCommander.Core)                                        │
├─────────────────────────────────────────────────────────────────┤
│  Interfaces:                                                     │
│  ├── IMattermostService     - REST API операции                 │
│  ├── IDirectMessageHandler  - Обработка direct messages         │
│  └── IMessageParser         - Парсинг текста в команды          │
│                                                                  │
│  Models:                                                         │
│  ├── MattermostWebSocketEvent   - События от WebSocket          │
│  ├── MattermostPost             - Модели постов                 │
│  ├── MattermostUser             - Модель пользователя           │
│  ├── MattermostChannel          - Модель канала                 │
│  └── MessageCommand/CommandType - Команды бота                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓ implementation
┌─────────────────────────────────────────────────────────────────┐
│                    Infrastructure Layer                          │
│  (BirthdayCommander.Infrastructure)                              │
├─────────────────────────────────────────────────────────────────┤
│  Services:                                                       │
│  ├── MattermostService       - IMattermostService (HttpClient)  │
│  ├── MessageParser           - IMessageParser (regex parsing)   │
│  └── EmployeeService         - Business logic для сотрудников   │
│                                                                  │
│  Background Services:                                            │
│  └── MattermostBotService    - WebSocket connection handler     │
└─────────────────────────────────────────────────────────────────┘
                              ↓ uses
┌─────────────────────────────────────────────────────────────────┐
│                        API Layer                                 │
│  (BirthdayCommander.API)                                         │
├─────────────────────────────────────────────────────────────────┤
│  Program.cs:                                                     │
│  ├── DI Registration                                             │
│  ├── HttpClient setup                                            │
│  └── HostedServices registration                                │
│                                                                  │
│  Handlers:                                                       │
│  └── DirectMessageHandler    - IDirectMessageHandler            │
│       ├── Uses IMessageParser                                    │
│       ├── Uses IEmployeeService                                  │
│       └── Uses IMattermostService                                │
└─────────────────────────────────────────────────────────────────┘
```

## Жизненный цикл подключения

### 1. Запуск приложения (Program.cs)

```csharp
// Регистрация HttpClient для REST API
builder.Services.AddHttpClient<IMattermostService, MattermostService>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {botToken}");
});

// Регистрация BackgroundService для WebSocket
builder.Services.AddHostedService<MattermostBotService>();

// Scoped services для обработки сообщений
builder.Services.AddScoped<IDirectMessageHandler, DirectMessageHandler>();
builder.Services.AddScoped<IMessageParser, MessageParser>();
```

### 2. Инициализация WebSocket (MattermostBotService.ExecuteAsync)

```csharp
public override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // 1. Получаем ID бота через REST API
    _botUserId = await _mattermostService.GetMeAsync();

    // 2. Формируем WebSocket URL
    var wsUrl = _serverUrl.Replace("https://", "wss://") + "/api/v4/websocket";

    // 3. Настраиваем клиент
    var client = new ClientWebSocket();
    client.Options.SetRequestHeader("Authorization", $"Bearer {_botToken}");

    // 4. Подключаемся
    await client.ConnectAsync(new Uri(wsUrl), stoppingToken);

    // 5. Аутентификация
    await SendAuthenticationChallenge(client, stoppingToken);

    // 6. Запуск keep-alive таймера
    _pingTimer = new Timer(SendPing, client, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

    // 7. Цикл чтения сообщений
    await ReceiveMessagesLoop(client, stoppingToken);
}
```

### 3. Аутентификация

```csharp
private async Task SendAuthenticationChallenge(ClientWebSocket client, CancellationToken ct)
{
    var authMessage = new
    {
        seq = 1,
        action = "authentication_challenge",
        data = new { token = _botToken }
    };

    var json = JsonSerializer.Serialize(authMessage);
    var bytes = Encoding.UTF8.GetBytes(json);
    await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
}
```

### 4. Keep-Alive механизм

```csharp
private void SendPing(object? state)
{
    var client = (ClientWebSocket)state;
    var pingMessage = new
    {
        seq = GetNextSequence(),
        action = "ping"
    };
    // Отправка через WebSocket
}
```

## Обработка входящих сообщений

### Фильтрация событий

```csharp
private async Task ProcessWebSocketMessage(string json)
{
    var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(json);

    // Обрабатываем только "posted" события
    if (wsEvent.Event != "posted")
        return;

    // Обрабатываем только direct messages (channel_type == "D")
    if (wsEvent.Data.ChannelType != "D")
        return;

    // Десериализуем пост
    var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(wsEvent.Data.PostJson);

    // Игнорируем сообщения от самого бота
    if (post.UserId == _botUserId)
        return;

    // Обрабатываем в отдельном scope
    using var scope = _serviceProvider.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<IDirectMessageHandler>();
    await handler.HandleDirectMessage(post.UserId, post.ChannelId, post.Message);
}
```

### Scoped обработка

Важно: DirectMessageHandler создаётся в отдельном scope для каждого сообщения:

```csharp
using var scope = _serviceProvider.CreateScope();
var handler = scope.ServiceProvider.GetRequiredService<IDirectMessageHandler>();
await handler.HandleDirectMessage(userId, channelId, message);
```

Это позволяет:
- Использовать scoped зависимости (DbContext, IDbConnection)
- Изолировать обработку разных сообщений
- Корректно управлять ресурсами

## Отправка ответа

```csharp
// В DirectMessageHandler
public async Task HandleDirectMessage(string userId, string channelId, string message)
{
    // ... бизнес-логика ...

    var response = "Ваш ответ пользователю";
    await _mattermostService.SendMessageAsync(channelId, response);
}

// В MattermostService
public async Task SendMessageAsync(string channelId, string message)
{
    var request = new MattermostPostRequest
    {
        ChannelId = channelId,
        Message = message
    };

    var response = await _httpClient.PostAsJsonAsync("/api/v4/posts", request);
    response.EnsureSuccessStatusCode();
}
```

## Обработка ошибок

### В WebSocket Service

```csharp
try
{
    await ProcessWebSocketMessage(json);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing WebSocket message");
    // Не пробрасываем исключение - сервис продолжает работать
}
```

### В Direct Message Handler

```csharp
try
{
    var command = _messageParser.Parse(message);
    var response = await ExecuteCommand(command, employee);
    await _mattermostService.SendMessageAsync(channelId, response);
}
catch (ValidationException ex)
{
    await _mattermostService.SendMessageAsync(channelId, $"Ошибка: {ex.Message}");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error handling direct message");
    await _mattermostService.SendMessageAsync(channelId, "Произошла ошибка. Попробуйте позже.");
}
```

## Reconnection стратегия

```csharp
private readonly TimeSpan _reconnectTimeout = TimeSpan.FromSeconds(30);

private async Task RunWithReconnection(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await ConnectAndProcess(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket connection lost");
            await Task.Delay(_reconnectTimeout, stoppingToken);
        }
    }
}
```

## Зависимости NuGet

```xml
<PackageReference Include="Websocket.Client" Version="5.2.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
```

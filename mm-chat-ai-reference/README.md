# Mattermost Chat Integration Reference

Эта документация описывает архитектуру интеграции с Mattermost для создания AI-ассистентов и ботов.

## Структура документации

| Файл | Описание |
|------|----------|
| [architecture.md](./architecture.md) | Общая архитектура и поток данных |
| [websocket-client.md](./websocket-client.md) | WebSocket подключение и обработка событий |
| [api-client.md](./api-client.md) | REST API клиент Mattermost |
| [models.md](./models.md) | DTOs для WebSocket событий и API запросов |
| [command-handler.md](./command-handler.md) | Паттерн обработки команд |
| [examples/](./examples/) | Минимальные примеры реализации |

## Быстрый старт

### Минимальная архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core Application                  │
├─────────────────────────────────────────────────────────────┤
│  Program.cs                                                  │
│  ├── DI Registration                                         │
│  ├── HttpClient (MattermostService)                         │
│  └── BackgroundService (MattermostBotService)               │
├─────────────────────────────────────────────────────────────┤
│  MattermostBotService (BackgroundService)                   │
│  ├── WebSocket Connection → wss://server/api/v4/websocket   │
│  ├── Authentication Challenge                                │
│  ├── Ping/Pong Keep-Alive                                   │
│  └── Event Dispatch → DirectMessageHandler                  │
├─────────────────────────────────────────────────────────────┤
│  DirectMessageHandler                                        │
│  ├── MessageParser (текст → Command)                        │
│  ├── Business Logic Services                                │
│  └── MattermostService.SendMessageAsync()                   │
├─────────────────────────────────────────────────────────────┤
│  MattermostService (HttpClient)                             │
│  ├── GET /api/v4/users/{id}                                 │
│  ├── GET /api/v4/users/email/{email}                        │
│  └── POST /api/v4/posts                                     │
└─────────────────────────────────────────────────────────────┘
```

### Ключевые компоненты

1. **MattermostBotService** - BackgroundService для WebSocket подключения
2. **MattermostService** - HttpClient-обёртка для REST API
3. **DirectMessageHandler** - Обработка входящих сообщений
4. **MessageParser** - Парсинг текста в структурированные команды
5. **Models** - DTOs для WebSocket событий и API запросов

### Конфигурация

```json
{
  "Mattermost": {
    "ServerUrl": "https://mattermost.company.com",
    "BotToken": "your_bot_token",
    "WebhookSecret": "optional_webhook_secret"
  }
}
```

### DI Registration

```csharp
// HttpClient для REST API
builder.Services.AddHttpClient<IMattermostService, MattermostService>(client =>
{
    client.BaseAddress = new Uri(mattermostServerUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {botToken}");
});

// Background service для WebSocket
builder.Services.AddHostedService<MattermostBotService>();

// Scoped handlers
builder.Services.AddScoped<IDirectMessageHandler, DirectMessageHandler>();
builder.Services.AddScoped<IMessageParser, MessageParser>();
```

## Поток обработки сообщения

```
1. Пользователь пишет боту в Mattermost
                    ↓
2. Mattermost отправляет WebSocket event ("posted")
                    ↓
3. MattermostBotService получает событие
                    ↓
4. Проверка: channel_type == "D" (direct message)
                    ↓
5. Десериализация PostJson → MattermostPostFromWebSocket
                    ↓
6. Фильтрация: игнорировать сообщения от самого бота
                    ↓
7. DirectMessageHandler.HandleDirectMessage(userId, channelId, message)
                    ↓
8. MessageParser.Parse(message) → MessageCommand
                    ↓
9. Выполнение бизнес-логики
                    ↓
10. MattermostService.SendMessageAsync(channelId, response)
```

## Исходный код реализации

| Компонент | Путь в проекте |
|-----------|----------------|
| WebSocket Service | `BirthdayCommander.Infrastructure/Services/BackgroundServices/MattermostBotService.cs` |
| REST API Service | `BirthdayCommander.Infrastructure/Services/MattermostService.cs` |
| Message Parser | `BirthdayCommander.Infrastructure/Services/MessageParser.cs` |
| Direct Message Handler | `BirthdayCommander.API/Handlers/DirectMessageHandler.cs` |
| WebSocket Models | `BirthdayCommander.Core/Models/Mattermost/MattermostWebSocketEvent.cs` |
| Post Models | `BirthdayCommander.Core/Models/Mattermost/MattermostPost.cs` |
| User Models | `BirthdayCommander.Core/Models/Mattermost/MattermostUser.cs` |
| Channel Models | `BirthdayCommander.Core/Models/Mattermost/MattermostChannel.cs` |
| DI Setup | `BirthdayCommander.API/Program.cs` |

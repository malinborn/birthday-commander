# Command Handler Pattern

Паттерн обработки команд из сообщений Mattermost.

## Архитектура

```
WebSocket Event
       ↓
DirectMessageHandler
       ↓
MessageParser.Parse(message)
       ↓
MessageCommand { Type, Parameters }
       ↓
Switch Expression → Handler Method
       ↓
MattermostService.SendMessageAsync(response)
```

## Модели команд

### CommandType (enum)

```csharp
namespace BirthdayCommander.Core.Models.BotCommands;

public enum CommandType
{
    Unknown,
    Subscribe,
    Unsubscribe,
    ShowSubscriptions,
    ShowSubscribers,
    SetWishlist,
    SetBirthday,
    Help,
    ListBirthdaysWeek,
    ChangeInvisibility
}
```

### MessageCommand

```csharp
namespace BirthdayCommander.Core.Models.BotCommands;

public class MessageCommand
{
    public CommandType Type { get; set; } = CommandType.Unknown;
    public List<string> Parameters { get; set; } = [];
    public string? RawMessage { get; set; }
}
```

## Интерфейсы

### IMessageParser

```csharp
namespace BirthdayCommander.Core.Interfaces;

public interface IMessageParser
{
    MessageCommand Parse(string message);
}
```

### IDirectMessageHandler

```csharp
namespace BirthdayCommander.Core.Interfaces;

public interface IDirectMessageHandler
{
    Task HandleDirectMessage(string userId, string channelId, string message);
}
```

## MessageParser Implementation

```csharp
using System.Text.RegularExpressions;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Models.BotCommands;

namespace BirthdayCommander.Infrastructure.Services;

public class MessageParser : IMessageParser
{
    // Ключевые слова для команд (без параметров)
    private static readonly Dictionary<string, CommandType> ParameterlessCommands = new()
    {
        // Help
        ["help"] = CommandType.Help,
        ["помощь"] = CommandType.Help,
        ["?"] = CommandType.Help,

        // Subscriptions
        ["subscriptions"] = CommandType.ShowSubscriptions,
        ["подписки"] = CommandType.ShowSubscriptions,
        ["my subscriptions"] = CommandType.ShowSubscriptions,
        ["мои подписки"] = CommandType.ShowSubscriptions,

        // Subscribers
        ["subscribers"] = CommandType.ShowSubscribers,
        ["подписчики"] = CommandType.ShowSubscribers,
        ["my subscribers"] = CommandType.ShowSubscribers,
        ["мои подписчики"] = CommandType.ShowSubscribers,

        // List birthdays
        ["birthdays"] = CommandType.ListBirthdaysWeek,
        ["дни рождения"] = CommandType.ListBirthdaysWeek,
        ["week"] = CommandType.ListBirthdaysWeek,
        ["неделя"] = CommandType.ListBirthdaysWeek,

        // Invisibility
        ["invisible"] = CommandType.ChangeInvisibility,
        ["невидимость"] = CommandType.ChangeInvisibility,
        ["hide"] = CommandType.ChangeInvisibility,
        ["скрыть"] = CommandType.ChangeInvisibility,
    };

    // Паттерны для команд с параметрами
    private static readonly Regex WishlistUrlPattern = new(
        @"https?://(www\.)?[-a-zA-Z0-9@:%.*+~#=]*(wish|gift|present|birthday)[-a-zA-Z0-9@:%.*+~#=]*\.[a-zA-Z0-9()]{1,6}\b(/[-a-zA-Z0-9()@:%*+.~#/=]*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EmailPattern = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        RegexOptions.Compiled);

    private static readonly Regex DatePattern = new(
        @"\b(\d{1,2})[\/\-\.](\d{1,2})(?:[\/\-\.](\d{2,4}))?\b",
        RegexOptions.Compiled);

    // Ключевые слова для subscribe/unsubscribe
    private static readonly string[] SubscribeKeywords = ["subscribe", "подписаться", "подписка", "sub", "+"];
    private static readonly string[] UnsubscribeKeywords = ["unsubscribe", "отписаться", "отписка", "unsub", "-"];

    public MessageCommand Parse(string message)
    {
        var normalizedMessage = message.Trim().ToLowerInvariant();
        var command = new MessageCommand { RawMessage = message };

        // 1. Проверяем команды без параметров (точное совпадение)
        if (ParameterlessCommands.TryGetValue(normalizedMessage, out var commandType))
        {
            command.Type = commandType;
            return command;
        }

        // 2. Проверяем wishlist URL
        var wishlistMatch = WishlistUrlPattern.Match(message);
        if (wishlistMatch.Success)
        {
            command.Type = CommandType.SetWishlist;
            command.Parameters.Add(wishlistMatch.Value);
            return command;
        }

        // 3. Проверяем email (subscribe/unsubscribe)
        var emailMatches = EmailPattern.Matches(message);
        if (emailMatches.Count > 0)
        {
            // Определяем тип по ключевым словам
            command.Type = DetermineSubscriptionType(normalizedMessage);
            foreach (Match match in emailMatches)
            {
                command.Parameters.Add(match.Value.ToLowerInvariant());
            }
            return command;
        }

        // 4. Проверяем дату (set birthday)
        var dateMatch = DatePattern.Match(message);
        if (dateMatch.Success)
        {
            command.Type = CommandType.SetBirthday;
            command.Parameters.Add(dateMatch.Value);
            return command;
        }

        // 5. Неизвестная команда
        command.Type = CommandType.Unknown;
        return command;
    }

    private static CommandType DetermineSubscriptionType(string message)
    {
        if (SubscribeKeywords.Any(k => message.Contains(k)))
            return CommandType.Subscribe;

        if (UnsubscribeKeywords.Any(k => message.Contains(k)))
            return CommandType.Unsubscribe;

        // По умолчанию - подписка
        return CommandType.Subscribe;
    }
}
```

## DirectMessageHandler Implementation

```csharp
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Models.BotCommands;

namespace BirthdayCommander.API.Handlers;

public class DirectMessageHandler(
    IMattermostService mattermostService,
    IMessageParser messageParser,
    IEmployeeService employeeService,
    ISubscriptionService subscriptionService,
    ILogger<DirectMessageHandler> logger) : IDirectMessageHandler
{
    public async Task HandleDirectMessage(string userId, string channelId, string message)
    {
        try
        {
            logger.LogInformation(
                "Handling direct message from {UserId} in {ChannelId}: {Message}",
                userId, channelId, message);

            // 1. Получаем информацию о пользователе из Mattermost
            var mattermostUser = await mattermostService.GetUserByIdAsync(userId);
            if (mattermostUser is null)
            {
                logger.LogWarning("User not found in Mattermost: {UserId}", userId);
                return;
            }

            // 2. Получаем или создаём сотрудника в БД
            var employee = await employeeService.GetOrCreateAsync(
                mattermostUser.Email,
                mattermostUser.Id,
                mattermostUser.FullName);

            // 3. Парсим команду
            var command = messageParser.Parse(message);
            logger.LogDebug("Parsed command: {CommandType}, Parameters: {Params}",
                command.Type, string.Join(", ", command.Parameters));

            // 4. Выполняем команду
            var response = await ExecuteCommandAsync(command, employee);

            // 5. Отправляем ответ
            await mattermostService.SendMessageAsync(channelId, response);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Validation error for user {UserId}", userId);
            await mattermostService.SendMessageAsync(channelId, $"❌ {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling direct message");
            await mattermostService.SendMessageAsync(
                channelId,
                "Произошла ошибка при обработке команды. Попробуйте позже.");
        }
    }

    private async Task<string> ExecuteCommandAsync(MessageCommand command, Employee employee)
    {
        return await command.Type switch
        {
            CommandType.Subscribe => await HandleSubscribeAsync(command, employee),
            CommandType.Unsubscribe => await HandleUnsubscribeAsync(command, employee),
            CommandType.ShowSubscriptions => await HandleShowSubscriptionsAsync(employee),
            CommandType.ShowSubscribers => await HandleShowSubscribersAsync(employee),
            CommandType.SetWishlist => await HandleSetWishlistAsync(command, employee),
            CommandType.SetBirthday => await HandleSetBirthdayAsync(command, employee),
            CommandType.ListBirthdaysWeek => await HandleListBirthdaysWeekAsync(),
            CommandType.ChangeInvisibility => await HandleChangeInvisibilityAsync(employee),
            CommandType.Help or CommandType.Unknown => GetHelpText(),
            _ => GetHelpText()
        };
    }

    private async Task<string> HandleSubscribeAsync(MessageCommand command, Employee subscriber)
    {
        if (command.Parameters.Count == 0)
            return "Укажите email адрес для подписки. Пример: `подписаться user@company.com`";

        var results = new List<string>();

        foreach (var email in command.Parameters)
        {
            var targetEmployee = await employeeService.GetByEmailAsync(email);
            if (targetEmployee is null)
            {
                results.Add($"❌ Пользователь {email} не найден");
                continue;
            }

            if (targetEmployee.Id == subscriber.Id)
            {
                results.Add($"⚠️ Нельзя подписаться на самого себя");
                continue;
            }

            if (targetEmployee.IsInvisible)
            {
                results.Add($"⚠️ Пользователь {email} скрыл свой день рождения");
                continue;
            }

            var success = await subscriptionService.SubscribeAsync(subscriber.Id, targetEmployee.Id);
            results.Add(success
                ? $"✅ Подписка на {email} оформлена"
                : $"⚠️ Вы уже подписаны на {email}");
        }

        return string.Join("\n", results);
    }

    private async Task<string> HandleUnsubscribeAsync(MessageCommand command, Employee subscriber)
    {
        if (command.Parameters.Count == 0)
            return "Укажите email адрес для отписки. Пример: `отписаться user@company.com`";

        var results = new List<string>();

        foreach (var email in command.Parameters)
        {
            var targetEmployee = await employeeService.GetByEmailAsync(email);
            if (targetEmployee is null)
            {
                results.Add($"❌ Пользователь {email} не найден");
                continue;
            }

            var success = await subscriptionService.UnsubscribeAsync(subscriber.Id, targetEmployee.Id);
            results.Add(success
                ? $"✅ Отписка от {email} выполнена"
                : $"⚠️ Вы не были подписаны на {email}");
        }

        return string.Join("\n", results);
    }

    private async Task<string> HandleShowSubscriptionsAsync(Employee subscriber)
    {
        var subscriptions = await subscriptionService.GetSubscriptionsAsync(subscriber.Id);

        if (subscriptions.Count == 0)
            return "У вас нет активных подписок.";

        var lines = subscriptions.Select((s, i) =>
            $"{i + 1}. {s.TargetEmployee.FullName} ({s.TargetEmployee.Email}) - {s.TargetEmployee.Birthday:dd.MM}");

        return $"📋 **Ваши подписки:**\n{string.Join("\n", lines)}";
    }

    private async Task<string> HandleShowSubscribersAsync(Employee employee)
    {
        var subscribers = await subscriptionService.GetSubscribersAsync(employee.Id);

        if (subscribers.Count == 0)
            return "У вас нет подписчиков.";

        var lines = subscribers.Select((s, i) =>
            $"{i + 1}. {s.Subscriber.FullName} ({s.Subscriber.Email})");

        return $"👥 **Ваши подписчики:**\n{string.Join("\n", lines)}";
    }

    private async Task<string> HandleSetWishlistAsync(MessageCommand command, Employee employee)
    {
        var wishlistUrl = command.Parameters[0];
        await employeeService.SetWishlistAsync(employee.Id, wishlistUrl);
        return $"✅ Ссылка на вишлист сохранена: {wishlistUrl}";
    }

    private async Task<string> HandleSetBirthdayAsync(MessageCommand command, Employee employee)
    {
        var dateStr = command.Parameters[0];

        if (!TryParseBirthday(dateStr, out var birthday))
            return "❌ Неверный формат даты. Используйте DD.MM.YYYY";

        await employeeService.SetBirthdayAsync(employee.Id, birthday.Value);
        return $"✅ День рождения установлен: {birthday:dd.MM.yyyy}";
    }

    private async Task<string> HandleListBirthdaysWeekAsync()
    {
        var birthdays = await employeeService.GetBirthdaysThisWeekAsync();

        if (birthdays.Count == 0)
            return "На ближайшей неделе нет дней рождения.";

        var lines = birthdays.Select(b =>
            $"🎂 {b.FullName} - {b.Birthday:dd.MM} ({DateTime.Today.Year - b.Birthday.Value.Year} лет)");

        return $"🎂 **Дни рождения на неделе:**\n{string.Join("\n", lines)}";
    }

    private async Task<string> HandleChangeInvisibilityAsync(Employee employee)
    {
        var newStatus = await employeeService.ToggleInvisibilityAsync(employee.Id);
        return newStatus
            ? "👻 Ваш день рождения теперь скрыт от других пользователей"
            : "👁️ Ваш день рождения снова виден другим пользователям";
    }

    private static bool TryParseBirthday(string dateStr, out DateTime? birthday)
    {
        birthday = null;

        var parts = dateStr.Split('.', '/', '-');
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var month))
            return false;

        var year = parts.Length > 2 && int.TryParse(parts[2], out var y) ? y : DateTime.Today.Year;

        try
        {
            birthday = new DateTime(year, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetHelpText()
    {
        return """
               🤖 **Birthday Bot - Справка**

               **Подписки:**
               • `подписаться email@company.com` - подписаться на день рождения
               • `отписаться email@company.com` - отписаться
               • `подписки` - показать ваши подписки
               • `подписчики` - показать ваших подписчиков

               **Ваш профиль:**
               • `ДД.ММ.ГГГГ` - установить день рождения
               • `https://wishlist-url` - установить ссылку на вишлист
               • `невидимость` - скрыть/показать день рождения

               **Информация:**
               • `дни рождения` - список на ближайшей неделе
               • `помощь` - эта справка
               """;
    }
}
```

## DI Registration

```csharp
// Program.cs
builder.Services.AddScoped<IDirectMessageHandler, DirectMessageHandler>();
builder.Services.AddScoped<IMessageParser, MessageParser>();
```

## Ключевые принципы

1. **Один handler - одна ответственность**: DirectMessageHandler только координирует, бизнес-логика в сервисах
2. **Async/await везде**: Все операции с БД и API асинхронные
3. **Graceful error handling**: Пользователь всегда получает понятный ответ
4. **Multilingual support**: Ключевые слова на русском и английском
5. **Switch expression**: Чистый dispatch команд
6. **Scoped lifestyle**: Handler создаётся на каждое сообщение

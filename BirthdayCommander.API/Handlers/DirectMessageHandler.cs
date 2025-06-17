using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using BirthdayCommander.Core.Entities;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Interfaces.Handlers;
using BirthdayCommander.Core.Models.BotCommands;
using BirthdayCommander.Core.Models.Mattermost;

namespace BirthdayCommander.Handlers;

public class DirectMessageHandler(
    IMessageParser messageParser,
    IEmployeeService employeeService,
    ISubscriptionService subscriptionService,
    IMattermostService mattermostService,
    ILogger<DirectMessageHandler> logger)
    : IDirectMessageHandler
{

    public async Task HandleDirectMessage(string userId, string channelId, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException($"UserId cannot be null or empty");
            }
            var mattermostUser = await mattermostService.GetUserByIdAsync(userId)!
                                 ?? throw new ArgumentException($"User with id {userId} not found");

            var employee = await employeeService.GetOrCreate(mattermostUser.Email, userId);
            
            logger.LogInformation("Processing message from {Email}: {Message}", mattermostUser.Email, message);

            var command = messageParser.Parse(message);

            // Console.WriteLine(command.Type.ToString());
            // Console.WriteLine(command.RawMessage);
            // Console.WriteLine(employee.Email);
            // for (var i = 0; i < command.Parameters.Count; i++)
            // {
            //     Console.WriteLine($"[{i}] - {command.Parameters[i]}");
            // }
            
            var response = await HandleCommand(command, employee, mattermostUser.Email);
            
            await SendResponse(channelId, response);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Error while processing message", e);
        }
    }
    
    private async Task SendResponse(string channelId, string message)
    {
        try
        {
            await mattermostService.SendMessageAsync(channelId, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send response to channel {ChannelId}", channelId);
        }
    }

    private async Task<string> HandleCommand(MessageCommand command, Employee employee, string userEmail)
    {
        try
        {
            return command.Type switch
            {
                CommandType.Subscribe => await HandleSubscribe(userEmail, command.Parameters),
                CommandType.ShowSubscriptions => await HandleShowSubscriptions(employee.Id),
                CommandType.ShowSubscribers => await HandleShowSubscribers(employee.Id),
                CommandType.SetWishlist => await HandleSetWishlist(employee.Id, command.Parameters[0]),
                CommandType.SetBirthday => await HandleSetBirthday(employee.Id, command.Parameters[0]),
                CommandType.Unsubscribe => await HandleUnsubscribe(userEmail, command.Parameters),
                _ => GetHelpText()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling command {CommandType}", command.Type);
            return "❌ An unexpected error occurred. Please try again later.";
        }
    }
    
    private async Task<string> HandleSubscribe(string subscriberEmail, List<string> targetEmails)
    {
        var results = new List<string>();
        var successCount = 0;

        foreach (var email in targetEmails)
        {
            var success = await subscriptionService.Subscribe(subscriberEmail, email);

            if (success)
            {
                results.Add($"✅ Subscribed to **{email}**");
                successCount++;
            }
            else
            {
                results.Add($"❌ Could not subscribe to **{email}** (user not found or birthday not set)");
            }
        }

        var summary = successCount > 0 
            ? $"\n✨ Successfully subscribed to {successCount} birthday{(successCount > 1 ? "s" : "")}!"
            : "\n⚠️ No subscriptions were created.";

        return string.Join("\n", results) + summary;
    }

    private string GetHelpText()
    {
        return @"##### 🎂 **Birthday Commander Bot**
Я помогаю не забывать о днях рождения ваших друллег и собираю подписчиков именинников вместе, чтобы вы могли вместе подобрать подарок и договориться о хитрой засаде на дейлике для поздравления! 
Вот что я могу сделать:

**📧 Подписаться на дни рождения**
Просто отправьте мне адреса электронной почты:
- `john.doe@example.com`
- `jane@example.com, bob@example.com` _(несколько сразу)_

**📅 Установить ваш день рождения**
Сообщите мне вашу дату рождения:
- `31.12.1990` _(с указанием года)_

**📋 Просмотреть ваши подписки**
- `подписки` - Посмотреть, чьи дни рождения вы отслеживаете

**👥 Просмотреть ваших подписчиков**
- `подписчики` - Посмотреть, кто отслеживает ваш день рождения

**🎁 Установить ваш список желаний**
Отправьте мне URL вашего списка желаний, например с этого сайта:
- `https://mywishlist.online`

**❌ Отписаться**
- `отписка john.doe@example.com`

**❓ Получить помощь**
- `help` - Показать это сообщение

---
💡 **Как это работает**: Когда до чьего-то дня рождения остаётся 2 недели, я создаю групповую беседу со всеми их подписчиками и делюсь их списком желаний. Один человек случайным образом выбирается в качестве ""Birthday Commander"", чтобы координировать подготовку к Дню Рождения!

_Примечание: на всякий случай, лучше напиши мне дату своего рождения, чтобы я был уверен что помню ее верно <3_";
    }

    private async Task<string> HandleUnsubscribe(string subscriberEmail, List<string> commandParameters)
    {
        var employeeEmail = commandParameters[0];
        var isDeleted = await subscriptionService.Unsubscribe(subscriberEmail, employeeEmail);
        if (isDeleted)
        {
            return $"♻️ Deleted subscription to {employeeEmail}";
        }

        {
            return $"👀 There is no subscription to {employeeEmail} for {subscriberEmail}.";
        }
    }

    private async Task<string> HandleSetBirthday(Guid employeeId, string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            return "Please provide your birthday date.\n" +
                   "Example: `31.12.1990`";
        }

        try
        {
            var date = ParseBirthday(dateStr);
            await employeeService.UpdateBirthday(employeeId, date);
            
            return $"✅ Your birthday has been set to **{date:dd MM yyyy}**!" +
                   "\nI'll notify your subscribers 2 weeks before your special day! 🎉";
        }
        catch (Exception ex)
        {
            return $"❌ Could not parse the date: {ex.Message}\n\n" +
                   "Please use format like:\n" +
                   "• `31.12.1990` (with year)";
        }
    }

    private DateTime ParseBirthday(string dateStr)
    {
        if (!DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ArgumentException("Invalid date format");
        }

        return date;
    }

    private async Task<string> HandleSetWishlist(Guid employeeId, string url)
    {
        try
        {
            await employeeService.UpdateWishlist(employeeId, url);
                
            return $"✅ Your wishlist has been updated!\n\n" +
                   $"🎁 **Your wishlist**: {url}\n\n" +
                   "This will be shared with your birthday subscribers when your birthday approaches.";
        }
        catch (ValidationException ex)
        {
            return $"❌ {ex.Message}\n\n" +
                   "Please provide a valid URL starting with http:// or https://";
        }
    }

    private async Task<string> HandleShowSubscribers(Guid employeeId)
    {
        var subscribers = await employeeService.GetSubscribers(employeeId);
        var employee = await employeeService.GetById(employeeId);
        
        if (!subscribers.Any())
        {
            var noBirthdayMsg = employee.Birthday.HasValue 
                ? "" 
                : "\n\n💡 **Tip**: Set your birthday so people can subscribe to it!";
                    
            return "No one is subscribed to your birthday yet." + noBirthdayMsg;
        }
        
        var response = new StringBuilder();
        response.AppendLine("🎉 **Your Birthday Subscribers**");
        response.AppendLine();
        
        if (employee!.Birthday.HasValue)
        {
            response.AppendLine($"📅 Your birthday: **{employee.Birthday.Value:MMMM dd}**");
                
            var daysUntil = GetDaysUntilBirthday(employee.Birthday.Value);

            response.AppendLine($"🎂 Coming up in **{daysUntil} days**!");
            response.AppendLine();
        }
        
        response.AppendLine("People who are subscribed to your birthday:");
        foreach (var subscriber in subscribers.OrderBy(s => s.Email))
        {
            response.AppendLine($"• {subscriber.Email}");
        }
        
        response.AppendLine();
        response.AppendLine($"_Total: {subscribers.Count} subscriber{(subscribers.Count != 1 ? "s" : "")}_");

        if (string.IsNullOrEmpty(employee.WishlistLink))
        {
            response.AppendLine();
            response.AppendLine("💡 **Tip**: Add a wishlist URL so your subscribers know what you'd like!");
            response.AppendLine("To do so, just send me the link to your wishlist 👌");
        }
        
        return response.ToString();
    }
    
    private int GetDaysUntilBirthday(DateTime birthday)
    {
        var today = DateTime.Today;
        var next = new DateTime(today.Year, birthday.Month, birthday.Day);
            
        if (next < today)
            next = next.AddYears(1);
                
        return (next - today).Days;
    }

    private async Task<string> HandleShowSubscriptions(Guid employeeId)
    {
        var subscriptions = await employeeService.GetSubscriptions(employeeId);
            
        if (!subscriptions.Any())
        {
            return "You are not subscribed to anyone's birthday yet.\n\n" +
                   "To subscribe, just send me an email address: `john.doe@example.com`";
        }

        var response = new StringBuilder();
        response.AppendLine("📅 **Your Birthday Subscriptions**");
        response.AppendLine();
            
        var grouped = subscriptions
            .OrderBy(s => s.Birthday?.Month ?? 13)
            .ThenBy(s => s.Birthday?.Day ?? 32)
            .GroupBy(s => s.Birthday?.Month);

        foreach (var group in grouped)
        {
            if (group.Key.HasValue)
            {
                response.AppendLine($"**{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(group.Key.Value)}**");
            }
            else
            {
                response.AppendLine("**No Birthday Set**");
            }

            foreach (var sub in group)
            {
                var birthdayStr = sub.Birthday?.ToString("MMM dd") ?? "Not set";
                var wishlistIcon = !string.IsNullOrEmpty(sub.WishlistLink) ? " 🎁" : "";
                response.AppendLine($"• {sub.Email} - {birthdayStr}{wishlistIcon}");
            }
            response.AppendLine();
        }

        response.AppendLine($"_Total: {subscriptions.Count} subscription{(subscriptions.Count != 1 ? "s" : "")}_");
            
        return response.ToString();
    }
}
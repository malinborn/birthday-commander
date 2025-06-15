using System.Text.RegularExpressions;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Models.BotCommands;

namespace BirthdayCommander.Infrastructure.Services;

public class MessageParser : IMessageParser
{
    private static readonly Regex UrlRegex = new (
        @"https?://(www\.)?[-a-zA-Z0-9@:%.*+~#=]*(wish|gift|present|birthday|anniversary|wedding|registry|list)[-a-zA-Z0-9@:%.*+~#=]*\.[a-zA-Z0-9()]{1,6}\b(/[-a-zA-Z0-9()@:%*+.~#/=]*)?", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
    private static readonly Regex EmailRegex = new (@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
    private static readonly Regex DateRegex = new (@"\b(\d{1,2})[\/\-\.](\d{1,2})(?:[\/\-\.](\d{2,4}))?\b",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, CommandType> UnsubscribeCommandKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "unsubscribe", CommandType.Unsubscribe },
            { "remove", CommandType.Unsubscribe },
            { "rm", CommandType.Unsubscribe },
            { "отписаться", CommandType.Unsubscribe },
            { "отписка", CommandType.Unsubscribe },
            { "удалить", CommandType.Unsubscribe },
        };
    
    private static readonly Dictionary<string, CommandType> BirthdayCommandKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "my birthday", CommandType.SetBirthday },
            { "set birthday", CommandType.SetBirthday },
            { "birthday", CommandType.SetBirthday },
            { "мой день рождения", CommandType.SetBirthday },
            { "день рождения", CommandType.SetBirthday }
        };
    
    private static readonly Dictionary<string, CommandType> ParameterlessCommandKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Help commands
        { "привет", CommandType.Help },
        { "hello", CommandType.Help },
        { "hi", CommandType.Help },
        { "старт", CommandType.Help },
        { "start", CommandType.Help },
        { "help", CommandType.Help },
        { "?", CommandType.Help },
        { "commands", CommandType.Help },
        { "info", CommandType.Help },
        { "h", CommandType.Help },
        { "помощь", CommandType.Help },
        { "команды", CommandType.Help },
            
        // Subscription list commands
        { "my subscriptions", CommandType.ShowSubscriptions },
        { "subscriptions", CommandType.ShowSubscriptions },
        { "list", CommandType.ShowSubscriptions },
        { "ls", CommandType.ShowSubscriptions },
        { "show subscriptions", CommandType.ShowSubscriptions },
        { "мои подписки", CommandType.ShowSubscriptions },
        { "подписки", CommandType.ShowSubscriptions },
            
        // Subscriber list commands
        { "subscribers", CommandType.ShowSubscribers },
        { "my subscribers", CommandType.ShowSubscribers },
        { "ps", CommandType.ShowSubscribers },
        { "who subscribed", CommandType.ShowSubscribers },
        { "подписчики", CommandType.ShowSubscribers },
        { "кто подписан", CommandType.ShowSubscribers },
    };
    
    private static readonly Dictionary<string, CommandType> CommandKeywords = 
        new Dictionary<string, CommandType>(StringComparer.OrdinalIgnoreCase)
            .Concat(ParameterlessCommandKeywords)
            .Concat(BirthdayCommandKeywords)
            .Concat(UnsubscribeCommandKeywords)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    public MessageCommand Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new MessageCommand
            {
                Type = CommandType.Unknown,
                RawMessage = message ?? string.Empty
            };
        }
        
        message = message.ToLower().Trim();

        var command = new MessageCommand { RawMessage = message };

        // check if thats an parameterless command, e.g. "help", "ls", "ps"
        foreach (var kvp in ParameterlessCommandKeywords)
        {
            if (message.Equals(kvp.Key))
            {
                command.Type = kvp.Value;
                var remainder = message.Substring(kvp.Key.Length).Trim();
                if (!string.IsNullOrWhiteSpace(remainder))
                {
                    command.Parameters.Add(remainder);
                }
                command.Parameters = command.Parameters.Distinct().ToList();
                return command;
            }
        }
        
        // Check if thats a wishlist
        var urlMatches = UrlRegex.Matches(message);
        if (urlMatches.Count > 0 && message.Split(' ').Length <= 3)
        {
            command.Type = CommandType.SetWishlist;
            command.Parameters.Add(urlMatches[0].Value);
            command.Parameters = command.Parameters.Distinct().ToList();
            return command;
        }
        
        // check if thats some email 
        var emails = EmailRegex.Matches(message);
        if (emails.Count > 0)
        {
            command.Parameters.AddRange(emails.Select(m => m.Value.ToLower()));
            
            command.Type = ContainsCommandWord(message, UnsubscribeCommandKeywords) 
                ? CommandType.Unsubscribe 
                : CommandType.Subscribe;
            
            command.Parameters = command.Parameters.Distinct().ToList();
            return command;
        }
        
        var dateMatches = DateRegex.Matches(message);
        if (dateMatches.Count > 0)
        {
            command.Type = CommandType.SetBirthday;
            command.Parameters.Add(dateMatches[0].Value);
            command.Parameters = command.Parameters.Distinct().ToList();
            return command;
        }

        command.Type = CommandType.Unknown;
        command.Parameters = command.Parameters.Distinct().ToList();
        return command;
    }
    
    private bool ContainsCommandWord(string message, Dictionary<string, CommandType> keywordsDict)
    {
        var keywords = keywordsDict.Keys;
        foreach (var keyword in keywords)
        {
            if (message.Contains(keyword)) return true;
        }
        return false;
    }
}
namespace BirthdayCommander.Core.Models.BotCommands;

public class MessageCommand
{
    public CommandType Type { get; set; }
    public List<string> Parameters { get; set; } = new();
    public string? RawMessage { get; set; }
}
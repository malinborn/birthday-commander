using BirthdayCommander.Core.Models.BotCommands;

namespace BirthdayCommander.Core.Interfaces;

public interface IMessageParser
{
    MessageCommand Parse(string message);
}
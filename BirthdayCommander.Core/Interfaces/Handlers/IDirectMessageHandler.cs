namespace BirthdayCommander.Core.Interfaces.Handlers;

public interface IDirectMessageHandler
{
    Task HandleDirectMessage(string userId, string channelId, string message);
}
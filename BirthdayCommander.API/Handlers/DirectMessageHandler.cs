using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Interfaces.Handlers;
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

            Console.WriteLine(nameof(command.Type));
            Console.WriteLine(command.RawMessage);
            Console.WriteLine(employee.Email);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Error while processing message", e);
        }
    }
}
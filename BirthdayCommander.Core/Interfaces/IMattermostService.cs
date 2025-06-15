using BirthdayCommander.Core.Models.Mattermost;

namespace BirthdayCommander.Core.Interfaces;

public interface IMattermostService
{
    Task<MattermostUser> GetMeAsync();
    Task<MattermostUser?>? GetUserByIdAsync(string userId);
    Task<MattermostUser?> GetUserByEmailAsync(string email);
    Task<string> CreateGroupChannelAsync(List<string> userIds, string channelName);
    Task<string> CreateDirectChannelAsync(string userId);
    Task SendMessageAsync(string channelId, string message);
    Task SendDirectMessageAsync(string userId, string message);
    Task<MattermostChannel> GetChannelAsync(string channelId);
    Task UpdateChannelDisplayNameAsync(string channelId, string displayName);
    bool ValidateWebhookToken(string token);
    string FormatBirthdayNotificationMessage(string birthdayPersonName, string wishlistUrl, int daysUntil);

}
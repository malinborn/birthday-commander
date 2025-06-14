using BirthdayCommander.Core.Entities;

namespace BirthdayCommander.Core.Interfaces;

public interface ISubscriptionService
{
    Task<bool> Subscribe(string subscriberEmail, string birthdayPersonEmail);
    Task<bool> Unsubscribe(string subscriberEmail, string birthdayPersonEmail);
    // TODO: добавить новые функции
    // Task<List<Employee>> GetUserSubscriptions(int userId);
    // Task<List<Employee>> GetSubscribersForUser(int userId);
    // Task ProcessUpcomingBirthdays();
}
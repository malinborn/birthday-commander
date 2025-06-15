using System.Runtime.InteropServices.ComTypes;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Infrastructure.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace BirthdayCommander.Infrastructure.Services;

public class SubscriptionService(
    ILogger<SubscriptionService> logger,
    IDbConnectionFactory connectionFactory,
    IEmployeeService employeeService,
    IMattermostService mattermostService) : ISubscriptionService
{
    public async Task<bool> Subscribe(string subscriberEmail, string birthdayPersonEmail)
    {
        if (string.IsNullOrWhiteSpace(subscriberEmail)) 
            throw new ArgumentNullException(nameof(subscriberEmail));
        if (string.IsNullOrWhiteSpace(birthdayPersonEmail)) 
            throw new ArgumentNullException(nameof(birthdayPersonEmail));
        
        subscriberEmail = subscriberEmail.ToLower().Trim();
        birthdayPersonEmail = birthdayPersonEmail.ToLower().Trim();
        
        if (subscriberEmail.Equals(birthdayPersonEmail, StringComparison.OrdinalIgnoreCase)) 
            throw new ArgumentException("Subscriber and Birthday Person cannot be the same");
        
        using var connection = connectionFactory.Create();

        try
        {
            var subscriber = await employeeService.GetByEmail(subscriberEmail);
            var birthdayPerson = await employeeService.GetByEmail(birthdayPersonEmail);
            
            if (subscriber == null) 
                throw new ArgumentException($"Subscriber with email {subscriberEmail} not found");
            
            if (birthdayPerson == null) 
                throw new ArgumentException($"Birthday person with email {birthdayPersonEmail} not found"); 
            
            if (!birthdayPerson.Birthday.HasValue) 
                throw new ArgumentException($"Birthday person with email {birthdayPersonEmail} has no birthday set");

            var existingSubscriptionsCount = await connection.ExecuteScalarAsync<int>(
                SqlScripts.GetBirthdaySubscriptions,
                new { SubscriberId = subscriber.Id, BirthdayEmployeeId = birthdayPerson.Id });

            if (existingSubscriptionsCount > 0)
            {
                logger.LogInformation("Subscriber {subscriber} already subscribed to {birthdayPerson}", 
                    subscriber, birthdayPerson);
                return false;
            }
            
            await connection.ExecuteAsync(
                SqlScripts.InsertBirthdaySubscription,
                new { 
                    Id = Guid.NewGuid(), 
                    SubscriberId = subscriber.Id, 
                    BirthdayEmployeeId = birthdayPerson.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow 
                });
            
            logger.LogInformation("Subscriber {subscriber} subscribed to {birthdayPerson}", 
                subscriber, birthdayPerson);
            
            return true;
        }
        catch (Exception ex) when (ex is SqlException || ex is InvalidOperationException)
        {
            logger.LogError(ex, "Error while subscribing {subscriber} to {birthdayPerson}", 
                subscriberEmail, birthdayPersonEmail);
            return false;
        }
    }

    public async Task<bool> Unsubscribe(string subscriberEmail, string birthdayPersonEmail)
    {
        if (string.IsNullOrWhiteSpace(subscriberEmail)) 
            throw new ArgumentNullException(nameof(subscriberEmail));
        if (string.IsNullOrWhiteSpace(birthdayPersonEmail)) 
            throw new ArgumentNullException(nameof(birthdayPersonEmail));
        
        subscriberEmail = subscriberEmail.ToLower().Trim();
        birthdayPersonEmail = birthdayPersonEmail.ToLower().Trim();
        
        using var connection = connectionFactory.Create();
        
        if (subscriberEmail.Equals(birthdayPersonEmail, StringComparison.OrdinalIgnoreCase)) 
            throw new ArgumentException("Subscriber and Birthday Person cannot be the same");
        
        var subscriber = await employeeService.GetByEmail(subscriberEmail);
        var birthdayPerson = await employeeService.GetByEmail(birthdayPersonEmail);
            
        if (subscriber == null) 
            throw new ArgumentException($"Subscriber with email {subscriberEmail} not found");
            
        if (birthdayPerson == null) 
            throw new ArgumentException($"Birthday person with email {birthdayPersonEmail} not found"); 
        
        var deleted = await connection.ExecuteAsync(
            SqlScripts.DeleteSubscription, 
            new { SubscriberId = subscriber.Id, BirthdayEmployeeId = birthdayPerson.Id });

        if (deleted > 0)
        {
            logger.LogInformation("Subscriber {subscriber} unsubscribed from {birthdayPerson}", 
                subscriber, birthdayPerson);
        } 
        
        return deleted > 0;
    }
}
using System.ComponentModel.DataAnnotations;
using BirthdayCommander.Core.Entities;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Infrastructure.Data;
using Dapper;
using Microsoft.Extensions.Logging;

namespace BirthdayCommander.Infrastructure.Services;

public class EmployeeService(
    IDbConnectionFactory connectionFactory, 
    ILogger<EmployeeService> logger) : IEmployeeService
{
    public async Task<Employee> GetOrCreate(string email, string mattermostId)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ValidationException("Email cannot be empty");

        email = email.ToLower().Trim();
        
        using var connection = connectionFactory.Create();
        
        var employee = await connection.QueryFirstOrDefaultAsync<Employee?>(
            SqlScripts.GetEmployeeByEmailOrMattermostId,
            new { Email = email, MattermostId = mattermostId });
        
        logger.LogDebug("Employee {mattermostId} and ID: {Id} found with email {Email}.", employee?.MattermostUserId, employee?.Id, email);

        if (employee == null)
        {
            employee = new Employee
            {
                Id = Guid.NewGuid(),
                Email = email,
                MattermostUserId = mattermostId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await connection.ExecuteAsync(SqlScripts.InsertEmployee, employee);
            
            logger.LogInformation("Created Employee with email {Email}.", email);
            return employee;
        }

        if (string.IsNullOrWhiteSpace(employee.MattermostUserId))
        {
            employee.MattermostUserId = mattermostId;
            await connection.ExecuteAsync(SqlScripts.UpdateEmployeeMattermostId,
                new { Id = employee.Id, MattermostUserId = employee.MattermostUserId, UpdatedAt = DateTime.UtcNow });
            
            logger.LogInformation("Updated Employee mattermostID with email {Email} to {MattermostUserId}.", email,
                mattermostId);
        }
        
        return employee;
    }

    public async Task<Employee?> GetById(Guid id)
    {
        using var connection = connectionFactory.Create();

        return await connection.QueryFirstOrDefaultAsync<Employee>(
            SqlScripts.GetEmployeeById,
            new { Id = id });
        
    }

    public async Task<Employee?> GetByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) 
            throw new ValidationException("Email cannot be empty");
        
        using var connection = connectionFactory.Create();

        return await connection.QueryFirstOrDefaultAsync<Employee>(
            SqlScripts.GetEmployeeByEmail,
            new { Email = email });
    }

    public async Task<Employee?>? GetByMattermostId(string mattermostId)
    {
        if (string.IsNullOrWhiteSpace(mattermostId))
            return null;
        
        using var connection = connectionFactory.Create();
        
        return await connection.QueryFirstOrDefaultAsync<Employee>(
            SqlScripts.GetEmployeeByMattermostId,
            new { MattermostUserId = mattermostId });
    }

    public async Task UpdateWishlist(Guid employeeId, string wishlistLink)
    {
        if (Guid.Empty.Equals(employeeId)) throw new ArgumentException("Employee ID cannot be empty");
        using var connection = connectionFactory.Create();
        
        await connection.ExecuteAsync(SqlScripts.UpdateWishlist, 
            new { Id = employeeId, WishlistLink = wishlistLink, UpdatedAt = DateTime.UtcNow });
    }

    public async Task<List<Employee>> GetSubscriptions(Guid employeeId)
    {
        if (Guid.Empty.Equals(employeeId)) throw new ArgumentException("Employee ID cannot be empty");
        
        using var connection = connectionFactory.Create();
        
        var subscribers = await connection.QueryAsync<Employee>(SqlScripts.GetSubscriptions, 
            new { SubscriberId = employeeId });
        
        return subscribers.ToList();
    }

    public async Task<List<Employee>> GetSubscribers(Guid employeeId)
    {
        if (Guid.Empty.Equals(employeeId)) throw new ArgumentException("Employee ID cannot be empty");
        
        using var connection = connectionFactory.Create();
        
        var subscribers = await connection.QueryAsync<Employee>(SqlScripts.GetSubscribers, 
            new { BirthdayEmployeeId = employeeId });
        
        return subscribers.ToList();
    }

    public async Task UpdateBirthday(Guid employeeId, DateTime birthday)
    {
        if (Guid.Empty.Equals(employeeId)) throw new ArgumentException("Employee ID cannot be empty");
        if (birthday.Equals(default)) throw new ArgumentException("Birthday cannot be empty");

        using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            SqlScripts.UpdateBirthday, 
            new { Id = employeeId, Birthday = birthday, UpdatedAt = DateTime.UtcNow });;
    }

    public async Task<List<Employee>> GetEmployeesWithUpcomingBirthdays(int daysAhead)
    {
        using var connection = connectionFactory.Create();

        var endDate = DateTime.Today.AddDays(daysAhead);

        var employees = await connection.QueryAsync<Employee>(
            SqlScripts.GetEmployeesWithUpcomingBirthdays,
            new { EndDate = endDate });

        return employees.ToList();
    }

    public async Task<List<Employee>> GetAllEmployees()
    {
        using var connection = connectionFactory.Create();
        
        var employees = await connection.QueryAsync<Employee>(
            SqlScripts.GetAllEmployees);
        
        return employees.ToList();
    }
}
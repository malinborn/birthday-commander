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
        
        var employee = await connection.QueryFirstOrDefaultAsync<Employee>(
            SqlScripts.GetEmployeeByEmailOrMattermostId,
            new { Email = email, MattermostId = mattermostId });

        if (employee != null)
        {
            if (!string.IsNullOrEmpty(employee.MattermostUserId)) return employee;
            
            await connection.ExecuteAsync(SqlScripts.UpdateEmployeeMattermostId,
                new { Id = employee.Id, MattermostUserId = employee.MattermostUserId, UpdatedAt = DateTime.UtcNow });
                
            employee.MattermostUserId = mattermostId;
            logger.LogInformation("Updated Employee mattermostID with email {Email} to {MattermostUserId}.", email, mattermostId);

            return employee;
        }

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

    public async Task<List<Employee>> GetAllEmployees()
    {
        using var connection = connectionFactory.Create();
        
        var employees = await connection.QueryAsync<Employee>(
            SqlScripts.GetAllEmployees);
        
        return employees.ToList();
    }
}
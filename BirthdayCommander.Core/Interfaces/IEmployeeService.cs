using BirthdayCommander.Core.Entities;

namespace BirthdayCommander.Core.Interfaces;

public interface IEmployeeService
{
    Task<Employee> GetOrCreate(string email, string mattermostId);
    Task<Employee?> GetById(Guid id);
    Task<Employee?> GetByEmail(string email);
    Task<Employee?>? GetByMattermostId(string mattermostId);
    Task UpdateWishlist(Guid employeeId, string wishlistLink);
    Task<List<Employee>> GetSubscriptions(Guid employeeId);
    Task<List<Employee>> GetSubscribers(Guid employeeId);
    Task UpdateBirthday(Guid employeeId, DateTime birthday);
    Task<List<Employee>> GetEmployeesWithUpcomingBirthdays(int daysAhead);
    Task<List<Employee>> GetAllEmployees();
    Task UpdateInvisibility(Guid employeeId, bool newIsInvisible);
}
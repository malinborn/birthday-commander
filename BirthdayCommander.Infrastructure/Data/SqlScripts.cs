namespace BirthdayCommander.Infrastructure.Data;

public class SqlScripts
{
    // EmployeeService requests
    public const string GetEmployeeByEmailOrMattermostId = @"
    SELECT id, email, birthday, mattermost_user_id, is_active, wishlist_link, created_at, updated_at
    FROM employees
    WHERE LOWER(email) = LOWER(@Email)
    OR LOWER(mattermost_user_id) = LOWER(@MattermostId)";

    public const string UpdateEmployeeMattermostId = @"
    UPDATE employees
    SET mattermost_user_id = @MattermostUserId, updated_at = @UpdatedAt
    WHERE id = @Id";

    public const string InsertEmployee = @"
    INSERT INTO employees (id, email, mattermost_user_id, is_active, created_at, updated_at)
    VALUES (@Id, @Email, @MattermostUserId, @IsActive, @CreatedAt, @UpdatedAt)";

    public const string GetEmployeeById = @"
    SELECT * 
    FROM employees
    WHERE id = @Id";
    
    public const string GetEmployeeByEmail = @"
    SELECT * 
    FROM employees
    WHERE email = @Email";
    
    public const string GetEmployeeByMattermostId = @"
    SELECT * 
    FROM employees
    WHERE mattermost_user_id = @MattermostUserId";

    public const string GetAllEmployees = @"
    SELECT * 
    FROM employees";
    
    // BirthdayService requests
    public const string GetBirthdaySubscriptions = @"
    SELECT COUNT(*) 
    FROM subscriptions
    WHERE subscriber_id = @SubscriberId
    AND birthday_employee_id = @BirthdayEmployeeId";
    
    public const string InsertBirthdaySubscription = @"
    INSERT INTO subscriptions (id, subscriber_id, birthday_employee_id, created_at, updated_at)
    VALUES (@Id, @SubscriberId, @BirthdayEmployeeId, @CreatedAt, @UpdatedAt)";
    
    public const string DeleteSubscription = @"
    DELETE FROM subscriptions
    WHERE subscriber_id = @SubscriberId
    AND birthday_employee_id = @BirthdayEmployeeId";
}
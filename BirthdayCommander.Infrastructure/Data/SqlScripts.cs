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

    public const string UpdateWishlist = @"
    UPDATE employees
    SET wishlist_link = @WishlistLink, updated_at = @UpdatedAt
    WHERE id = @Id";

    public const string GetSubscribers = @"
    SELECT e.id, e.email, e.birthday, e.mattermost_user_id, e.is_active, e.wishlist_link, e.created_at, e.updated_at
    FROM subscribers s
    JOIN employees e ON s.subscriber_id = e.id
    WHERE s.birthday_employee_id = @BirthdayEmployeeId";
    
    public const string GetSubscriptions = @"
    SELECT e.*
    FROM subscribers s
    JOIN employees e ON s.birthday_employee_id = e.id
    WHERE s.subscriber_id = @SubscriberId";

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

    public const string UpdateBirthday = @"
    UPDATE employees 
    SET birthday = @Birthday, updated_at = @UpdatedAt
    WHERE id = @Id";
    
    // BirthdayService requests
    public const string GetBirthdaySubscriptions = @"
    SELECT COUNT(*) 
    FROM subscribers
    WHERE subscriber_id = @SubscriberId
    AND birthday_employee_id = @BirthdayEmployeeId";
    
    public const string InsertBirthdaySubscription = @"
    INSERT INTO subscribers (id, subscriber_id, birthday_employee_id, created_at, updated_at)
    VALUES (@Id, @SubscriberId, @BirthdayEmployeeId, @CreatedAt, @UpdatedAt)";
    
    public const string DeleteSubscription = @"
    DELETE FROM subscribers
    WHERE subscriber_id = @SubscriberId
    AND birthday_employee_id = @BirthdayEmployeeId";

    public const string GetEmployeesWithUpcomingBirthdays = @"
    SELECT id, email, birthday, mattermost_user_id, wishlist_link
    FROM employees 
    WHERE is_active = true 
      AND birthday IS NOT NULL
      AND (
        MAKE_DATE(EXTRACT(YEAR FROM CURRENT_DATE)::int, 
                  EXTRACT(MONTH FROM birthday)::int, 
                  EXTRACT(DAY FROM birthday)::int)
        BETWEEN CURRENT_DATE AND @EndDate
        OR
        MAKE_DATE(EXTRACT(YEAR FROM CURRENT_DATE)::int + 1, 
                  EXTRACT(MONTH FROM birthday)::int, 
                  EXTRACT(DAY FROM birthday)::int)
        BETWEEN CURRENT_DATE AND @EndDate
      )";
}
#!/usr/bin/env dotnet-script
#r "nuget: Npgsql, 9.0.3"

using System;
using System.Collections.Generic;
using Npgsql;

Console.WriteLine("🎂 Генерация тестовых данных для таблицы employees...");

// Замени на свою строку подключения
var connectionString = Args.Count > 0 ? Args[0] : "Host=localhost;Port=5432;Database=testdb_tests;Username=testuser;Password=testpassword;";
Console.WriteLine($"📡 Подключение: {connectionString.Split(';')[0]}");

var employees = GenerateEmployees();
await InsertEmployees(employees, connectionString);

Console.WriteLine($"✅ Успешно добавлено {employees.Count} сотрудников!");



List<Employee> GenerateEmployees()
{
    var employees = new List<Employee>();
    var random = new Random();
    
    // Латинские имена и фамилии
    var firstNames = new[] { 
        "Alexander", "Maria", "Dmitry", "Anna", "Sergey", "Elena", "Andrew", "Olga", 
        "Maxim", "Tatiana", "Vladimir", "Svetlana", "Mikhail", "Irina", "Alexey", 
        "Natasha", "Pavel", "Victoria", "Roman", "Ekaterina", "Igor", "Yulia",
        "Konstantin", "Anastasia", "Evgeny", "Daria", "Nikolay", "Vera", "Anton", "Lydia"
    };
    
    var lastNames = new[] { 
        "Ivanov", "Petrov", "Sidorov", "Kozlov", "Novikov", "Morozov", "Popov", 
        "Volkov", "Sokolov", "Lebedev", "Smirnov", "Kuznetsov", "Fedorov", "Mikhailov",
        "Stepanov", "Yakovlev", "Orlov", "Makarov", "Andreev", "Pavlov", "Alexeev",
        "Grigoriev", "Titov", "Vladimirov", "Romanov", "Antonov", "Konstantinov"
    };
    
    var domains = new[] { "company.com", "corp.com", "office.com", "team.com" };
    
    var wishlistSites = new[] { 
        "https://www.ozon.ru/wishlist/", 
        "https://www.wildberries.ru/wishlist/", 
        "https://market.yandex.ru/wishlist/", 
        "https://www.dns-shop.ru/wishlist/",
        "https://www.mvideo.ru/wishlist/",
        "https://www.eldorado.ru/wishlist/"
    };
    
    Console.WriteLine("📅 Генерируем сотрудников на каждый день года...");
    
    // Генерируем сотрудника на каждый день года
    var startDate = new DateTime(2024, 1, 1);
    for (int day = 0; day < 365; day++)
    {
        var birthday = startDate.AddDays(day);
        var firstName = firstNames[random.Next(firstNames.Length)];
        var lastName = lastNames[random.Next(lastNames.Length)];
        var domain = domains[random.Next(domains.Length)];
        
        // Создаем email на латинице
        var emailName = $"{firstName.ToLower()}.{lastName.ToLower()}";
        if (day > 0) emailName += $".{day + 1:000}"; // Добавляем номер для уникальности
        
        // Обрабатываем 29 февраля - переводим в 28 февраля для обычного года
        var birthdayMonth = birthday.Month;
        var birthdayDay = birthday.Day;
        if (birthdayMonth == 2 && birthdayDay == 29)
        {
            birthdayDay = 28; // 29 февраля -> 28 февраля
        }
        
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Email = $"{emailName}@{domain}",
            Birthday = new DateTime(1990, birthdayMonth, birthdayDay), // Год 1990, важны месяц и день
            MattermostUserId = $"mm_user_{day + 1:000}",
            IsActive = random.Next(10) != 0, // 90% активных
            WishlistLink = random.Next(3) != 0 ? // 66% имеют wishlist
                $"{wishlistSites[random.Next(wishlistSites.Length)]}{random.Next(10000, 99999)}" : null,
            CreatedAt = DateTime.Now.AddMinutes(-random.Next(1, 10000)),
            UpdatedAt = DateTime.Now.AddMinutes(-random.Next(1, 1000))
        };
        
        employees.Add(employee);
        
        if ((day + 1) % 50 == 0)
        {
            Console.WriteLine($"   📝 Сгенерировано {day + 1}/365 сотрудников...");
        }
    }
    
    return employees;
}

async Task InsertEmployees(List<Employee> employees, string connString)
{
    try
    {
        using var connection = new NpgsqlConnection(connString);
        await connection.OpenAsync();
        Console.WriteLine("🔗 Подключение к базе данных установлено");
        
        // Опционально: очистить таблицу
        Console.Write("🗑️  Очистить таблицу перед вставкой? (y/n): ");
        var clearAnswer = Console.ReadLine()?.ToLower();
        if (clearAnswer == "y" || clearAnswer == "yes")
        {
            using var clearCmd = new NpgsqlCommand("TRUNCATE TABLE employees", connection);
            await clearCmd.ExecuteNonQueryAsync();
            Console.WriteLine("✨ Таблица очищена.");
        }
        
        // Массовая вставка с прогрессом
        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            Console.WriteLine("💾 Вставляем данные в базу...");
            
            for (int i = 0; i < employees.Count; i++)
            {
                var employee = employees[i];
                
                var cmd = new NpgsqlCommand(@"
                    INSERT INTO employees (id, email, birthday, mattermost_user_id, is_active, wishlist_link, created_at, updated_at)
                    VALUES (@id, @email, @birthday, @mattermostUserId, @isActive, @wishlistLink, @createdAt, @updatedAt)", 
                    connection, transaction);
                
                cmd.Parameters.AddWithValue("id", employee.Id);
                cmd.Parameters.AddWithValue("email", employee.Email);
                cmd.Parameters.AddWithValue("birthday", employee.Birthday != null ? (object)employee.Birthday : DBNull.Value);
                cmd.Parameters.AddWithValue("mattermostUserId", employee.MattermostUserId != null ? (object)employee.MattermostUserId : DBNull.Value);
                cmd.Parameters.AddWithValue("isActive", employee.IsActive);
                cmd.Parameters.AddWithValue("wishlistLink", employee.WishlistLink != null ? (object)employee.WishlistLink : DBNull.Value);
                cmd.Parameters.AddWithValue("createdAt", employee.CreatedAt);
                cmd.Parameters.AddWithValue("updatedAt", employee.UpdatedAt);
                
                await cmd.ExecuteNonQueryAsync();
                
                if ((i + 1) % 50 == 0)
                {
                    Console.WriteLine($"   💾 Вставлено {i + 1}/{employees.Count} записей...");
                }
            }
            
            await transaction.CommitAsync();
            Console.WriteLine("✅ Данные успешно вставлены в базу.");
            
            // Показываем статистику
            var statsCmd = new NpgsqlCommand(@"
                SELECT 
                    COUNT(*) as total,
                    COUNT(CASE WHEN is_active THEN 1 END) as active,
                    COUNT(CASE WHEN wishlist_link IS NOT NULL THEN 1 END) as with_wishlist
                FROM employees", connection);
            
            using var reader = await statsCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine($"📊 Статистика:");
                Console.WriteLine($"   👥 Всего сотрудников: {reader["total"]}");
                Console.WriteLine($"   ✅ Активных: {reader["active"]}");
                Console.WriteLine($"   🎁 С wishlist: {reader["with_wishlist"]}");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"❌ Ошибка при вставке данных: {ex.Message}");
            throw;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка подключения к базе данных: {ex.Message}");
        Console.WriteLine($"🔧 Connection string: {connString}");
        throw;
    }
}

public class Employee
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime Birthday { get; set; }
    public string MattermostUserId { get; set; }
    public bool IsActive { get; set; }
    public string WishlistLink { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
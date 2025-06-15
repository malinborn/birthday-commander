using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql; 


namespace BirthdayCommander.Infrastructure.Data;

public class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("No connection string found");
        
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public IDbConnection Create()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
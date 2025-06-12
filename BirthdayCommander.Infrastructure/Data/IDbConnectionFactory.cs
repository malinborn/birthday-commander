using System.Data;

namespace BirthdayCommander.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
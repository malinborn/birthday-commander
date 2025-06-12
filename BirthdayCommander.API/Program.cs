using System.Data;
using BirthdayCommander.Infrastructure.Data;
using FluentMigrator.Runner;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.All())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();


var app = builder.Build();

var migrationRunner = app.Services.GetRequiredKeyedService<IMigrationRunner>(connectionString);
migrationRunner.MigrateUp();

app.MapGet("/", () => "Hello World!");

await app.RunAsync();


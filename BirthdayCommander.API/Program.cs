using System.Data;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Interfaces.Handlers;
using BirthdayCommander.Handlers;
using BirthdayCommander.Infrastructure.Services;
using BirthdayCommander.Infrastructure.Data;
using BirthdayCommander.Infrastructure.Data.Migrations;
using BirthdayCommander.Infrastructure.Services.BackgroundServices;
using FluentMigrator.Runner;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Persistance 
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(CreateEmployeesTable).Assembly).For.All())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

// Services
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IMessageParser, MessageParser>();
builder.Services.AddScoped<IDirectMessageHandler, DirectMessageHandler>();

builder.Services.AddHttpClient<IMattermostService, MattermostService>(client =>
{
    var mattermostServerUrl = builder.Configuration["Mattermost:ServerUrl"] 
                              ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");
    var mattermostBotToken = builder.Configuration["Mattermost:BotToken"];
    var mattermostWebhookSecret = builder.Configuration["Mattermost:WebhookSecret"];
    
    client.BaseAddress = new Uri(mattermostServerUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {mattermostBotToken}");
    client.DefaultRequestHeaders.Add("X-Mattermost-Webhook-Secret", mattermostWebhookSecret);
});

// Hosted Services
builder.Services.AddHostedService<MattermostBotService>();
// TODO: add BirthdayCheckService
    
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        migrationRunner.MigrateUp();
        Console.WriteLine("Migrations went successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Fatal, {0}", ex);
        throw;
    }
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

app.MapGet("/", () => "Hello World!");

await app.RunAsync();


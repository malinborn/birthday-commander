using System.Data;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Interfaces.Handlers;
using BirthdayCommander.Infrastructure.Services;
using BirthdayCommander.Infrastructure.Data;
using BirthdayCommander.Infrastructure.Services.BackgroundServices;
using FluentMigrator.Runner;

var builder = WebApplication.CreateBuilder(args);

// Persistance 
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.All())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

// Services
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IMessageParser, MessageParser>();
builder.Services.AddScoped<IDirectMessageHandler>(); // TODO добавить Message Handler

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

var migrationRunner = app.Services.GetRequiredKeyedService<IMigrationRunner>(connectionString);
migrationRunner.MigrateUp();

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


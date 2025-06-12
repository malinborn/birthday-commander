using FluentMigrator.Runner;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString("str")
        .ScanIn(typeof(Program).Assembly).For.All())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

await app.RunAsync();


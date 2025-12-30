using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Services;
using NotificationService.Workers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IEmailSenderService, EmailSenderService>();
        services.AddHostedService<PasswordResetNotificationProcessor>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        var logLevel = context.Configuration.GetValue<string>("Logging:LogLevel:Default");
        logging.SetMinimumLevel(
            Enum.TryParse<LogLevel>(logLevel, out var level) ? level : LogLevel.Information);
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("==============================================");
logger.LogInformation("NotificationService Starting...");
logger.LogInformation("==============================================");

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    logger.LogInformation("NotificationService Stopped");
}

using System.Text.Json;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Contracts;
using NotificationService.Services;

namespace NotificationService.Workers;

public class PasswordResetNotificationProcessor : BackgroundService
{
    private readonly ILogger<PasswordResetNotificationProcessor> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailSenderService _emailSenderService;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    public PasswordResetNotificationProcessor(
        ILogger<PasswordResetNotificationProcessor> logger,
        IConfiguration configuration,
        IEmailSenderService emailSenderService)
    {
        _logger = logger;
        _configuration = configuration;
        _emailSenderService = emailSenderService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connectionString = _configuration["ServiceBus:PasswordResetConString"]
                ?? throw new InvalidOperationException("ServiceBus:PasswordResetConString not configured");
            var queueName = _configuration["ServiceBus:PasswordResetQueueName"]
                ?? throw new InvalidOperationException("ServiceBus:PasswordResetQueueName not configured");

            _logger.LogInformation("Starting Service Bus Consumer Service");
            _logger.LogDebug("Queue configured: {QueueName}", queueName);

            _client = new ServiceBusClient(connectionString);
            _processor = _client.CreateProcessor(queueName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 10,
                AutoCompleteMessages = false,
                PrefetchCount = 10,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });

            _processor.ProcessMessageAsync += ProcessMessageHandler;
            _processor.ProcessErrorAsync += ProcessErrorHandler;

            await _processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Service Bus Consumer Service started successfully and listening");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Service Bus Consumer Service");
            throw;
        }
    }

    private async Task ProcessMessageHandler(ProcessMessageEventArgs args)
    {
        try
        {
            var messageBody = args.Message.Body.ToString();
            
            var message = JsonSerializer.Deserialize<PasswordResetMessage>(messageBody) 
                          ?? throw new JsonException("Message deserialized to null");

            await _emailSenderService.SendPasswordResetEmailAsync(message, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid message format");
            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: "InvalidMessageFormat",
                deadLetterErrorDescription: ex.Message,
                args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing password reset message");
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus error: {ErrorSource} - {EntityPath}", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
            _processor = null;
        }
        
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        await base.StopAsync(cancellationToken);
    }
}
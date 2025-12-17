using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace NotificationService.Services;

public class ServiceBusConsumerService : BackgroundService
{
    private readonly ILogger<ServiceBusConsumerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailSenderService _emailSenderService;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    public ServiceBusConsumerService(
        ILogger<ServiceBusConsumerService> logger,
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
            var connectionString = _configuration["ServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("ServiceBus:ConnectionString not configured");
            var queueName = _configuration["ServiceBus:QueueName"]
                ?? throw new InvalidOperationException("ServiceBus:QueueName not configured");

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
        var messageId = args.Message.MessageId;
        try
        {
            var messageBody = args.Message.Body.ToString();
            
            _logger.LogDebug("Received message {MessageId}", messageId);

            NotificationMessage? message = null;
            try
            {
                message = JsonSerializer.Deserialize<NotificationMessage>(messageBody);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse message as NotificationMessage, treating as plain text");
                message = new NotificationMessage
                {
                    Subject = "Service Bus Notification",
                    Body = messageBody,
                    Recipient = _configuration["Notification:DefaultRecipient"] ?? "recipient@example.com"
                };
            }

            if (message != null)
            {
                await _emailSenderService.SendEmailAsync(
                    message.Recipient,
                    message.Subject,
                    message.Body,
                    args.CancellationToken);

                _logger.LogDebug("Email queued for {Recipient}", message.Recipient);
            }

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            _logger.LogInformation("Message {MessageId} completed successfully", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}. Delivery count: {DeliveryCount}", 
                messageId, args.Message.DeliveryCount);
            
            // Move to dead letter queue after max retries
            if (args.Message.DeliveryCount >= 3)
            {
                _logger.LogWarning("Moving message {MessageId} to dead letter queue after {DeliveryCount} attempts", 
                    messageId, args.Message.DeliveryCount);
                await args.DeadLetterMessageAsync(args.Message, 
                    deadLetterReason: "MaxDeliveryAttemptsExceeded",
                    deadLetterErrorDescription: ex.Message,
                    cancellationToken: args.CancellationToken);
            }
            else
            {
                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
            }
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Error in Service Bus Consumer. Source: {ErrorSource}, Entity: {EntityPath}",
            args.ErrorSource,
            args.EntityPath);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Service Bus Consumer Service");

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Service Bus Consumer Service stopped");
    }
}

public class NotificationMessage
{
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

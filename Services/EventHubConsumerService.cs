using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace NotificationService.Services;

public class EventHubConsumerService : BackgroundService
{
    private readonly ILogger<EventHubConsumerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailSenderService _emailSenderService;
    private EventProcessorClient? _processor;

    public EventHubConsumerService(
        ILogger<EventHubConsumerService> logger,
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
            // Get configuration
            var eventHubNamespace = _configuration["EventHub:FullyQualifiedNamespace"]
                ?? throw new InvalidOperationException("EventHub:FullyQualifiedNamespace not configured");
            var eventHubName = _configuration["EventHub:EventHubName"]
                ?? throw new InvalidOperationException("EventHub:EventHubName not configured");
            var consumerGroup = _configuration["EventHub:ConsumerGroup"] ?? "$Default";
            var blobConnectionString = _configuration["BlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException("BlobStorage:ConnectionString not configured");
            var blobContainerName = _configuration["BlobStorage:ContainerName"] ?? "eventhub-checkpoints";

            _logger.LogInformation("Starting Event Hub Consumer Service");
            _logger.LogInformation("Event Hub: {EventHub}, Namespace: {Namespace}", eventHubName, eventHubNamespace);

            // Create a blob container client for checkpointing
            var storageClient = new BlobContainerClient(blobConnectionString, blobContainerName);
            await storageClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

            // Create the event processor client
            _processor = new EventProcessorClient(
                storageClient,
                consumerGroup,
                eventHubNamespace,
                eventHubName);

            // Register handlers for processing events and handling errors
            _processor.ProcessEventAsync += ProcessEventHandler;
            _processor.ProcessErrorAsync += ProcessErrorHandler;

            // Start processing
            await _processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Event Hub Consumer Service started successfully. Listening for events...");

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Event Hub Consumer Service");
            throw;
        }
    }

    private async Task ProcessEventHandler(ProcessEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.CancellationToken.IsCancellationRequested)
                return;

            // Get the event data
            var eventBody = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());
            
            _logger.LogInformation("Received event from partition {PartitionId}: {EventBody}",
                eventArgs.Partition.PartitionId, eventBody);

            // Parse the event (expecting JSON format)
            NotificationMessage? message = null;
            try
            {
                message = JsonSerializer.Deserialize<NotificationMessage>(eventBody);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse event as NotificationMessage, treating as plain text");
                message = new NotificationMessage
                {
                    Subject = "Event Hub Notification",
                    Body = eventBody,
                    Recipient = _configuration["Notification:DefaultRecipient"] ?? "recipient@example.com"
                };
            }

            if (message != null)
            {
                // Send email notification
                await _emailSenderService.SendEmailAsync(
                    message.Recipient,
                    message.Subject,
                    message.Body,
                    eventArgs.CancellationToken);

                _logger.LogInformation("Email notification sent successfully to {Recipient}", message.Recipient);
            }

            // Update checkpoint in the blob storage
            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event from partition {PartitionId}",
                eventArgs.Partition.PartitionId);
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
    {
        _logger.LogError(eventArgs.Exception,
            "Error in Event Hub Consumer. Partition: {PartitionId}, Operation: {Operation}",
            eventArgs.PartitionId ?? "N/A",
            eventArgs.Operation ?? "N/A");

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Event Hub Consumer Service");

        if (_processor != null)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken);
                _processor.ProcessEventAsync -= ProcessEventHandler;
                _processor.ProcessErrorAsync -= ProcessErrorHandler;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Event Hub processor");
            }
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Event Hub Consumer Service stopped");
    }
}

public class NotificationMessage
{
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

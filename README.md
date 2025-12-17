# NotificationService

A .NET 10 console application that continuously listens for messages from Azure Event Hub and sends email notifications using Azure Communication Services.

## Overview

This service is designed to run as an always-on background service that:
- Connects to Azure Event Hub and listens for incoming events
- Processes each event and extracts notification information
- Sends email notifications using Azure Communication Services Email SDK
- Maintains checkpoints using Azure Blob Storage for reliable event processing

## Architecture

- **EventHubConsumerService**: Background service that continuously processes Event Hub messages
- **EmailSenderService**: Handles email sending through Azure Communication Services
- **Azure Blob Storage**: Checkpoint management for reliable message processing

## Prerequisites

Before running the application, ensure you have:

1. **.NET 10 SDK** installed on your machine
2. **Azure Event Hub** namespace and event hub created
3. **Azure Storage Account** for checkpoint management
4. **Azure Communication Services** resource with Email service configured
5. A **verified domain** connected to Azure Communication Services

## Configuration

### 1. Update appsettings.json

Copy the template and fill in your Azure resource details:

```json
{
  "EventHub": {
    "FullyQualifiedNamespace": "YOUR_EVENTHUB_NAMESPACE.servicebus.windows.net",
    "EventHubName": "YOUR_EVENTHUB_NAME",
    "ConsumerGroup": "$Default"
  },
  "BlobStorage": {
    "ConnectionString": "YOUR_BLOB_STORAGE_CONNECTION_STRING",
    "ContainerName": "eventhub-checkpoints"
  },
  "Email": {
    "ConnectionString": "YOUR_EMAIL_COMMUNICATION_SERVICE_CONNECTION_STRING",
    "SenderAddress": "donotreply@yourdomain.com"
  },
  "Notification": {
    "DefaultRecipient": "recipient@example.com"
  }
}
```

### 2. Get Azure Resource Connection Strings

#### Event Hub Connection Details:
1. Go to Azure Portal → Event Hubs Namespace
2. Note the **namespace** (e.g., `mynamespace.servicebus.windows.net`)
3. Note your **event hub name**

#### Blob Storage Connection String:
1. Go to Azure Portal → Storage Account
2. Under "Security + networking" → "Access keys"
3. Copy the connection string

#### Azure Communication Services Email Connection String:
1. Go to Azure Portal → Communication Services
2. Under "Settings" → "Keys"
3. Copy the connection string
4. Make sure you have a verified domain connected to the resource

### 3. Environment Variables (Optional)

You can also use environment variables instead of appsettings.json:

```bash
export EventHub__FullyQualifiedNamespace="mynamespace.servicebus.windows.net"
export EventHub__EventHubName="myeventhub"
export BlobStorage__ConnectionString="DefaultEndpointsProtocol=https;..."
export Email__ConnectionString="endpoint=https://...;accesskey=..."
export Email__SenderAddress="donotreply@yourdomain.com"
```

## Message Format

The service expects Event Hub messages in JSON format:

```json
{
  "Recipient": "user@example.com",
  "Subject": "Notification Subject",
  "Body": "This is the email body content"
}
```

If a message is not in JSON format or cannot be parsed, it will be treated as plain text and sent to the default recipient configured in `appsettings.json`.

## Running the Application

### Local Development

1. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

3. **Stop the application:**
   Press `Ctrl+C`

### Production Deployment

For production deployment, you can:

1. **Publish the application:**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Run as a service:**
   - **Windows**: Use Windows Service or Task Scheduler
   - **Linux**: Use systemd service
   - **Docker**: Create a Dockerfile and run as a container
   - **Azure**: Deploy to Azure App Service or Azure Container Apps

## Running as a Linux Systemd Service

Create a service file `/etc/systemd/system/notificationservice.service`:

```ini
[Unit]
Description=Notification Service
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/notificationservice
ExecStart=/usr/bin/dotnet /opt/notificationservice/NotificationService.dll
Restart=always
RestartSec=10
SyslogIdentifier=notificationservice
User=www-data
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable notificationservice
sudo systemctl start notificationservice
sudo systemctl status notificationservice
```

## Docker Deployment

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "NotificationService.dll"]
```

Build and run:
```bash
docker build -t notificationservice .
docker run -d --name notificationservice \
  -e EventHub__FullyQualifiedNamespace="..." \
  -e EventHub__EventHubName="..." \
  -e BlobStorage__ConnectionString="..." \
  -e Email__ConnectionString="..." \
  -e Email__SenderAddress="..." \
  notificationservice
```

## Monitoring and Logging

The application uses structured logging with the following log levels:
- **Information**: Normal operation logs (events received, emails sent)
- **Warning**: Non-critical issues (message parsing failures)
- **Error**: Processing errors (email send failures, Event Hub errors)
- **Critical**: Fatal application errors

View logs:
```bash
# Local development
dotnet run

# Systemd service
sudo journalctl -u notificationservice -f

# Docker
docker logs -f notificationservice
```

## Error Handling

The service includes comprehensive error handling:
- **Event processing errors**: Logged but don't stop the service
- **Email send failures**: Logged with detailed error information
- **Connection issues**: Automatic retry with exponential backoff
- **Checkpointing**: Ensures messages are not lost on restart

## Security Best Practices

1. **Never commit connection strings** to source control
2. **Use Azure Key Vault** for production secrets
3. **Enable Azure Managed Identity** when running in Azure
4. **Use least privilege** for Azure resource access
5. **Rotate keys regularly** for all Azure services

## Troubleshooting

### Service won't start
- Check all configuration values are correct
- Verify Azure resources are accessible
- Check firewall rules allow outbound connections
- Review logs for specific error messages

### Events not being received
- Verify Event Hub is receiving messages (check Azure Portal metrics)
- Check consumer group name matches
- Ensure checkpoint container exists in Blob Storage
- Verify Event Hub namespace URL is correct

### Emails not being sent
- Verify Email Communication Service connection string
- Check sender address is from a verified domain
- Review Azure Communication Services quotas/limits
- Check recipient email addresses are valid

## Performance Considerations

- The service processes events in parallel across partitions
- Checkpointing ensures at-least-once delivery
- Email sending is non-blocking and asynchronous
- Consider scaling Event Hub partitions for high throughput

## License

MIT License

## Support

For issues and questions, please create an issue in the repository.

# NotificationService

A .NET 10 console application that continuously listens for messages from Azure Service Bus Queue and sends email notifications using Azure Communication Services.

## Overview

This service is designed to run as an always-on background service that:
- Connects to Azure Service Bus Queue and listens for incoming messages
- Processes each message and extracts notification information
- Sends email notifications using Azure Communication Services Email SDK

## Architecture

- **ServiceBusConsumerService**: Background service that continuously processes Service Bus Queue messages
- **EmailSenderService**: Handles email sending through Azure Communication Services

## Prerequisites

Before running the application, ensure you have:

1. **.NET 10 SDK** installed on your machine
2. **Azure Service Bus** namespace and queue created
3. **Azure Communication Services** resource with Email service configured
4. A **verified domain** connected to Azure Communication Services

## Configuration

### 1. Update appsettings.json

Copy the template and fill in your Azure resource details:

```json
{
  "ServiceBus": {
    "ConnectionString": "YOUR_SERVICE_BUS_CONNECTION_STRING",
    "QueueName": "notifications"
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

#### Service Bus Connection String:
1. Go to Azure Portal → Service Bus Namespace
2. Under "Settings" → "Shared access policies"
3. Copy the connection string
4. Create a queue named "notifications" (or update QueueName in config)

#### Azure Communication Services Email Connection String:
1. Go to Azure Portal → Communication Services
2. Under "Settings" → "Keys"
3. Copy the connection string
4. Make sure you have a verified domain connected to the resource

### 3. Environment Variables (Optional)

You can also use environment variables instead of appsettings.json:

```bash
export ServiceBus__ConnectionString="Endpoint=sb://..."
export ServiceBus__QueueName="notifications"
export Email__ConnectionString="endpoint=https://...;accesskey=..."
export Email__SenderAddress="donotreply@yourdomain.com"
```

## Message Format

The service expects Service Bus messages in JSON format:

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

### Azure App Service Deployment

#### Option 1: GitHub Actions (Recommended)
Use the included CI/CD pipeline:

1. **Set up Azure credentials** in GitHub repository secrets
2. **Configure repository variables:**
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `AZURE_WEBAPP_NAME`
3. **Run the workflow** from Actions tab
4. **Configure App Settings** in Azure Portal (see below)

#### Option 2: Manual Deployment
Deploy directly to Azure App Service:

1. **Publish the application:**
   ```bash
   dotnet publish -c Release
   ```

2. **Deploy to Azure:**
   ```bash
   az webapp up \
     --name your-notification-service \
     --resource-group your-resource-group \
     --runtime "DOTNETCORE:10.0" \
     --sku B1
   ```

3. **Configure App Settings in Azure Portal:**
   - Navigate to your App Service → Configuration → Application settings
   - Add the following settings:
     - `ServiceBus__ConnectionString`
     - `ServiceBus__QueueName`
     - `Email__ConnectionString`
     - `Email__SenderAddress`
     - `Notification__DefaultRecipient`
     - `Logging__LogLevel__Default` (optional, default: Information)

4. **Enable Always On** (under Configuration → General settings) to keep the service running continuously
5. **Recommended SKU**: B2 or higher for production workloads

## Monitoring and Logging

The application uses structured logging with the following log levels:
- **Information**: Normal operation logs (messages received, emails sent)
- **Warning**: Non-critical issues (message parsing failures)
- **Error**: Processing errors (email send failures, Service Bus errors)
- **Critical**: Fatal application errors

View logs:
```bash
# Local development
dotnet run

# Azure App Service
az webapp log tail --name your-notification-service --resource-group your-resource-group
```

## Error Handling

The service includes comprehensive error handling:
- **Message processing errors**: Logged but don't stop the service; messages are abandoned for retry
- **Email send failures**: Logged with detailed error information
- **Connection issues**: Automatic retry via Service Bus built-in mechanisms

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

### Messages not being received
- Verify Service Bus queue is receiving messages (check Azure Portal metrics)
- Check queue name matches configuration
- Verify Service Bus connection string is correct
- Ensure queue exists in the namespace

### Emails not being sent
- Verify Email Communication Service connection string
- Check sender address is from a verified domain
- Review Azure Communication Services quotas/limits
- Check recipient email addresses are valid

## Performance Considerations

- The service processes messages sequentially by default
- Failed messages are automatically retried by Service Bus
- Email sending is non-blocking and asynchronous
- Scale out by deploying multiple instances for higher throughput

## License

MIT License

## Support

For issues and questions, please create an issue in the repository.

# Quick Start Guide

## Step 1: Configure Your Azure Resources

Before running the application, you need to set up the following Azure resources:

### 1.1 Create Azure Event Hub

1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new **Event Hubs Namespace**
3. Create an **Event Hub** inside the namespace
4. Note down:
   - Namespace name (e.g., `mynamespace.servicebus.windows.net`)
   - Event Hub name

### 1.2 Create Azure Storage Account

1. Create a new **Storage Account**
2. Go to **Access keys** under Security + networking
3. Copy the **Connection string**

### 1.3 Create Azure Communication Services

1. Create a new **Communication Services** resource
2. Go to **Settings** → **Keys**
3. Copy the **Connection string**
4. Set up **Email** service:
   - Go to **Email** → **Provision domains**
   - Add and verify a domain (or use Azure subdomain)
   - Note the sender address (e.g., `donotreply@yourdomain.com`)

## Step 2: Configure the Application

1. Open `appsettings.Development.json`
2. Replace the placeholder values:

```json
{
  "EventHub": {
    "FullyQualifiedNamespace": "mynamespace.servicebus.windows.net",
    "EventHubName": "myeventhub",
    "ConsumerGroup": "$Default"
  },
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "eventhub-checkpoints"
  },
  "Email": {
    "ConnectionString": "endpoint=https://....communication.azure.com/;accesskey=...",
    "SenderAddress": "donotreply@yourdomain.com"
  },
  "Notification": {
    "DefaultRecipient": "your-email@example.com"
  }
}
```

## Step 3: Run the Application

```bash
cd /Users/reniciuspagotto/development/g4u/notification-service
dotnet run
```

You should see output like:
```
==============================================
NotificationService Starting...
==============================================
info: NotificationService.Services.EmailSenderService[0]
      Email Sender Service initialized with sender: donotreply@yourdomain.com
info: NotificationService.Services.EventHubConsumerService[0]
      Starting Event Hub Consumer Service
info: NotificationService.Services.EventHubConsumerService[0]
      Event Hub Consumer Service started successfully. Listening for events...
```

## Step 4: Send a Test Message

### Option A: Using Azure Portal

1. Go to your Event Hub in Azure Portal
2. Click **Generate Data** or use **Event Hubs Explorer**
3. Send a message with this JSON:

```json
{
  "Recipient": "your-email@example.com",
  "Subject": "Test Notification",
  "Body": "This is a test message from Event Hub!"
}
```

### Option B: Using Azure CLI

```bash
az eventhubs eventhub send \
  --resource-group your-resource-group \
  --namespace-name your-namespace \
  --name your-eventhub \
  --body '{
    "Recipient": "your-email@example.com",
    "Subject": "Test from CLI",
    "Body": "Testing the notification service!"
  }'
```

## Step 5: Verify Email Delivery

1. Check the NotificationService console logs
2. You should see:
   ```
   info: Received event from partition 0: {"Recipient":"...","Subject":"...","Body":"..."}
   info: Email notification sent successfully to your-email@example.com
   ```
3. Check your email inbox for the notification

## Troubleshooting

### "Connection refused" or "Unable to connect"
- Check firewall rules allow outbound connections
- Verify network connectivity to Azure services

### "Authentication failed"
- Verify connection strings are correct
- Check Azure resource keys haven't been rotated

### "Email not received"
- Check spam/junk folder
- Verify sender domain is verified in Azure Communication Services
- Check Azure Communication Services quotas

### "No events received"
- Verify Event Hub is receiving messages (check Azure Portal metrics)
- Ensure checkpoint container exists in Blob Storage
- Check consumer group name matches

## Next Steps

- Deploy to production (see README.md for deployment options)
- Set up monitoring and alerts
- Configure auto-scaling based on Event Hub metrics
- Implement custom message templates
- Add retry policies for email failures

## Support

For more detailed information, see the main [README.md](README.md) file.

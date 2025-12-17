# 🚀 Production Deployment Checklist

## Pre-Deployment Verification

### Code Quality
- ✅ Project builds successfully with Release configuration
- ✅ No compiler warnings or errors
- ✅ All dependencies up to date
- ✅ Nullable reference types enabled
- ✅ Code follows C# best practices

### Performance Optimizations Applied
- ✅ Service Bus concurrent processing (10 messages)
- ✅ Message prefetching enabled (10 messages)
- ✅ Server GC for high throughput
- ✅ ReadyToRun compilation for fast startup
- ✅ Tiered compilation enabled
- ✅ Email retry policy with exponential backoff

### Security
- ✅ No secrets in source code
- ✅ Connection strings via environment variables
- ✅ HTML encoding for email content (XSS protection)
- ✅ .gitignore properly configured
- ✅ appsettings.json contains only templates

### Reliability
- ✅ Dead letter queue handling after 3 retries
- ✅ Automatic lock renewal (5 minutes)
- ✅ Transient error retry (email service)
- ✅ Graceful shutdown handling
- ✅ Proper resource disposal

### Observability
- ✅ Structured logging configured
- ✅ Message ID tracking
- ✅ Delivery count monitoring
- ✅ Error logging with context
- ✅ Health checks enabled

---

## Azure Resources Required

### 1. Service Bus Namespace
- [ ] Namespace created
- [ ] Queue named "notifications" created
- [ ] Connection string obtained
- [ ] Consider Premium tier for production (better performance)

### 2. Communication Services
- [ ] Resource created
- [ ] Email service enabled
- [ ] Domain verified
- [ ] Sender address configured
- [ ] Connection string obtained

### 3. App Service
- [ ] App Service created
- [ ] Runtime: .NET 10.0 (Linux)
- [ ] SKU: B2 or higher recommended
- [ ] Always On: Enabled
- [ ] Platform: 64-bit

---

## GitHub Setup

### Repository Secrets (Federated Identity)
Set these in: Settings → Secrets and variables → Actions → Variables

#### Variables:
- [ ] `AZURE_CLIENT_ID` - Service principal client ID
- [ ] `AZURE_TENANT_ID` - Azure tenant ID
- [ ] `AZURE_SUBSCRIPTION_ID` - Azure subscription ID
- [ ] `AZURE_WEBAPP_NAME` - App Service name

#### Environments:
- [ ] "Stage" environment configured
- [ ] "Production" environment configured
- [ ] Protection rules set for Production (optional)

---

## Azure App Service Configuration

### Application Settings
Navigate to: App Service → Configuration → Application settings

#### Required Settings:
```bash
ServiceBus__ConnectionString=Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=...
ServiceBus__QueueName=notifications
Email__ConnectionString=endpoint=https://your-comm-service.communication.azure.com/;accesskey=...
Email__SenderAddress=donotreply@yourdomain.com
Notification__DefaultRecipient=fallback@example.com
```

#### Optional Settings:
```bash
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft=Warning
Logging__LogLevel__Azure.Messaging.ServiceBus=Warning
ASPNETCORE_ENVIRONMENT=Production
```

### General Settings
- [ ] **Always On**: ON
- [ ] **Platform**: Linux
- [ ] **Platform Architecture**: 64 Bit
- [ ] **Web sockets**: OFF (not needed)
- [ ] **ARR affinity**: OFF (stateless service)

### Scale Out (Production)
- [ ] Manual scale or auto-scale configured
- [ ] Recommended: 2+ instances for high availability

---

## Deployment Steps

### Option 1: GitHub Actions (Recommended)
1. [ ] Push code to GitHub repository
2. [ ] Navigate to Actions tab
3. [ ] Select "Deploy Notification Service (.NET) to Azure"
4. [ ] Click "Run workflow"
5. [ ] Select environment (Stage/Production)
6. [ ] Monitor deployment progress
7. [ ] Verify logs in Azure Portal

### Option 2: Manual Deployment
```bash
# Build and publish
dotnet publish -c Release -o ./publish

# Zip the output
cd ./publish && zip -r ../release.zip ./*

# Deploy using Azure CLI
az webapp deploy \
  --resource-group <your-rg> \
  --name <your-app-name> \
  --src-path ./release.zip \
  --type zip
```

---

## Post-Deployment Verification

### 1. Service Health
```bash
# View real-time logs
az webapp log tail \
  --name <your-app-name> \
  --resource-group <your-rg>

# Check if app is running
az webapp show \
  --name <your-app-name> \
  --resource-group <your-rg> \
  --query "state"
```

Expected log entries:
```
NotificationService Starting...
Starting Service Bus Consumer Service
Queue: notifications
Service Bus Consumer Service started successfully. Listening for messages...
```

### 2. Functionality Test
Send a test message to the Service Bus queue:

```json
{
  "Recipient": "test@example.com",
  "Subject": "Test Notification",
  "Body": "This is a test message"
}
```

Expected behavior:
1. Message received log entry
2. Email queued successfully log entry
3. Message completed successfully log entry
4. Email received by recipient

### 3. Error Handling Test
Send a message with invalid recipient:

```json
{
  "Recipient": "invalid-email",
  "Subject": "Test",
  "Body": "Test"
}
```

Expected behavior:
1. Error logged with delivery count
2. Message retried automatically
3. After 3 attempts, moved to dead letter queue

### 4. Performance Check
- [ ] Monitor App Service metrics (CPU, Memory)
- [ ] Monitor Service Bus metrics (messages processed)
- [ ] Check application logs for errors
- [ ] Verify email delivery rate

---

## Monitoring Setup (Optional but Recommended)

### Application Insights
```bash
# Create Application Insights
az monitor app-insights component create \
  --app <app-insights-name> \
  --location <location> \
  --resource-group <your-rg>

# Link to App Service
az webapp config appsettings set \
  --name <your-app-name> \
  --resource-group <your-rg> \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="..."
```

### Alerts
Set up alerts for:
- [ ] High CPU usage (>80%)
- [ ] High memory usage (>80%)
- [ ] Dead letter queue messages
- [ ] Email send failures

---

## Performance Benchmarks

### Expected Metrics (B2 instance):
- **Throughput**: 100-200 messages/minute
- **CPU Usage**: 20-40% under normal load
- **Memory Usage**: 200-400 MB
- **Startup Time**: <2 seconds
- **Response to message**: <500ms average

### Scaling Thresholds:
- CPU > 70%: Scale out to +1 instance
- CPU < 30%: Scale in to -1 instance (min 1)

---

## Troubleshooting

### Service not starting
1. Check Application Settings are correct
2. Verify connection strings
3. Check App Service logs
4. Verify .NET 10.0 runtime is available

### Messages not processing
1. Verify Service Bus queue exists
2. Check connection string permissions
3. Review Service Bus metrics in Azure Portal
4. Check for dead letter messages

### Emails not sending
1. Verify email service connection string
2. Check sender address is from verified domain
3. Review Azure Communication Services quotas
4. Check recipient address validity

### High resource usage
1. Check message volume
2. Review concurrent processing settings
3. Consider scaling up (vertical) or out (horizontal)
4. Check for memory leaks in logs

---

## Rollback Plan

If deployment fails:

### GitHub Actions
1. Navigate to previous successful workflow run
2. Click "Re-run all jobs"
3. Monitor deployment

### Manual
1. Deploy previous working version from backup
2. Verify services are restored
3. Investigate failure in non-production environment

---

## Success Criteria

- [ ] Application starts without errors
- [ ] Consumes messages from Service Bus queue
- [ ] Sends emails successfully
- [ ] Failed messages move to dead letter queue
- [ ] Logs are visible and structured
- [ ] CPU < 50%, Memory < 500MB under normal load
- [ ] No critical errors in first 24 hours

---

## 🎉 Ready for Production!

Once all items are checked, your application is production-ready!

**Estimated deployment time**: 15-20 minutes
**Pipeline execution time**: 2-3 minutes
**Expected uptime**: 99.9%+

For support and monitoring, refer to:
- [PERFORMANCE_IMPROVEMENTS.md](PERFORMANCE_IMPROVEMENTS.md)
- [README.md](README.md)
- Azure Portal → Monitor → Logs

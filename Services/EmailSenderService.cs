using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace NotificationService.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly ILogger<EmailSenderService> _logger;
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;
    private readonly AsyncRetryPolicy _retryPolicy;

    public EmailSenderService(
        ILogger<EmailSenderService> logger,
        IConfiguration configuration)
    {
        _logger = logger;

        var connectionString = configuration["Email:ConnectionString"]
            ?? throw new InvalidOperationException("Email:ConnectionString not configured");
        
        _senderAddress = configuration["Email:SenderAddress"]
            ?? throw new InvalidOperationException("Email:SenderAddress not configured");

        _emailClient = new EmailClient(connectionString);
        
        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<RequestFailedException>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Email send attempt {RetryCount} failed. Retrying in {RetryDelay}s",
                        retryCount, timeSpan.TotalSeconds);
                });
        
        _logger.LogInformation("Email Sender Service initialized with sender: {SenderAddress}", _senderAddress);
    }

    public async Task SendEmailAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                _logger.LogInformation("Sending email to {Recipient} with subject: {Subject}", recipient, subject);

                var emailContent = new EmailContent(subject)
                {
                    PlainText = body,
                    Html = $"<html><body><p>{System.Net.WebUtility.HtmlEncode(body)}</p></body></html>"
                };

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    recipientAddress: recipient,
                    content: emailContent);

                EmailSendOperation emailSendOperation = await _emailClient.SendAsync(
                    WaitUntil.Started,
                    emailMessage,
                    cancellationToken);

                _logger.LogInformation("Email queued successfully. Message ID: {MessageId}", emailSendOperation.Id);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient}. Error code: {ErrorCode}",
                    recipient, ex.ErrorCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to {Recipient}", recipient);
                throw;
            }
        });
    }

    private static bool IsTransientError(RequestFailedException ex)
    {
        // Retry on throttling, timeout, and server errors
        return ex.Status == 429 || ex.Status == 503 || ex.Status == 504 || ex.Status >= 500;
    }
}

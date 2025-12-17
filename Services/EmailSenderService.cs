using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NotificationService.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly ILogger<EmailSenderService> _logger;
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;

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
        
        _logger.LogInformation("Email Sender Service initialized with sender: {SenderAddress}", _senderAddress);
    }

    public async Task SendEmailAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending email to {Recipient} with subject: {Subject}", recipient, subject);

            var emailContent = new EmailContent(subject)
            {
                PlainText = body,
                Html = $"<html><body><p>{body}</p></body></html>"
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

            // Optional: Wait for the email to be sent (you can remove this if you want fire-and-forget)
            // var response = await emailSendOperation.WaitForCompletionAsync(cancellationToken);
            // _logger.LogInformation("Email sent with status: {Status}", response.Value.Status);
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
    }
}

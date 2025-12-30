using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Contracts;
using NotificationService.Templates;

namespace NotificationService.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly ILogger<EmailSenderService> _logger;
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;

    public EmailSenderService(ILogger<EmailSenderService> logger, IConfiguration configuration)
    {
        _logger = logger;

        var connectionString = configuration["Email:ConnectionString"]
            ?? throw new InvalidOperationException("Email:ConnectionString not configured");
        
        _senderAddress = configuration["Email:SenderAddress"]
            ?? throw new InvalidOperationException("Email:SenderAddress not configured");

        _emailClient = new EmailClient(connectionString);
        
        _logger.LogInformation("Email Sender Service initialized");
    }

    public async Task SendPasswordResetEmailAsync(PasswordResetMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = PasswordResetTemplate.BuildTemplate(message.ResetCode);

            var emailContent = new EmailContent("Your password reset code") { Html = body };
            var emailMessage = new EmailMessage(_senderAddress, message.Recipient, emailContent);

            EmailSendOperation operation = await _emailClient.SendAsync(WaitUntil.Started, emailMessage, cancellationToken);

            _logger.LogInformation("Email queued successfully. Message ID: {MessageId}", operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email");
            throw;
        }
    }
}

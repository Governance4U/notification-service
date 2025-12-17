namespace NotificationService.Services;

public interface IEmailSenderService
{
    Task SendEmailAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

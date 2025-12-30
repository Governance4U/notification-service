using NotificationService.Contracts;

namespace NotificationService.Services;

public interface IEmailSenderService
{
    Task SendPasswordResetEmailAsync(PasswordResetMessage message, CancellationToken cancellationToken = default);
}

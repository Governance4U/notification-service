namespace NotificationService.Templates;

public sealed class PasswordResetTemplate
{
    public static string BuildTemplate(int resetCode)
    {
        return
            $"<html><body><p>Hi,<br>Here is your code to reset your password: <strong>{resetCode}</strong></p></body></html>";
    }
}
namespace NotificationService.Contracts;

public sealed class PasswordResetMessage
{
    public required string Recipient { get; init; }
    public required int ResetCode { get; init; }
}
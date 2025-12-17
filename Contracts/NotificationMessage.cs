namespace NotificationService.Contracts;

public class NotificationMessage
{
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
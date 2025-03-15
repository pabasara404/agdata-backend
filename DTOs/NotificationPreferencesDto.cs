namespace AgData.DTOs
{
    public class NotificationPreferencesDto
        {
            public bool EmailEnabled { get; set; } = true;
            public bool SlackEnabled { get; set; } = false;
            public string? SlackWebhookUrl { get; set; }
        }
}

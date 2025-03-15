namespace AgData.Models
{
    public class NotificationPreferences
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public bool EmailEnabled { get; set; } = true;
        public bool SlackEnabled { get; set; } = false;
        public string? SlackWebhookUrl { get; set; }
    }
}
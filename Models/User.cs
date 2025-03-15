namespace AgData.Models
{
    public class User
    {
        public int? Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        public NotificationPreferences? NotificationPreferences { get; set; }
    }
}
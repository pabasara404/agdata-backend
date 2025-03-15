namespace AgData.DTOs
{
    public class CreateUserDto
        {
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public NotificationPreferencesDto? NotificationPreferences { get; set; }
        }
}

namespace AgData.DTOs
{
     public class UpdateUserDto
        {
            public string Email { get; set; } = string.Empty;
            public NotificationPreferencesDto? NotificationPreferences { get; set; }
        }
}
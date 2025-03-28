namespace AgData.DTOs
{
   public class UserDto
       {
           public int? Id { get; set; }
           public string Username { get; set; } = string.Empty;
           public string Email { get; set; } = string.Empty;
           public bool IsAdmin { get; set; }
           public NotificationPreferencesDto? NotificationPreferences { get; set; }
       }
}
using System.Collections.Generic;
using System.Threading.Tasks;
using AgData.Models;

namespace AgData.Data.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByPasswordResetTokenAsync(string token);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task DeleteAsync(int id);
        Task<NotificationPreferences> CreateNotificationPreferencesAsync(NotificationPreferences preferences);
        Task<NotificationPreferences> UpdateNotificationPreferencesAsync(NotificationPreferences preferences);
        Task<NotificationPreferences?> GetNotificationPreferencesByUserIdAsync(int userId);
    }
}
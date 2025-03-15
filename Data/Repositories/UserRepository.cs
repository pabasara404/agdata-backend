using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using AgData.Data;
using AgData.Models;

namespace AgData.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<UserRepository>? _logger;

        public UserRepository(IDbConnectionFactory connectionFactory, ILogger<UserRepository>? logger = null)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var user = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT Id, Username, Email, PasswordHash, PasswordResetToken, PasswordResetTokenExpiry
                  FROM Users WHERE Id = @Id",
                new { Id = id });

            if (user != null)
            {
                user.NotificationPreferences = await GetNotificationPreferencesByUserIdAsync(id);
                _logger.LogDebug("Retrieved user with ID {UserId} and notification preferences", id);
            }

            return user;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var user = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT Id, Username, Email, PasswordHash, PasswordResetToken, PasswordResetTokenExpiry
                  FROM Users WHERE Email = @Email",
                new { Email = email });

            if (user != null && user.Id.HasValue)
            {
                user.NotificationPreferences = await GetNotificationPreferencesByUserIdAsync(user.Id.Value);
            }

            return user;
        }

        public async Task<User?> GetByPasswordResetTokenAsync(string token)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var user = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT Id, Username, Email, PasswordHash, PasswordResetToken, PasswordResetTokenExpiry
                  FROM Users WHERE PasswordResetToken = @Token AND PasswordResetTokenExpiry > @Now",
                new { Token = token, Now = DateTime.UtcNow });

            if (user != null && user.Id.HasValue)
            {
                user.NotificationPreferences = await GetNotificationPreferencesByUserIdAsync(user.Id.Value);
            }

            return user;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var users = await connection.QueryAsync<User>(
                "SELECT Id, Username, Email FROM Users");

            foreach (var user in users.Where(u => u.Id.HasValue))
            {
                user.NotificationPreferences = await GetNotificationPreferencesByUserIdAsync(user.Id!.Value);
            }

            return users;
        }

        public async Task<User> CreateAsync(User user)
        {
            _logger.LogInformation("Creating new user: {Username}, {Email}", user.Username, user.Email);

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var id = await connection.ExecuteScalarAsync<int>(
                    @"INSERT INTO Users (Username, Email, PasswordHash, PasswordResetToken, PasswordResetTokenExpiry)
                      VALUES (@Username, @Email, @PasswordHash, @PasswordResetToken, @PasswordResetTokenExpiry);
                      SELECT last_insert_rowid()",
                    user,
                    transaction);

                user.Id = id;
                _logger.LogDebug("User created with ID: {UserId}", id);

                if (user.NotificationPreferences != null)
                {
                    user.NotificationPreferences.UserId = id;
                    await CreateNotificationPreferencesAsync(user.NotificationPreferences, connection, transaction);
                    _logger.LogDebug("Created notification preferences for user {UserId}", id);
                }

                transaction.Commit();
                _logger.LogInformation("User creation transaction committed successfully");

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                transaction.Rollback();
                throw;
            }
        }

        public async Task<User> UpdateAsync(User user)
        {
            _logger.LogInformation("Updating user with ID: {UserId}", user.Id);

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await connection.ExecuteAsync(
                    @"UPDATE Users SET
                      Username = @Username,
                      Email = @Email,
                      PasswordHash = @PasswordHash,
                      PasswordResetToken = @PasswordResetToken,
                      PasswordResetTokenExpiry = @PasswordResetTokenExpiry
                      WHERE Id = @Id",
                    user,
                    transaction);

                _logger.LogDebug("Updated user basic information");

                if (user.NotificationPreferences != null && user.Id.HasValue)
                {
                    var existingPrefs = await GetNotificationPreferencesByUserIdAsync(user.Id.Value, connection, transaction);

                    if (existingPrefs == null)
                    {
                        _logger.LogDebug("No existing notification preferences found, creating new ones");
                        user.NotificationPreferences.UserId = user.Id.Value;
                        await CreateNotificationPreferencesAsync(user.NotificationPreferences, connection, transaction);
                    }
                    else
                    {
                        _logger.LogDebug("Updating existing notification preferences");
                        user.NotificationPreferences.Id = existingPrefs.Id;
                        user.NotificationPreferences.UserId = user.Id.Value;
                        await UpdateNotificationPreferencesAsync(user.NotificationPreferences, connection, transaction);
                    }
                }

                transaction.Commit();
                _logger.LogInformation("User update transaction committed successfully");

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {UserId}", user.Id);
                transaction.Rollback();
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Delete notification preferences first
                await connection.ExecuteAsync(
                    "DELETE FROM NotificationPreferences WHERE UserId = @UserId",
                    new { UserId = id },
                    transaction);

                // Then delete the user
                await connection.ExecuteAsync(
                    "DELETE FROM Users WHERE Id = @Id",
                    new { Id = id },
                    transaction);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID {UserId}", id);
                transaction.Rollback();
                throw;
            }
        }

        public async Task<NotificationPreferences> CreateNotificationPreferencesAsync(NotificationPreferences preferences)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            return await CreateNotificationPreferencesAsync(preferences, connection, null);
        }

        private async Task<NotificationPreferences> CreateNotificationPreferencesAsync(
            NotificationPreferences preferences,
            IDbConnection connection,
            IDbTransaction? transaction)
        {
            _logger.LogDebug("Creating notification preferences for user {UserId}", preferences.UserId);

            var id = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO NotificationPreferences (UserId, EmailEnabled, SlackEnabled, SlackWebhookUrl)
                  VALUES (@UserId, @EmailEnabled, @SlackEnabled, @SlackWebhookUrl);
                  SELECT last_insert_rowid()",
                preferences,
                transaction);

            preferences.Id = id;
            _logger.LogDebug("Created notification preferences with ID {PreferencesId}", id);

            return preferences;
        }

        public async Task<NotificationPreferences> UpdateNotificationPreferencesAsync(NotificationPreferences preferences)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            return await UpdateNotificationPreferencesAsync(preferences, connection, null);
        }

        private async Task<NotificationPreferences> UpdateNotificationPreferencesAsync(
            NotificationPreferences preferences,
            IDbConnection connection,
            IDbTransaction? transaction)
        {
            _logger.LogDebug("Updating notification preferences with ID {PreferencesId}", preferences.Id);

            await connection.ExecuteAsync(
                @"UPDATE NotificationPreferences SET
                  EmailEnabled = @EmailEnabled,
                  SlackEnabled = @SlackEnabled,
                  SlackWebhookUrl = @SlackWebhookUrl
                  WHERE Id = @Id",
                preferences,
                transaction);

            return preferences;
        }

        public async Task<NotificationPreferences?> GetNotificationPreferencesByUserIdAsync(int userId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            return await GetNotificationPreferencesByUserIdAsync(userId, connection, null);
        }

        private async Task<NotificationPreferences?> GetNotificationPreferencesByUserIdAsync(
            int userId,
            IDbConnection connection,
            IDbTransaction? transaction)
        {
            _logger.LogDebug("Retrieving notification preferences for user {UserId}", userId);

            var preferences = await connection.QuerySingleOrDefaultAsync<NotificationPreferences>(
                @"SELECT Id, UserId, EmailEnabled, SlackEnabled, SlackWebhookUrl
                  FROM NotificationPreferences WHERE UserId = @UserId",
                new { UserId = userId },
                transaction);

            if (preferences != null)
            {
                _logger.LogDebug("Found notification preferences with ID {PreferencesId}", preferences.Id);
            }
            else
            {
                _logger.LogDebug("No notification preferences found for user {UserId}", userId);
            }

            return preferences;
        }
    }
}
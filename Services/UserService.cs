using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Globalization;
using CsvHelper;
using FluentValidation;
using AgData.Data.Repositories;
using AgData.DTOs;
using AgData.Models;
using Microsoft.Extensions.Logging;

namespace AgData.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IValidator<CreateUserDto> _createValidator;
        private readonly IValidator<UpdateUserDto> _updateValidator;
        private readonly IValidator<SetPasswordDto>? _passwordValidator;
        private readonly IEmailService? _emailService;
        private readonly ILogger<UserService>? _logger;

        public UserService(
            IUserRepository userRepository,
            IValidator<CreateUserDto> createValidator,
            IValidator<UpdateUserDto> updateValidator,
            IValidator<SetPasswordDto>? passwordValidator = null,
            IEmailService? emailService = null,
            ILogger<UserService>? logger = null)
        {
            _userRepository = userRepository;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _passwordValidator = passwordValidator;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<UserDto?> GetByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return null;

            return MapToDto(user);
        }

        public async Task<IEnumerable<UserDto>> GetAllAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(MapToDto);
        }

        public async Task<UserDto> CreateAsync(CreateUserDto userDto)
        {
            _logger?.LogInformation("Creating new user with username: {Username}, email: {Email}",
                userDto.Username, userDto.Email);

            var validationResult = await _createValidator.ValidateAsync(userDto);
            if (!validationResult.IsValid)
            {
                _logger?.LogWarning("User creation validation failed: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                // Use fully qualified name
                throw new FluentValidation.ValidationException(validationResult.Errors);
            }

            var user = new User
            {
                Username = userDto.Username,
                Email = userDto.Email,
                // Generate token for all new users
                PasswordResetToken = GenerateSecureToken(),
                PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(24)
            };

            // Create notification preferences if provided
            if (userDto.NotificationPreferences != null)
            {
                user.NotificationPreferences = new NotificationPreferences
                {
                    EmailEnabled = userDto.NotificationPreferences.EmailEnabled,
                    SlackEnabled = userDto.NotificationPreferences.SlackEnabled,
                    SlackWebhookUrl = userDto.NotificationPreferences.SlackWebhookUrl
                };

                _logger?.LogInformation("Setting notification preferences: EmailEnabled={EmailEnabled}, " +
                    "SlackEnabled={SlackEnabled}",
                    user.NotificationPreferences.EmailEnabled,
                    user.NotificationPreferences.SlackEnabled);
            }
            else
            {
                // Create default notification preferences
                user.NotificationPreferences = new NotificationPreferences
                {
                    EmailEnabled = true,
                    SlackEnabled = false
                };

                _logger?.LogInformation("Setting default notification preferences");
            }

            try
            {
                // Save the user to the database
                var createdUser = await _userRepository.CreateAsync(user);
                _logger?.LogInformation("User created successfully with ID: {UserId}", createdUser.Id);

                // Send email for password setup if email service is available and email notifications are enabled
                var shouldSendEmail = _emailService != null &&
                    (createdUser.NotificationPreferences == null || createdUser.NotificationPreferences.EmailEnabled);

                if (shouldSendEmail)
                {
                    _logger?.LogInformation("Sending password setup email to: {Email}", createdUser.Email);
                    try
                    {
                        await _emailService!.SendPasswordSetupEmailAsync(
                            createdUser.Email,
                            createdUser.Username,
                            createdUser.PasswordResetToken!
                        );
                        _logger?.LogInformation("Password setup email sent successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to send password setup email");
                        // Continue even if email fails
                    }
                }
                else
                {
                    _logger?.LogInformation("Skipping password setup email (email service not available or notifications disabled)");
                }

                return MapToDto(createdUser);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating user");
                throw;
            }
        }

        public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto userDto)
        {
            _logger?.LogInformation("Updating user with ID: {UserId}", id);

            var validationResult = await _updateValidator.ValidateAsync(userDto);
            if (!validationResult.IsValid)
            {
                _logger?.LogWarning("User update validation failed: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                // Use fully qualified name
                throw new FluentValidation.ValidationException(validationResult.Errors);
            }

            var existingUser = await _userRepository.GetByIdAsync(id);
            if (existingUser == null)
            {
                _logger?.LogWarning("User not found: {UserId}", id);
                return null;
            }

            existingUser.Email = userDto.Email;

            // Handle notification preferences update
            if (userDto.NotificationPreferences != null)
            {
                if (existingUser.NotificationPreferences == null)
                {
                    // Create new notification preferences
                    existingUser.NotificationPreferences = new NotificationPreferences
                    {
                        UserId = id,
                        EmailEnabled = userDto.NotificationPreferences.EmailEnabled,
                        SlackEnabled = userDto.NotificationPreferences.SlackEnabled,
                        SlackWebhookUrl = userDto.NotificationPreferences.SlackWebhookUrl
                    };

                    _logger?.LogInformation("Creating new notification preferences for user {UserId}", id);
                }
                else
                {
                    // Update existing notification preferences
                    existingUser.NotificationPreferences.EmailEnabled = userDto.NotificationPreferences.EmailEnabled;
                    existingUser.NotificationPreferences.SlackEnabled = userDto.NotificationPreferences.SlackEnabled;
                    existingUser.NotificationPreferences.SlackWebhookUrl = userDto.NotificationPreferences.SlackWebhookUrl;

                    _logger?.LogInformation("Updating existing notification preferences for user {UserId}", id);
                }
            }

            try
            {
                var updatedUser = await _userRepository.UpdateAsync(existingUser);
                _logger?.LogInformation("User {UserId} updated successfully", id);
                return MapToDto(updatedUser);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating user {UserId}", id);
                throw;
            }
        }

        public async Task<bool> SetPasswordAsync(SetPasswordDto setPasswordDto)
        {
            if (_passwordValidator != null)
            {
                var validationResult = await _passwordValidator.ValidateAsync(setPasswordDto);
                if (!validationResult.IsValid)
                {
                    // Use fully qualified name
                    throw new FluentValidation.ValidationException(validationResult.Errors);
                }
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(setPasswordDto.Token);
            if (user == null)
            {
                _logger?.LogWarning("Invalid or expired password reset token");
                return false;
            }

            user.PasswordHash = HashPassword(setPasswordDto.Password);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            await _userRepository.UpdateAsync(user);
            _logger?.LogInformation("Password set successfully for user {UserId}", user.Id);
            return true;
        }

        public async Task<byte[]> ExportUsersToCsvAsync()
        {
            var users = await _userRepository.GetAllAsync();
            var userDtos = users.Select(MapToDto).ToList();

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(userDtos);
            }

            return memoryStream.ToArray();
        }

        private UserDto MapToDto(User user)
        {
            var dto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsAdmin = user.Email.EndsWith("@admin.com")
            };

            // Map notification preferences
            if (user.NotificationPreferences != null)
            {
                dto.NotificationPreferences = new NotificationPreferencesDto
                {
                    EmailEnabled = user.NotificationPreferences.EmailEnabled,
                    SlackEnabled = user.NotificationPreferences.SlackEnabled,
                    SlackWebhookUrl = user.NotificationPreferences.SlackWebhookUrl
                };
            }

            return dto;
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }
    }
}
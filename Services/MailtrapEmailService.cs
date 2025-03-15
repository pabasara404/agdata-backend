using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgData.Services
{
    public class MailtrapEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MailtrapEmailService> _logger;

        public MailtrapEmailService(IConfiguration configuration, ILogger<MailtrapEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordSetupEmailAsync(string email, string username, string token)
        {
            try
            {
                _logger.LogInformation("Preparing to send password setup email to {Email}", email);

                // Get configuration values - use exact values from your code example
                string host = "sandbox.smtp.mailtrap.io";
                int port = 2525;
                string? mailtrapUsername = _configuration["Mailtrap:Username"];
                string? mailtrapPassword = _configuration["Mailtrap:Password"];

                if (string.IsNullOrEmpty(mailtrapUsername) || string.IsNullOrEmpty(mailtrapPassword))
                {
                    throw new InvalidOperationException("Mailtrap username or password is missing in configuration");
                }

                _logger.LogDebug("Mailtrap configuration: Host={Host}, Port={Port}, Username={Username}",
                    host, port, mailtrapUsername);

                // Create SMTP client exactly as in the example
                var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(mailtrapUsername, mailtrapPassword),
                    EnableSsl = true
                };

                // Generate password setup URL
                var baseUrl = _configuration["BaseUrl"] ?? "http://localhost:4200";
                var setupUrl = $"{baseUrl}/set-password?token={WebUtility.UrlEncode(token)}";
                _logger.LogDebug("Password setup URL: {SetupUrl}", setupUrl);

                // Build email body
                string body = $@"
                    <html>
                    <body>
                        <h2>Welcome to AgData, {WebUtility.HtmlEncode(username)}!</h2>
                        <p>Your account has been created. Please set up your password by clicking the link below:</p>
                        <p><a href='{setupUrl}'>Set Your Password</a></p>
                        <p>This link will expire in 24 hours.</p>
                        <p>If you didn't request this account, please ignore this email.</p>
                    </body>
                    </html>";

                // Create and send the email
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("noreply@agdata.com", "AgData System"),
                    Subject = "Set Up Your AgData Account Password",
                    IsBodyHtml = true,
                    Body = body
                };

                mailMessage.To.Add(new MailAddress(email));

                // Use the simple Send() method as in the example, but wrapped in Task.Run for async
                await Task.Run(() => {
                    _logger.LogInformation("Sending email via Mailtrap...");
                    client.Send(mailMessage);
                });

                _logger.LogInformation("Password setup email successfully sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password setup email to {Email}", email);

                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {Message}", ex.InnerException.Message);
                }

                throw;
            }
        }
    }
}
using System.Threading.Tasks;

namespace AgData.Services
{
    public interface IEmailService
    {
        Task SendPasswordSetupEmailAsync(string email, string username, string token);
    }
}
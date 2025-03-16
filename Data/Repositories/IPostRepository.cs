using System.Collections.Generic;
using System.Threading.Tasks;
using AgData.Models;

namespace AgData.Data.Repositories
{
    public interface IPostRepository
    {
        Task<Post?> GetByIdAsync(int id);
        Task<IEnumerable<Post>> GetAllAsync();
        Task<IEnumerable<Post>> GetByUserIdAsync(int userId);
        Task<int> GetCountByUserIdAsync(int userId);
        Task<Post> CreateAsync(Post post);
        Task<Post> UpdateAsync(Post post);
        Task DeleteAsync(int id);
    }
}
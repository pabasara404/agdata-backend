using System.Collections.Generic;
using System.Threading.Tasks;
using AgData.DTOs;

namespace AgData.Services
{
    public interface IPostService
    {
        Task<PostDto?> GetByIdAsync(int id);
        Task<IEnumerable<PostDto>> GetAllAsync();
        Task<IEnumerable<PostDto>> GetByUserIdAsync(int userId);
        Task<int> GetCountByUserIdAsync(int userId);
        Task<PostDto> CreateAsync(int userId, CreatePostDto createPostDto);
        Task<PostDto?> UpdateAsync(int id, int userId, UpdatePostDto updatePostDto);
        Task<bool> DeleteAsync(int id, int userId);
    }
}
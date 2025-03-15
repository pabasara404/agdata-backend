using System.Collections.Generic;
using System.Threading.Tasks;
using AgData.DTOs;

namespace AgData.Services
{
    public interface IUserService
    {
        Task<UserDto?> GetByIdAsync(int id);
        Task<IEnumerable<UserDto>> GetAllAsync();
        Task<UserDto> CreateAsync(CreateUserDto userDto);
        Task<UserDto?> UpdateAsync(int id, UpdateUserDto userDto);
        Task<bool> SetPasswordAsync(SetPasswordDto setPasswordDto);
        Task<byte[]> ExportUsersToCsvAsync();
    }
}
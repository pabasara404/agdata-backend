using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using AgData.Data;
using AgData.Models;

namespace AgData.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            return await connection.QuerySingleOrDefaultAsync<User>(
                "SELECT Id, Username, Email FROM Users WHERE Id = @Id",
                new { Id = id });
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            return await connection.QueryAsync<User>("SELECT Id, Username, Email FROM Users");
        }

        public async Task<User> CreateAsync(User user)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var id = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Users (Username, Email) VALUES (@Username, @Email); SELECT last_insert_rowid()",
                user);

            user.Id = id;
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            await connection.ExecuteAsync(
                "UPDATE Users SET Username = @Username, Email = @Email WHERE Id = @Id",
                user);

            return user;
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            await connection.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = id });
        }
    }
}
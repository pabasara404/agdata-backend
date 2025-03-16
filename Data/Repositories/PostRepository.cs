using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using AgData.Models;

namespace AgData.Data.Repositories
{
    public class PostRepository : IPostRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<PostRepository> _logger;

        public PostRepository(IDbConnectionFactory connectionFactory, ILogger<PostRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<Post?> GetByIdAsync(int id)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var post = await connection.QuerySingleOrDefaultAsync<Post>(
                @"SELECT Id, UserId, Title, Content, CreatedAt, UpdatedAt
                  FROM Posts WHERE Id = @Id",
                new { Id = id });

            return post;
        }

        public async Task<IEnumerable<Post>> GetAllAsync()
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var posts = await connection.QueryAsync<Post>(
                @"SELECT Id, UserId, Title, Content, CreatedAt, UpdatedAt
                  FROM Posts ORDER BY CreatedAt DESC");

            return posts;
        }

        public async Task<IEnumerable<Post>> GetByUserIdAsync(int userId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var posts = await connection.QueryAsync<Post>(
                @"SELECT Id, UserId, Title, Content, CreatedAt, UpdatedAt
                  FROM Posts WHERE UserId = @UserId ORDER BY CreatedAt DESC",
                new { UserId = userId });

            return posts;
        }

        public async Task<int> GetCountByUserIdAsync(int userId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Posts WHERE UserId = @UserId",
                new { UserId = userId });

            return count;
        }

        public async Task<Post> CreateAsync(Post post)
        {
            _logger.LogInformation("Creating new post: {Title}", post.Title);

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Set creation timestamp
            post.CreatedAt = DateTime.UtcNow;

            var id = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Posts (UserId, Title, Content, CreatedAt)
                  VALUES (@UserId, @Title, @Content, @CreatedAt);
                  SELECT last_insert_rowid()",
                post);

            post.Id = id;
            _logger.LogDebug("Post created with ID: {PostId}", id);

            return post;
        }

        public async Task<Post> UpdateAsync(Post post)
        {
            _logger.LogInformation("Updating post with ID: {PostId}", post.Id);

            post.UpdatedAt = DateTime.UtcNow;

            using var connection = await _connectionFactory.CreateConnectionAsync();

            await connection.ExecuteAsync(
                @"UPDATE Posts SET
                  Title = @Title,
                  Content = @Content,
                  UpdatedAt = @UpdatedAt
                  WHERE Id = @Id",
                post);

            _logger.LogDebug("Post updated: {PostId}", post.Id);

            return post;
        }

        public async Task DeleteAsync(int id)
        {
            _logger.LogInformation("Deleting post with ID: {PostId}", id);

            using var connection = await _connectionFactory.CreateConnectionAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Posts WHERE Id = @Id",
                new { Id = id });

            _logger.LogDebug("Post deleted: {PostId}", id);
        }
    }
}
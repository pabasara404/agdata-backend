using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Logging;
using AgData.Data.Repositories;
using AgData.DTOs;
using AgData.Models;

namespace AgData.Services
{
    public class PostService : IPostService
    {
        private readonly IPostRepository _postRepository;
        private readonly IUserRepository _userRepository;
        private readonly IValidator<CreatePostDto> _createValidator;
        private readonly IValidator<UpdatePostDto> _updateValidator;
        private readonly ILogger<PostService> _logger;

        public PostService(
            IPostRepository postRepository,
            IUserRepository userRepository,
            IValidator<CreatePostDto> createValidator,
            IValidator<UpdatePostDto> updateValidator,
            ILogger<PostService> logger)
        {
            _postRepository = postRepository;
            _userRepository = userRepository;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _logger = logger;
        }

        public async Task<PostDto?> GetByIdAsync(int id)
        {
            var post = await _postRepository.GetByIdAsync(id);
            if (post == null) return null;

            return await MapToDto(post);
        }

        public async Task<IEnumerable<PostDto>> GetAllAsync()
        {
            var posts = await _postRepository.GetAllAsync();
            var dtos = new List<PostDto>();

            foreach (var post in posts)
            {
                var dto = await MapToDto(post);
                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }

            return dtos;
        }

        public async Task<IEnumerable<PostDto>> GetByUserIdAsync(int userId)
        {
            var posts = await _postRepository.GetByUserIdAsync(userId);
            var dtos = new List<PostDto>();

            foreach (var post in posts)
            {
                var dto = await MapToDto(post);
                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }

            return dtos;
        }

        public async Task<int> GetCountByUserIdAsync(int userId)
        {
            return await _postRepository.GetCountByUserIdAsync(userId);
        }

        public async Task<PostDto> CreateAsync(int userId, CreatePostDto createPostDto)
        {
            _logger.LogInformation("Creating new post for user {UserId}: {Title}", userId, createPostDto.Title);

            var validationResult = await _createValidator.ValidateAsync(createPostDto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Post creation validation failed: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                throw new ValidationException(validationResult.Errors);
            }

            // Check if user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                throw new ArgumentException($"User with ID {userId} not found");
            }

            var post = new Post
            {
                UserId = userId,
                Title = createPostDto.Title,
                Content = createPostDto.Content,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var createdPost = await _postRepository.CreateAsync(post);
                _logger.LogInformation("Post created successfully with ID: {PostId}", createdPost.Id);

                var postDto = await MapToDto(createdPost);
                if (postDto == null)
                {
                    throw new InvalidOperationException("Failed to map created post to DTO");
                }
                return postDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                throw;
            }
        }

        public async Task<PostDto?> UpdateAsync(int id, int userId, UpdatePostDto updatePostDto)
        {
            _logger.LogInformation("Updating post with ID: {PostId} for user {UserId}", id, userId);

            var validationResult = await _updateValidator.ValidateAsync(updatePostDto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Post update validation failed: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                throw new ValidationException(validationResult.Errors);
            }

            var existingPost = await _postRepository.GetByIdAsync(id);
            if (existingPost == null)
            {
                _logger.LogWarning("Post not found: {PostId}", id);
                return null;
            }

            // Only the author or an admin can update the post
            // Admin check will be done in the controller
            if (existingPost.UserId != userId)
            {
                _logger.LogWarning("Unauthorized update attempt for post {PostId} by user {UserId}", id, userId);
                return null;
            }

            if (updatePostDto.Title != null)
                existingPost.Title = updatePostDto.Title;

            if (updatePostDto.Content != null)
                existingPost.Content = updatePostDto.Content;

            existingPost.UpdatedAt = DateTime.UtcNow;

            try
            {
                var updatedPost = await _postRepository.UpdateAsync(existingPost);
                _logger.LogInformation("Post {PostId} updated successfully", id);
                return await MapToDto(updatedPost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post {PostId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            _logger.LogInformation("Deleting post with ID: {PostId} for user {UserId}", id, userId);

            var existingPost = await _postRepository.GetByIdAsync(id);
            if (existingPost == null)
            {
                _logger.LogWarning("Post not found: {PostId}", id);
                return false;
            }

            // Only the author or an admin can delete the post
            // Admin check will be done in the controller
            if (existingPost.UserId != userId)
            {
                _logger.LogWarning("Unauthorized delete attempt for post {PostId} by user {UserId}", id, userId);
                return false;
            }

            try
            {
                await _postRepository.DeleteAsync(id);
                _logger.LogInformation("Post {PostId} deleted successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post {PostId}", id);
                throw;
            }
        }

        private async Task<PostDto?> MapToDto(Post post)
        {
            var user = await _userRepository.GetByIdAsync(post.UserId);
            if (user == null)
            {
                _logger.LogWarning("User not found for post {PostId}, UserId: {UserId}", post.Id, post.UserId);
                return null;
            }

            return new PostDto
            {
                Id = post.Id,
                UserId = post.UserId,
                Username = user.Username,
                Title = post.Title,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt
            };
        }
    }
}
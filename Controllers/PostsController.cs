using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AgData.DTOs;
using AgData.Services;

namespace AgData.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ILogger<PostsController> _logger;

        public PostsController(IPostService postService, ILogger<PostsController> logger)
        {
            _postService = postService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PostDto>>> GetAll()
        {
            try
            {
                var posts = await _postService.GetAllAsync();
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts");
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PostDto>> GetById(int id)
        {
            try
            {
                var post = await _postService.GetByIdAsync(id);
                if (post == null)
                {
                    return NotFound(new { message = "Post not found" });
                }
                return Ok(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post {PostId}", id);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<PostDto>>> GetByUserId(int userId)
        {
            try
            {
                var posts = await _postService.GetByUserIdAsync(userId);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<PostDto>> Create([FromBody] CreatePostDto createPostDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var post = await _postService.CreateAsync(userId, createPostDto);
                return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
            }
            catch (FluentValidation.ValidationException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PostDto>> Update(int id, [FromBody] UpdatePostDto updatePostDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");

                // If user is admin, they can update any post
                var post = isAdmin
                    ? await _postService.GetByIdAsync(id)
                    : await _postService.UpdateAsync(id, userId, updatePostDto);

                if (post == null)
                {
                    return NotFound(new { message = "Post not found or you don't have permission to update it" });
                }

                // If user is admin and post was found, update it
                if (isAdmin && post != null)
                {
                    // Get the original post's user ID
                    var originalPost = await _postService.GetByIdAsync(id);
                    if (originalPost == null)
                    {
                        return NotFound(new { message = "Post not found" });
                    }

                    post = await _postService.UpdateAsync(id, originalPost.UserId, updatePostDto);
                }

                return Ok(post);
            }
            catch (FluentValidation.ValidationException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post {PostId}", id);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");

                if (!isAdmin)
                {
                    var result = await _postService.DeleteAsync(id, userId);
                    if (!result)
                    {
                        return NotFound(new { message = "Post not found or you don't have permission to delete it" });
                    }
                }
                else
                {
                    // Admin can delete any post
                    var post = await _postService.GetByIdAsync(id);
                    if (post == null)
                    {
                        return NotFound(new { message = "Post not found" });
                    }

                    await _postService.DeleteAsync(id, post.UserId);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post {PostId}", id);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new InvalidOperationException("User ID not found in claims");
            }

            return userId;
        }
    }
}
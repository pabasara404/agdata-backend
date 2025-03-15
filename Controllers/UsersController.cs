using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AgData.DTOs;
using AgData.Services;

namespace AgData.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
        {
            try
            {
                var users = await _userService.GetAllAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetById(int id)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> Create(CreateUserDto createUserDto)
        {
            try
            {
                var user = await _userService.CreateAsync(createUserDto);
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
            }
            catch (FluentValidation.ValidationException ex)
            {
                return BadRequest(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> Update(int id, UpdateUserDto updateUserDto)
        {
            try
            {
                var user = await _userService.UpdateAsync(id, updateUserDto);
                if (user == null)
                {
                    return NotFound();
                }
                return Ok(user);
            }
            catch (FluentValidation.ValidationException ex)
            {
                return BadRequest(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpPost("set-password")]
        public async Task<ActionResult> SetPassword(SetPasswordDto setPasswordDto)
        {
            try
            {
                var result = await _userService.SetPasswordAsync(setPasswordDto);
                if (result)
                {
                    return Ok(new { message = "Password has been set successfully" });
                }
                return BadRequest(new { message = "Invalid or expired token" });
            }
            catch (FluentValidation.ValidationException ex)
            {
                return BadRequest(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting password");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpGet("export-csv")]
        public async Task<ActionResult> ExportToCsv()
        {
            try
            {
                var csvData = await _userService.ExportUsersToCsvAsync();
                return File(csvData, "text/csv", "users.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting users to CSV");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }
}
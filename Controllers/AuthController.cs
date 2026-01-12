using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blog.Backend.Data;
using Blog.Backend.Models;
using Blog.Backend.DTOs;
using System.Security.Cryptography;
using System.Text;

namespace Blog.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly BlogDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(BlogDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] SignupDto dto)
        {
            try
            {
                _logger.LogInformation($"Signup attempt for username: {dto.Username}, email: {dto.Email}");

                // Basic validation
                if (string.IsNullOrEmpty(dto.Username) || string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                {
                    _logger.LogWarning("Signup failed: Missing required fields");
                    return BadRequest(new { message = "All fields are required" });
                }

                if (dto.Password.Length < 6)
                {
                    _logger.LogWarning("Signup failed: Password too short");
                    return BadRequest(new { message = "Password must be at least 6 characters" });
                }

                // Check username
                _logger.LogInformation("Checking if username exists...");
                var usernameExists = await _context.Users
                    .AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());

                if (usernameExists)
                {
                    _logger.LogWarning($"Signup failed: Username '{dto.Username}' already exists");
                    return BadRequest(new { message = "Username already exists" });
                }

                // Check email
                _logger.LogInformation("Checking if email exists...");
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());

                if (emailExists)
                {
                    _logger.LogWarning($"Signup failed: Email '{dto.Email}' already exists");
                    return BadRequest(new { message = "Email already exists" });
                }

                // Hash password
                _logger.LogInformation("Hashing password...");
                string passwordHash;
                using (var sha256 = SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dto.Password));
                    passwordHash = Convert.ToBase64String(bytes);
                }

                // Create user
                _logger.LogInformation("Creating new user...");
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = dto.Username,
                    Email = dto.Email,
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User created successfully with ID: {user.Id}");

                // Return simple response without JWT for now
                return Ok(new
                {
                    message = "User created successfully",
                    userId = user.Id,
                    username = user.Username
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError($"Database error during signup: {dbEx.Message}");
                _logger.LogError($"Inner exception: {dbEx.InnerException?.Message}");
                return StatusCode(500, new { message = "Database error", error = dbEx.InnerException?.Message ?? dbEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during signup: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Server error", error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                _logger.LogInformation($"Login attempt for email: {dto.Email}");

                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                {
                    return BadRequest(new { message = "Email and password are required" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

                if (user == null)
                {
                    _logger.LogWarning($"Login failed: User not found for email {dto.Email}");
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                // Hash the provided password
                string passwordHash;
                using (var sha256 = SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dto.Password));
                    passwordHash = Convert.ToBase64String(bytes);
                }

                if (user.PasswordHash != passwordHash)
                {
                    _logger.LogWarning($"Login failed: Invalid password for email {dto.Email}");
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                _logger.LogInformation($"Login successful for user: {user.Username}");

                return Ok(new
                {
                    message = "Login successful",
                    userId = user.Id,
                    username = user.Username
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during login: {ex.Message}");
                return StatusCode(500, new { message = "Server error", error = ex.Message });
            }
        }
    }
}
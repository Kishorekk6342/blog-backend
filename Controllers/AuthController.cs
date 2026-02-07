using BCrypt.Net;
using Blog.Backend.Data;
using Blog.Backend.DTOs;
using Blog.Backend.Models;
using Blog.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blog.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly BlogDbContext _context;
        private readonly ILogger<AuthController> _logger;

        private readonly JwtService _jwtService;

        public AuthController(
            BlogDbContext context,
            ILogger<AuthController> logger,
            JwtService jwtService)
        {
            _context = context;
            _logger = logger;
            _jwtService = jwtService;
        }


        // ===================== SIGNUP =====================
        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] SignupDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Username) ||
                    string.IsNullOrWhiteSpace(dto.Email) ||
                    string.IsNullOrWhiteSpace(dto.Password))
                {
                    return BadRequest("All fields are required");
                }

                if (dto.Password.Length < 6)
                    return BadRequest("Password must be at least 6 characters");

                if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                    return BadRequest("Email already exists");

                if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                    return BadRequest("Username already exists");

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = dto.Username,
                    Email = dto.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok("User registered successfully");
            }
            catch (DbUpdateException)
            {
                return BadRequest("Username or Email already exists");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signup error");
                return StatusCode(500, "Server error");
            }
        }

        // ===================== LOGIN =====================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

            if (user == null)
                return Unauthorized("Invalid email or password");

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid)
                return Unauthorized("Invalid email or password");

            // ✅ REAL JWT TOKEN
            var token = _jwtService.GenerateToken(user);

            return Ok(new AuthResponse
            {
                Token = token
            });
        }
    }

        // ===================== RESPONSE DTO =====================
        public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}

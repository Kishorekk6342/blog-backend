using Blog.Backend.Data;
using Blog.Backend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Supabase;
using Supabase.Storage;

namespace Blog.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly BlogDbContext _context;
        private readonly ILogger<UserController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IConfiguration _configuration;

        public UserController(
            BlogDbContext context,
            ILogger<UserController> logger,
            Supabase.Client supabase,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _supabase = supabase;
            _configuration = configuration;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        // GET: api/User/profile (Get current user's profile)
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized("Invalid user token");

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound("User not found");

            var dto = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Bio = user.Bio ?? "",
                Location = user.Location ?? "",
                Website = user.Website ?? "",
                ProfilePictureUrl = user.ProfilePictureUrl ?? "",
                CreatedAt = user.CreatedAt,
                PostCount = await _context.Posts.CountAsync(p => p.UserId == user.Id),
                FollowerCount = await _context.Follows.CountAsync(f => f.FollowingId == user.Id),
                FollowingCount = await _context.Follows.CountAsync(f => f.FollowerId == user.Id),
            };

            return Ok(dto);
        }

        // GET: api/User/profile/{userId} (Get another user's profile)
        [HttpGet("profile/{userId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserProfile(Guid userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound("User not found");

            var dto = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Bio = user.Bio ?? "",
                Location = user.Location ?? "",
                Website = user.Website ?? "",
                ProfilePictureUrl = user.ProfilePictureUrl ?? "",
                CreatedAt = user.CreatedAt,
                PostCount = await _context.Posts.CountAsync(p => p.UserId == user.Id),
                FollowerCount = await _context.Follows.CountAsync(f => f.FollowingId == user.Id),
                FollowingCount = await _context.Follows.CountAsync(f => f.FollowerId == user.Id),
            };

            return Ok(dto);
        }

        // PUT: api/User/profile (Update current user's profile)
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                    return Unauthorized("Invalid user token");

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound("User not found");

                // Validate username
                if (string.IsNullOrWhiteSpace(dto.Username))
                    return BadRequest("Username is required");

                // Check if username is taken by another user
                if (dto.Username != user.Username)
                {
                    var usernameExists = await _context.Users
                        .AnyAsync(u => u.Username == dto.Username && u.Id != userId);

                    if (usernameExists)
                        return BadRequest("Username already taken");
                }

                // Validate email
                if (string.IsNullOrWhiteSpace(dto.Email))
                    return BadRequest("Email is required");

                // Check if email is taken by another user
                if (dto.Email != user.Email)
                {
                    var emailExists = await _context.Users
                        .AnyAsync(u => u.Email == dto.Email && u.Id != userId);

                    if (emailExists)
                        return BadRequest("Email already taken");
                }

                // Update user properties
                user.Username = dto.Username;
                user.Email = dto.Email;
                user.Bio = dto.Bio;
                user.Location = dto.Location;
                user.Website = dto.Website;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Profile updated successfully" });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating profile");
                return BadRequest("Username or Email already exists");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, "Error updating profile");
            }
        }

        // GET: api/User/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var users = await _context.Users
                    .Where(u => u.Id != currentUserId)
                    .Select(u => new UserProfileDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        Bio = u.Bio,
                        Location = u.Location,
                        ProfilePictureUrl = u.ProfilePictureUrl,
                        CreatedAt = u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users");
                return StatusCode(500, "Error fetching users");
            }
        }

        // GET: api/User/search?query=kk
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<UserProfileDto>());

            try
            {
                var users = await _context.Users
                    .Where(u =>
                        u.Username.ToLower().Contains(query.ToLower()) ||
                        u.Email.ToLower().Contains(query.ToLower()))
                    .Select(u => new UserProfileDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        Bio = u.Bio,
                        Location = u.Location,
                        ProfilePictureUrl = u.ProfilePictureUrl,
                        CreatedAt = u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
                return StatusCode(500, "Error searching users");
            }
        }

        // POST: api/User/profile-picture (Upload profile picture)
        [HttpPost("profile-picture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            try
            {
                var fileName = $"{userId}.jpg";

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();

                await _supabase.Storage
                    .From("avatars")
                    .Upload(bytes, fileName, new Supabase.Storage.FileOptions
                    {
                        ContentType = file.ContentType,
                        Upsert = true
                    });

                var supabaseUrl = _configuration["Supabase:Url"];
                var publicUrl = $"{supabaseUrl}/storage/v1/object/public/avatars/{fileName}";

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.ProfilePictureUrl = publicUrl;
                    await _context.SaveChangesAsync();
                }

                return Ok(new { url = publicUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture");
                return StatusCode(500, "Error uploading profile picture");
            }
        }

        // GET: api/User/settings
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            return Ok(new UserSettingsDto
            {
                Email = user.Email,
                EmailNotifications = user.EmailNotifications,
                PostNotifications = user.PostNotifications,
                CommentNotifications = user.CommentNotifications,
                PrivateProfile = user.PrivateProfile
            });
        }

        // PUT: api/User/notification-settings
        [HttpPut("notification-settings")]
        public async Task<IActionResult> UpdateNotificationSettings(
            [FromBody] NotificationSettingsDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            user.EmailNotifications = dto.EmailNotifications;
            user.PostNotifications = dto.PostNotifications;
            user.CommentNotifications = dto.CommentNotifications;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Notification settings updated" });
        }

        // PUT: api/User/privacy-settings
        [HttpPut("privacy-settings")]
        public async Task<IActionResult> UpdatePrivacySettings(
            [FromBody] PrivacySettingsDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            user.PrivateProfile = dto.PrivateProfile;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Privacy settings updated" });
        }

        // DELETE: api/User/profile-picture (Delete profile picture)
        [HttpDelete("profile-picture")]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound("User not found");

                // Delete from Supabase Storage
                var fileName = $"{userId}.jpg";

                try
                {
                    await _supabase.Storage
                        .From("avatars")
                        .Remove(new List<string> { fileName });
                }
                catch (Exception ex)
                {
                    // Log but don't fail - file might not exist
                    _logger.LogWarning(ex, "Could not delete file from storage (might not exist)");
                }

                // Remove URL from database
                user.ProfilePictureUrl = null;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Profile picture deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile picture");
                return StatusCode(500, "Error deleting profile picture");
            }
        }
    }
}
using Blog.Backend.Data;
using Blog.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Blog.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FollowController : ControllerBase
    {
        private readonly BlogDbContext _context;

        public FollowController(BlogDbContext context)
        {
            _context = context;
        }

        // GET: api/Follow/status/{userId}
        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetFollowStatus(Guid userId)
        {
            try
            {
                var currentUserId = GetUserId();
                var isFollowing = await _context.Follows.AnyAsync(f =>
                    f.FollowerId == currentUserId &&
                    f.FollowingId == userId);

                return Ok(new { isFollowing });
            }
            catch
            {
                return Ok(new { isFollowing = false });
            }
        }

        // POST: api/Follow/{userId}
        [HttpPost("{userId}")]
        public async Task<IActionResult> Follow(Guid userId)
        {
            var currentUserId = GetUserId();

            if (currentUserId == userId)
                return BadRequest("You cannot follow yourself");

            var exists = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentUserId &&
                f.FollowingId == userId);

            if (exists)
                return Ok(new { message = "Already following" });

            // ✅ FIXED: No Id property needed - composite key handles it
            var follow = new Follow
            {
                FollowerId = currentUserId,
                FollowingId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Follows.Add(follow);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully followed user" });
        }

        // DELETE: api/Follow/{userId}
        [HttpDelete("{userId}")]
        public async Task<IActionResult> Unfollow(Guid userId)
        {
            var currentUserId = GetUserId();

            var follow = await _context.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == currentUserId &&
                f.FollowingId == userId);

            if (follow == null)
                return NotFound("You are not following this user");

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully unfollowed user" });
        }

        // GET: api/Follow/followers
        [HttpGet("followers")]
        public async Task<IActionResult> GetFollowers()
        {
            var currentUserId = GetUserId();

            var followers = await _context.Follows
                .Where(f => f.FollowingId == currentUserId)
                .Select(f => new
                {
                    f.Follower.Id,
                    f.Follower.Username,
                    f.Follower.Email,
                    f.Follower.ProfilePictureUrl,
                    f.CreatedAt
                })
                .ToListAsync();

            return Ok(followers);
        }

        // GET: api/Follow/following
        [HttpGet("following")]
        public async Task<IActionResult> GetFollowing()
        {
            var currentUserId = GetUserId();

            var following = await _context.Follows
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => new
                {
                    f.Following.Id,
                    f.Following.Username,
                    f.Following.Email,
                    f.Following.ProfilePictureUrl,
                    f.CreatedAt
                })
                .ToListAsync();

            return Ok(following);
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid user token");

            return userId;
        }
    }
}
using Blog.Backend.Data;
using Blog.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Supabase.Gotrue;
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
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty) return Unauthorized();

            bool isFollowing = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentUserId && f.FollowingId == userId);

            var targetUser = await _context.Users.FindAsync(userId);

            bool isPending = false;

            if (!isFollowing && targetUser != null)
            {
                var pendingRequest = await _context.FollowRequests.FirstOrDefaultAsync(r =>
                    r.RequesterId == currentUserId &&
                    r.TargetId == userId &&
                    r.Status == "pending");

                if (pendingRequest != null)
                {
                    // ✅ Profile changed to public — auto-convert request to follow
                    if (!targetUser.PrivateProfile)
                    {
                        _context.FollowRequests.Remove(pendingRequest);
                        _context.Follows.Add(new Follow
                        {
                            Id = Guid.NewGuid(),
                            FollowerId = currentUserId,
                            FollowingId = userId,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                        isFollowing = true;
                        isPending = false;
                    }
                    else
                    {
                        isPending = true;
                    }
                }
            }

            return Ok(new { isFollowing, isPending });
        }
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        // POST: api/Follow/{userId}
        [HttpPost("{userId}")]
        public async Task<IActionResult> FollowUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty)
                return Unauthorized();

            if (currentUserId == userId)
                return BadRequest("You cannot follow yourself");

            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null)
                return NotFound("User not found");

            // Already following?
            bool alreadyFollowing = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentUserId &&
                f.FollowingId == userId);

            if (alreadyFollowing)
                return BadRequest("Already following");

            // 🔥 AUTO-CONVERT pending request if profile is now public
            var pendingRequest = await _context.FollowRequests.FirstOrDefaultAsync(r =>
                r.RequesterId == currentUserId &&
                r.TargetId == userId &&
                r.Status == "pending");

            if (!targetUser.PrivateProfile && pendingRequest != null)
            {
                _context.FollowRequests.Remove(pendingRequest);

                _context.Follows.Add(new Follow
                {
                    Id = Guid.NewGuid(),
                    FollowerId = currentUserId,
                    FollowingId = userId,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    isFollowing = true,
                    isPending = false
                });
            }

            // 🔒 PRIVATE PROFILE → CREATE FOLLOW REQUEST
            if (targetUser.PrivateProfile)
            {
                bool requestExists = await _context.FollowRequests.AnyAsync(r =>
                    r.RequesterId == currentUserId &&
                    r.TargetId == userId &&
                    r.Status == "pending"
                );

                if (requestExists)
                {
                    return Ok(new
                    {
                        isFollowing = false,
                        isPending = true
                    });
                }

                _context.FollowRequests.Add(new FollowRequest
                {
                    Id = Guid.NewGuid(),
                    RequesterId = currentUserId,
                    TargetId = userId,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                });

                var requester = await _context.Users.FindAsync(currentUserId);

                var username = requester?.Username ?? "Someone";

                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Message = $"{username} sent you a follow request",
                    Type = "FollowRequest",
                    RelatedId = currentUserId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    isFollowing = false,
                    isPending = true
                });
            }

            // 🌍 PUBLIC PROFILE → FOLLOW DIRECTLY
            _context.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                FollowerId = currentUserId,
                FollowingId = userId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                isFollowing = true,
                isPending = false
            });
        }

        // DELETE: api/Follow/{userId}
        [HttpDelete("{userId}")]
        public async Task<IActionResult> Unfollow(Guid userId)
        {
            var currentUserId = GetUserId();

            var follow = await _context.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == currentUserId && f.FollowingId == userId);

            if (follow == null)
                return NotFound("You are not following this user");

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Unfollowed successfully" });
        }

        [HttpPost("accept/{requesterId}")]
        public async Task<IActionResult> AcceptRequest(Guid requesterId)
        {
            var currentUserId = GetUserId();

            var request = await _context.FollowRequests.FirstOrDefaultAsync(r =>
                r.RequesterId == requesterId &&
                r.TargetId == currentUserId &&
                r.Status == "pending");

            if (request == null) return NotFound("Request not found");

            // ✅ Update request
            request.Status = "accepted";

            // ✅ Add follow
            _context.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                FollowerId = requesterId,
                FollowingId = currentUserId,
                CreatedAt = DateTime.UtcNow
            });

            // 🔥 REMOVE FOLLOW REQUEST NOTIFICATION
            var notification = await _context.Notifications.FirstOrDefaultAsync(n =>
                n.Type == "FollowRequest" &&
                n.UserId == currentUserId &&
                n.RelatedId == requesterId);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
            }

            // ✅ Add accepted notification
            var accepter = await _context.Users.FindAsync(currentUserId);
            var username = accepter?.Username ?? "User";

            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = requesterId,
                Message = $"{username} accepted your follow request",
                Type = "FollowAccepted",
                RelatedId = currentUserId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        // DELETE: api/Follow/decline/{requesterId}
        [HttpDelete("decline/{requesterId}")]
        public async Task<IActionResult> DeclineRequest(Guid requesterId)
        {
            var currentUserId = GetUserId();

            var request = await _context.FollowRequests.FirstOrDefaultAsync(r =>
                r.RequesterId == requesterId &&
                r.TargetId == currentUserId &&
                r.Status == "pending");

            if (request == null) return NotFound("Request not found");

            // ✅ Remove request
            _context.FollowRequests.Remove(request);

            // 🔥 REMOVE FOLLOW REQUEST NOTIFICATION
            var notification = await _context.Notifications
     .Where(n => n.Type == "FollowRequest" &&
                 n.UserId == currentUserId &&
                 n.RelatedId == requesterId)
     .FirstOrDefaultAsync();

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET: api/Follow/followers
        [HttpGet("followers")]
        public async Task<IActionResult> GetFollowers()
        {
            var currentUserId = GetUserId();
            var followers = await _context.Follows
                .Where(f => f.FollowingId == currentUserId)
                .Select(f => new {
                    f.Follower.Id,
                    f.Follower.Username,
                    f.Follower.ProfilePictureUrl,
                    f.CreatedAt
                }).ToListAsync();
            return Ok(followers);
        }

        // GET: api/Follow/following
        [HttpGet("following")]
        public async Task<IActionResult> GetFollowing()
        {
            var currentUserId = GetUserId();
            var following = await _context.Follows
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => new {
                    f.Following.Id,
                    f.Following.Username,
                    f.Following.ProfilePictureUrl,
                    f.CreatedAt
                }).ToListAsync();
            return Ok(following);
        }

        // DELETE: api/Follow/request/{userId}
        [HttpDelete("request/{userId}")]
        public async Task<IActionResult> CancelFollowRequest(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty)
                return Unauthorized();

            var request = await _context.FollowRequests.FirstOrDefaultAsync(r =>
                r.RequesterId == currentUserId &&
                r.TargetId == userId &&
                r.Status == "pending");

            if (request == null)
                return NotFound("Follow request not found");

            // ✅ Remove request
            _context.FollowRequests.Remove(request);

            // 🔥 REMOVE NOTIFICATION FROM TARGET USER
            var notification = await _context.Notifications.FirstOrDefaultAsync(n =>
                n.Type == "FollowRequest" &&
                n.UserId == userId &&
                n.RelatedId == currentUserId);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Follow request cancelled" });
        }

        private Guid GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id))
                throw new UnauthorizedAccessException("Invalid user token");
            return id;
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blog.Backend.Data;
using Blog.Backend.Models;

namespace Blog.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FollowController : ControllerBase
    {
        private readonly BlogDbContext _context;

        public FollowController(BlogDbContext context)
        {
            _context = context;
        }

        [HttpPost("{followerId}/{followingId}")]
        public async Task<IActionResult> Follow(Guid followerId, Guid followingId)
        {
            if (followerId == followingId)
                return BadRequest("You cannot follow yourself");

            var exists = await _context.Follows.AnyAsync(f =>
                f.FollowerId == followerId &&
                f.FollowingId == followingId);

            if (exists)
                return BadRequest("Already following");

            var follow = new Follow
            {
                FollowerId = followerId,
                FollowingId = followingId
            };

            _context.Follows.Add(follow);
            await _context.SaveChangesAsync();

            return Ok("Followed successfully");
        }

        [HttpDelete("{followerId}/{followingId}")]
        public async Task<IActionResult> Unfollow(Guid followerId, Guid followingId)
        {
            var follow = await _context.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == followerId &&
                f.FollowingId == followingId);

            if (follow == null)
                return NotFound();

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();

            return Ok("Unfollowed successfully");
        }
    }
}

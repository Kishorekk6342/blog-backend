// LikeController.cs
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
    public class LikeController : ControllerBase
    {
        private readonly BlogDbContext _context;

        public LikeController(BlogDbContext context)
        {
            _context = context;
        }

        // POST: api/Like/{postId}
        [HttpPost("{postId}")]
        [Authorize]
        public async Task<IActionResult> ToggleLike(Guid postId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var existing = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            if (existing != null)
            {
                // Unlike
                _context.Likes.Remove(existing);
                await _context.SaveChangesAsync();
                return Ok(new { liked = false });
            }
            else
            {
                // Like
                _context.Likes.Add(new Like
                {
                    Id = Guid.NewGuid(),
                    PostId = postId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return Ok(new { liked = true });
            }
        }
    }
}
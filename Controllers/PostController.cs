using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blog.Backend.Data;
using Blog.Backend.Models;
using Blog.Backend.DTOs;
using System.Security.Claims;

namespace Blog.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostController : ControllerBase
    {
        private readonly BlogDbContext _context;
        private readonly ILogger<PostController> _logger;

        public PostController(BlogDbContext context, ILogger<PostController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: api/Post - Create new post
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized("Invalid user token");
                }

                if (string.IsNullOrWhiteSpace(dto.Title))
                    return BadRequest("Title is required");

                if (string.IsNullOrWhiteSpace(dto.Content))
                    return BadRequest("Content is required");

                var now = DateTime.UtcNow;
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = dto.Title.Trim(),
                    Content = dto.Content.Trim(),
                    IsPublic = dto.IsPublic,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Post created successfully",
                    postId = post.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // GET: api/Post/my-posts - Get current user's posts
        [HttpGet("my-posts")]
        [Authorize]
        public async Task<IActionResult> GetMyPosts()
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized("Invalid user token");
                }

                var posts = await _context.Posts
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new PostResponseDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Content = p.Content,
                        IsPublic = p.IsPublic,
                        AuthorId = p.UserId,
                        AuthorName = _context.Users.Where(u => u.Id == p.UserId).Select(u => u.Username).FirstOrDefault() ?? "Unknown",
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        LikesCount = 0,
                        CommentsCount = 0,
                        IsLiked = false
                    })
                    .ToListAsync();

                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting my posts");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // GET: api/Post/user/{userId} - Get user's posts (respects privacy)
        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserPosts(Guid userId)
        {
            try
            {
                var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid? currentUserId = null;

                if (!string.IsNullOrEmpty(currentUserIdClaim) && Guid.TryParse(currentUserIdClaim, out var parsedId))
                {
                    currentUserId = parsedId;
                }

                // Check if current user follows the target user
                bool isFollowing = false;
                if (currentUserId.HasValue)
                {
                    isFollowing = await _context.Follows
                        .AnyAsync(f => f.FollowerId == currentUserId.Value && f.FollowingId == userId);
                }

                // Get posts based on privacy
                var query = _context.Posts.Where(p => p.UserId == userId);

                // If viewing own profile, show all posts
                // If viewing other's profile, show public posts OR private posts if following
                if (currentUserId != userId)
                {
                    query = query.Where(p => p.IsPublic || isFollowing);
                }

                var posts = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new PostResponseDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Content = p.Content,
                        IsPublic = p.IsPublic,
                        AuthorId = p.UserId,
                        AuthorName = _context.Users.Where(u => u.Id == p.UserId).Select(u => u.Username).FirstOrDefault() ?? "Unknown",
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        LikesCount = 0,
                        CommentsCount = 0,
                        IsLiked = false
                    })
                    .ToListAsync();

                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user posts");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // GET: api/Post/feed - Get feed (public posts + own posts + followed users' private posts)
        [HttpGet("feed")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeed(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                Guid? userId = null;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (Guid.TryParse(claim, out var parsed))
                        userId = parsed;
                }

                IQueryable<Post> query = _context.Posts.AsQueryable();

                if (userId == null)
                {
                    // 🔓 Anonymous users → ONLY public posts
                    query = query.Where(p => p.IsPublic);
                }
                else
                {
                    // 🔐 Logged in → Get list of users they follow
                    var followingIds = await _context.Follows
                        .Where(f => f.FollowerId == userId.Value)
                        .Select(f => f.FollowingId)
                        .ToListAsync();

                    // Show posts that are:
                    // 1. Public posts from anyone
                    // 2. Private posts from users you follow
                    // 3. Your own posts (public or private)
                    query = query.Where(p =>
                        p.IsPublic ||                           // All public posts
                        p.UserId == userId.Value ||             // Your own posts
                        (followingIds.Contains(p.UserId))       // Posts from followed users (including their private posts)
                    );
                }

                var posts = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PostResponseDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Content = p.Content,
                        AuthorId = p.UserId,
                        AuthorName = _context.Users
                            .Where(u => u.Id == p.UserId)
                            .Select(u => u.Username)
                            .FirstOrDefault() ?? "Unknown",
                        IsPublic = p.IsPublic,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        LikesCount = 0,
                        CommentsCount = 0,
                        IsLiked = false
                    })
                    .ToListAsync();

                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // GET: api/Post/{id} - Get specific post
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetPost(Guid id)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid? currentUserId = null;

                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedId))
                {
                    currentUserId = parsedId;
                }

                var post = await _context.Posts
                    .Where(p => p.Id == id)
                    .Select(p => new PostResponseDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Content = p.Content,
                        AuthorId = p.UserId,
                        AuthorName = _context.Users.Where(u => u.Id == p.UserId).Select(u => u.Username).FirstOrDefault() ?? "Unknown",
                        IsPublic = p.IsPublic,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        LikesCount = 0,
                        CommentsCount = 0,
                        IsLiked = false
                    })
                    .FirstOrDefaultAsync();

                if (post == null)
                    return NotFound("Post not found");

                // Check if user has permission to view this post
                if (!post.IsPublic && currentUserId != post.AuthorId)
                {
                    // Check if user follows the post author
                    if (!currentUserId.HasValue)
                        return Forbid("You don't have permission to view this post");

                    var isFollowing = await _context.Follows
                        .AnyAsync(f => f.FollowerId == currentUserId.Value && f.FollowingId == post.AuthorId);

                    if (!isFollowing)
                        return Forbid("You don't have permission to view this post");
                }

                return Ok(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // PUT: api/Post/{id} - Update post
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdatePost(Guid id, [FromBody] UpdatePostDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized("Invalid user token");
                }

                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                    return NotFound("Post not found");

                // Only post owner can edit
                if (post.UserId != userId)
                    return Forbid("You can only edit your own posts");

                post.Title = dto.Title.Trim();
                post.Content = dto.Content.Trim();
                post.IsPublic = dto.IsPublic;
                post.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Post updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // DELETE: api/Post/{id} - Delete post
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeletePost(Guid id)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized("Invalid user token");
                }

                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                    return NotFound("Post not found");

                // Only post owner can delete
                if (post.UserId != userId)
                    return Forbid("You can only delete your own posts");

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Post deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }
    }
}
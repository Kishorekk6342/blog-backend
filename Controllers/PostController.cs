using Blog.Backend.Data;
using Blog.Backend.DTOs;
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
                    ImageUrl = dto.ImageUrl,
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
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
                        AuthorName = _context.Users
                            .Where(u => u.Id == p.UserId)
                            .Select(u => u.Username)
                            .FirstOrDefault() ?? "Unknown",

                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        ImageUrl = p.ImageUrl,

                        // 🔥 FIXED
                        LikesCount = _context.Likes.Count(l => l.PostId == p.Id),
                        CommentsCount = _context.Comments.Count(c => c.PostId == p.Id),
                        IsLiked = _context.Likes
                            .Any(l => l.PostId == p.Id && l.UserId == userId)
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

                bool isFollowing = false;

                if (currentUserId.HasValue)
                {
                    isFollowing = await _context.Follows
                        .AnyAsync(f => f.FollowerId == currentUserId.Value && f.FollowingId == userId);
                }

                var query = _context.Posts.Where(p => p.UserId == userId);

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
                        AuthorName = _context.Users
                            .Where(u => u.Id == p.UserId)
                            .Select(u => u.Username)
                            .FirstOrDefault() ?? "Unknown",

                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        ImageUrl = p.ImageUrl,

                        // 🔥 FIXED
                        LikesCount = _context.Likes.Count(l => l.PostId == p.Id),
                        CommentsCount = _context.Comments.Count(c => c.PostId == p.Id),
                        IsLiked = currentUserId.HasValue && _context.Likes
                            .Any(l => l.PostId == p.Id && l.UserId == currentUserId.Value)
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
                    query = query.Where(p => p.IsPublic);
                }
                else
                {
                    var followingIds = await _context.Follows
                        .Where(f => f.FollowerId == userId.Value)
                        .Select(f => f.FollowingId)
                        .ToListAsync();

                    query = query.Where(p =>
                        p.IsPublic ||
                        p.UserId == userId.Value ||
                        followingIds.Contains(p.UserId)
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
                        ImageUrl = p.ImageUrl,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,

                        // 🔥 FIXED
                        LikesCount = _context.Likes.Count(l => l.PostId == p.Id),
                        CommentsCount = _context.Comments.Count(c => c.PostId == p.Id),
                        IsLiked = userId.HasValue && _context.Likes
                            .Any(l => l.PostId == p.Id && l.UserId == userId.Value)
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
                        AuthorName = _context.Users
                            .Where(u => u.Id == p.UserId)
                            .Select(u => u.Username)
                            .FirstOrDefault() ?? "Unknown",

                        IsPublic = p.IsPublic,
                        ImageUrl = p.ImageUrl,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,

                        // 🔥 FIXED
                        LikesCount = _context.Likes.Count(l => l.PostId == p.Id),
                        CommentsCount = _context.Comments.Count(c => c.PostId == p.Id),
                        IsLiked = currentUserId.HasValue && _context.Likes
                            .Any(l => l.PostId == p.Id && l.UserId == currentUserId.Value)
                    })
                    .FirstOrDefaultAsync();

                if (post == null)
                    return NotFound("Post not found");

                return Ok(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post");
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpGet("comments/{postId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComments(Guid postId)
        {
            var comments = await _context.Comments
                .Where(c => c.PostId == postId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Content,
                    c.CreatedAt,
                    c.UserId,
                    Username = _context.Users
                        .Where(u => u.Id == c.UserId)
                        .Select(u => u.Username)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(comments);
        }

        [HttpPost("comment/{postId}")]
        [Authorize]
        public async Task<IActionResult> AddComment(Guid postId, [FromBody] string content)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok();
        }


        [HttpDelete("comment/{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(Guid commentId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
                return NotFound();

            if (comment.UserId != userId)
                return Forbid();

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // PUT: api/Post/{id} - Update post
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdatePost(Guid id, [FromBody] CreatePostDto dto)
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

                // 🔥 IMPORTANT: Update image ONLY if new one is provided
                if (!string.IsNullOrEmpty(dto.ImageUrl))
                {
                    post.ImageUrl = dto.ImageUrl;
                }

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
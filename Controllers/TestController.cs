using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Blog.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    // GET: api/Test/ping - Test if backend is running
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        _logger.LogInformation("Ping endpoint called");
        return Ok(new { message = "Backend is running!", timestamp = DateTime.UtcNow });
    }

    // GET: api/Test/auth - Test if authentication is working
    [HttpGet("auth")]
    [Authorize]
    public IActionResult TestAuth()
    {
        try
        {
            _logger.LogInformation("Auth test endpoint called");

            // Get all claims
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            _logger.LogInformation("Claims found: {ClaimCount}", claims.Count);

            foreach (var claim in claims)
            {
                _logger.LogInformation("Claim - Type: {Type}, Value: {Value}", claim.Type, claim.Value);
            }

            // Try to get the NameIdentifier claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            var nameClaim = User.FindFirst(ClaimTypes.Name)?.Value;

            return Ok(new
            {
                message = "Authentication working!",
                isAuthenticated = User.Identity?.IsAuthenticated ?? false,
                userId = userIdClaim,
                email = emailClaim,
                username = nameClaim,
                allClaims = claims
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auth test");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    // GET: api/Test/claims - Get all user claims (for debugging)
    [HttpGet("claims")]
    [Authorize]
    public IActionResult GetClaims()
    {
        var claims = User.Claims.Select(c => new
        {
            Type = c.Type,
            Value = c.Value
        }).ToList();

        return Ok(new
        {
            isAuthenticated = User.Identity?.IsAuthenticated,
            authenticationType = User.Identity?.AuthenticationType,
            name = User.Identity?.Name,
            claims = claims
        });
    }
}
using Microsoft.AspNetCore.Mvc;

namespace Blog.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostController : ControllerBase
    {
        [HttpGet("public")]
        public IActionResult GetPublicPosts()
        {
            return Ok("Public posts API working");
        }
    }
}

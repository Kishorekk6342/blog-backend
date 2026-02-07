using Microsoft.AspNetCore.Http;

namespace Blog.Backend.DTOs
{
    public class UploadProfilePictureDto
    {
        public IFormFile File { get; set; } = null!;
    }
}

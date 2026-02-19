using System.ComponentModel.DataAnnotations.Schema;

namespace Blog.Backend.Models
{
    [Table("follow_requests")] // ✅ IMPORTANT
    public class FollowRequest
    {
        public Guid Id { get; set; }
        public Guid RequesterId { get; set; }
        public Guid TargetId { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
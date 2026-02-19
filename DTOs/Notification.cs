using System.ComponentModel.DataAnnotations.Schema;

namespace Blog.Backend.Models
{
    [Table("notifications")] // ✅ IMPORTANT
    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Message { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
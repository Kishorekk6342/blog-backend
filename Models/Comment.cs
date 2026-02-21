using System.ComponentModel.DataAnnotations.Schema;

namespace Blog.Backend.Models
{
    [Table("comments")]  // ✅ lowercase
    public class Comment
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public Post Post { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
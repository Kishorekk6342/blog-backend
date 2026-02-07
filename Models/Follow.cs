using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blog.Backend.Models
{
    [Table("follows")]
    public class Follow
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("follower_id")]
        public Guid FollowerId { get; set; }

        [Column("following_id")]
        public Guid FollowingId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User Follower { get; set; } = null!;
        public User Following { get; set; } = null!;
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blog.Backend.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("username")]
    [MaxLength(50)]
    public string Username { get; set; } = "";

    [Required]
    [Column("email")]
    [MaxLength(100)]
    public string Email { get; set; } = "";

    [Required]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = "";

    // Profile fields
    [Column("bio")]
    [MaxLength(500)]
    public string? Bio { get; set; }

    [Column("location")]
    [MaxLength(100)]
    public string? Location { get; set; }

    [Column("website")]
    [MaxLength(200)]
    public string? Website { get; set; }

    [Column("profile_picture_url")]
    [MaxLength(500)]
    public string? ProfilePictureUrl { get; set; }

    // Settings fields
    [Column("email_notifications")]
    public bool EmailNotifications { get; set; } = true;

    [Column("post_notifications")]
    public bool PostNotifications { get; set; } = true;

    [Column("comment_notifications")]
    public bool CommentNotifications { get; set; } = true;

    [Column("private_profile")]
    public bool PrivateProfile { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Post> Posts { get; set; } = new List<Post>();

    // Followers: Users who follow this user
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();

    // Following: Users this user follows
    public ICollection<Follow> Following { get; set; } = new List<Follow>();
}
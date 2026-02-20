    using Blog.Backend.Models;
    using Microsoft.EntityFrameworkCore;

    namespace Blog.Backend.Data
    {
        public class BlogDbContext : DbContext
        {
            public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
            {
            }

            public DbSet<User> Users { get; set; }
            public DbSet<Post> Posts { get; set; }
            public DbSet<Follow> Follows { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<FollowRequest> FollowRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

            // ============ POST CONFIGURATION ============
            modelBuilder.Entity<Post>()
 .HasOne(p => p.User)
 .WithMany(u => u.Posts)   // ✅ EXPLICIT
 .HasForeignKey(p => p.UserId)
 .OnDelete(DeleteBehavior.Cascade);

            // ============ FOLLOW REQUEST CONFIGURATION ============
            modelBuilder.Entity<FollowRequest>()
                .HasOne(r => r.Requester)
                .WithMany()
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FollowRequest>()
                .HasOne(r => r.Target)
                .WithMany()
                .HasForeignKey(r => r.TargetId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============ NOTIFICATION CONFIGURATION ============
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============ FOLLOW CONFIGURATION ============

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)   // users I follow
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany(u => u.Followers)   // users following me
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);
        }
        }
    }
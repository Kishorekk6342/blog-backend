    using System.Text.Json.Serialization;

    namespace Blog.Backend.DTOs

    {

        public class CreatePostDto
        {
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";

            [JsonPropertyName("isPublic")]
            public bool IsPublic { get; set; }
        }

        public class UpdatePostDto
        {
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public bool IsPublic { get; set; }
        }

        public class PostResponseDto
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public bool IsPublic { get; set; }
            public Guid AuthorId { get; set; }
            public string AuthorName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public int LikesCount { get; set; }
            public int CommentsCount { get; set; }
            public bool IsLiked { get; set; }
        }
    }
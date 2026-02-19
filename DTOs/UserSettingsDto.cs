namespace Blog.Backend.DTOs
{
    public class UserSettingsDto
    {
        public string Email { get; set; } = "";
        public bool EmailNotifications { get; set; }
        public bool PostNotifications { get; set; }
        public bool CommentNotifications { get; set; }
        public bool PrivateProfile { get; set; }
    }

    public class NotificationSettingsDto
    {
        public bool EmailNotifications { get; set; }
        public bool PostNotifications { get; set; }
        public bool CommentNotifications { get; set; }
    }

    public class PrivacySettingsDto
    {
        public bool PrivateProfile { get; set; }
    }
}
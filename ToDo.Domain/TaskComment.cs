// Domain/TaskComment.cs
using ToDo.Domain;

public class TaskComment
{
    public int Id { get; set; }

    public int TodoItemId { get; set; }
    public TodoItem TodoItem { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string Body { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // --- Ek dosya (opsiyonel) ---
    public string? FileUrl { get; set; }          // /uploads/comments/{userId}/{name}
    public string? FileContentType { get; set; }  // image/png, application/pdf ...
    public long FileSizeBytes { get; set; }     // 0 ise yok
    public ICollection<TaskCommentAttachment> Attachments { get; set; } = new List<TaskCommentAttachment>();

}

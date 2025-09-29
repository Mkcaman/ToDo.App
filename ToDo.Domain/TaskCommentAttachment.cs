
namespace ToDo.Domain
{
    public class TaskCommentAttachment
    {
        public int Id { get; set; }

        public int TaskCommentId { get; set; }
        public TaskComment TaskComment { get; set; } = null!;

        public string Url { get; set; } = null!;
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? FileTitle { get; set; } // <-- YENİ
    }
}

namespace ToDo.Web.Models
{
    public class TaskCommentVm
    {
        public int Id { get; set; }
        public string UserDisplay { get; set; } = "";
        public string? UserPhotoUrl { get; set; }
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public List<CommentAttachmentVm> Attachments { get; set; } = new();
    }
}
using System.Net.Mail;
using ToDo.Domain;

namespace ToDo.Web.Models

{
    public class TaskPageVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime DueAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsDone { get; set; }
        public int Priority { get; set; }
        public string OwnerDisplay { get; set; } = "";

        public List<AttachmentVm> Attachments { get; set; } = new();
        public string RemainingText { get; set; } = "";
        public bool IsOverdue { get; set; }
        public List<TaskCommentVm> Comments { get; set; } = new();
    }
   
}

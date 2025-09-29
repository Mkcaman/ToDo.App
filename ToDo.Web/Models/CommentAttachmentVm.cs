namespace ToDo.Web.Models
{
    public class CommentAttachmentVm
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string? Title { get; set; } // <-- YENİ
    }
}

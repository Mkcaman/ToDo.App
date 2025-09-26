namespace ToDo.Web.Models
{
    public class AttachmentVm
    {
        public int Id { get; set; }
        public string FileTitle { get; set; } = "";
        public string Url { get; set; } = "";
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
    }
}

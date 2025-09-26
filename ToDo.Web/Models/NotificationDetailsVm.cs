namespace ToDo.Web.Models
{
    public class NotificationDetailsVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string? Url { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}

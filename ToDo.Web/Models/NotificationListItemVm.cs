namespace ToDo.Web.Models
{
    public class NotificationListItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string? Url { get; set; }
        public DateTime CreatedAt { get; set; }   // Db’deki alan adı “CreatedAt”
        public bool IsRead { get; set; }
    }
}

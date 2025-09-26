namespace ToDo.Web.Models
{
    public class HomeIndexVm
    {
        public List<NotificationListItemVm> Last5Notifications { get; set; } = new();
    }
}

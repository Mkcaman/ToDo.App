namespace ToDo.Web.Models
{
    public class UserWithRolesVm
    {
        public string Id { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }
}

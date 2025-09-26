namespace ToDo.Web.Models
{
    public sealed class UserPickVm
    {
        public string Id { get; set; } = default!;
        // Dropdown’da görünen metin
        public string Display { get; set; } = "";
        // Profil fotoğrafı (yoksa fallback)
        public string AvatarUrl { get; set; } = "/assets/images/avatars/user.png";
    }
}


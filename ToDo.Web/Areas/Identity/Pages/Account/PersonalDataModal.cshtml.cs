using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ToDo.Domain;

namespace ToDo.Web.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class PersonalDataModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _users;

        public PersonalDataModel(UserManager<ApplicationUser> users) => _users = users;

        // Sayfada “boþ” görünsün mü bilgisini ve küçük bir önizlemeyi tutuyoruz
        public bool HasAnyCustomData { get; set; }
        public IReadOnlyDictionary<string, string>? PreviewData { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();

            // “Kullanýcý bir þey ekleyene kadar boþ kalsýn” kuralý
            HasAnyCustomData =
                !string.IsNullOrWhiteSpace(u.FirstName) ||
                !string.IsNullOrWhiteSpace(u.LastName) ||
                !string.IsNullOrWhiteSpace(u.PhoneNumber) ||
                !string.IsNullOrWhiteSpace(u.ProfilePhotoPath) ||
                !string.IsNullOrWhiteSpace(u.AboutText) ||
                !string.IsNullOrWhiteSpace(u.AboutDocPath);

            if (HasAnyCustomData)
            {
                var dict = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(u.FirstName)) dict["FirstName"] = u.FirstName!;
                if (!string.IsNullOrWhiteSpace(u.LastName)) dict["LastName"] = u.LastName!;
                if (!string.IsNullOrWhiteSpace(u.PhoneNumber)) dict["PhoneNumber"] = u.PhoneNumber!;
                if (!string.IsNullOrWhiteSpace(u.ProfilePhotoPath)) dict["ProfilePhotoPath"] = u.ProfilePhotoPath!;
                if (!string.IsNullOrWhiteSpace(u.AboutText)) dict["AboutText"] = u.AboutText!;
                if (!string.IsNullOrWhiteSpace(u.AboutDocPath)) dict["AboutDocPath"] = u.AboutDocPath!;
                PreviewData = dict;
            }

            return Page();
        }

        // JSON indirme – yalnýzca dolu olan özel alanlarý ekle (email/2FA eklenmez)
        public async Task<IActionResult> OnPostDownloadAsync()
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();

            var data = new Dictionary<string, object?>();

            void AddIf(string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    data[key] = value; // null veya boþsa hiç ekleme
            }

            AddIf("FirstName", u.FirstName);
            AddIf("LastName", u.LastName);
            AddIf("PhoneNumber", u.PhoneNumber);
            AddIf("ProfilePhotoPath", u.ProfilePhotoPath);
            AddIf("AboutText", u.AboutText);
            AddIf("AboutDocPath", u.AboutDocPath);

            // E-posta burada özellikle eklenmiyor; profilde sadece görüntüleniyor.
            // 2FA / Authenticator vs. kesinlikle eklenmiyor.

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", "PersonalData.json");
        }
    }
}

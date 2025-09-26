using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace ToDo.Domain
{
    /// <summary>Uygulama kullanıcısı (IdentityUser genişletilmiş).</summary>
    public class ApplicationUser : IdentityUser
    {
        // Kayıt & profil
        public string? FirstName { get; set; }   // Ad
        public string? LastName { get; set; }   // Soyad

        // Profil görselleri/dosyaları
        public string? ProfilePhotoPath { get; set; }    // /uploads/profile/{uid}/photo_xxx.jpg
        public string? AboutText { get; set; }    // Hakkında metni (kısa açıklama)
        public string? AboutDocPath { get; set; }    // /uploads/profile/{uid}/about_doc_xxx.pdf

        // Bildirim e-postası (opsiyonel)
        public string? NotificationEmail { get; set; }
        public bool NotificationEmailConfirmed { get; set; }

        // Görünen ad
        public string DisplayName =>
            string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
                ? (UserName ?? Email ?? "Kullanıcı")
                : $"{FirstName} {LastName}".Trim();
    }
}


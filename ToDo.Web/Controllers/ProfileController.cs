using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ToDo.Domain;

namespace ToDo.Web.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailSender _email;

        // Upload kuralları
        private static readonly string[] ImageExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] ImageTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

        private static readonly string[] DocExt = new[] { ".pdf", ".doc", ".docx", ".txt" };
        private static readonly string[] DocTypes = new[]
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain"
        };
        private const long MaxDocBytes = 10 * 1024 * 1024; // 10 MB

        public ProfileController(
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn,
            IWebHostEnvironment env,
            IEmailSender email)
        {
            _users = users;
            _signIn = signIn;
            _env = env;
            _email = email;
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();
            return View(u);
        }

        /// <summary>Genel bilgiler + fotoğraf + hakkında dökümanı (telefon/e-posta hariç)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string? firstName,
            string? lastName,
            string? aboutText,
            IFormFile? profilePhoto,
            IFormFile? aboutDoc)
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();

            u.FirstName = firstName?.Trim();
            u.LastName = lastName?.Trim();
            u.AboutText = string.IsNullOrWhiteSpace(aboutText) ? null : aboutText.Trim();

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var root = Path.Combine(webRoot, "uploads", "profile", u.Id);
            Directory.CreateDirectory(root);

            // Profil fotoğrafı
            if (profilePhoto is not null && profilePhoto.Length > 0)
            {
                var savedRel = await SaveFileAsync(
                    profilePhoto, root, "photo",
                    ImageExt, ImageTypes, MaxImageBytes);

                if (savedRel is not null)
                {
                    DeletePhysicalIfExists(webRoot, u.ProfilePhotoPath);
                    u.ProfilePhotoPath = $"/uploads/profile/{u.Id}/{savedRel}";
                }
                else
                {
                    TempData["err"] = "Profil fotoğrafı için yalnızca jpg/png/gif/webp ve max 5MB yükleyin.";
                }
            }

            // Hakkında dökümanı
            if (aboutDoc is not null && aboutDoc.Length > 0)
            {
                var savedRel = await SaveFileAsync(
                    aboutDoc, root, "about",
                    DocExt, DocTypes, MaxDocBytes);

                if (savedRel is not null)
                {
                    DeletePhysicalIfExists(webRoot, u.AboutDocPath);
                    u.AboutDocPath = $"/uploads/profile/{u.Id}/{savedRel}";
                }
                else
                {
                    TempData["err"] = "Hakkında dökümanı için pdf/doc/docx/txt ve max 10MB yükleyin.";
                }
            }

            var res = await _users.UpdateAsync(u);
            if (res.Succeeded)
            {
                await _signIn.RefreshSignInAsync(u); // claims/cookie güncel
                TempData["ok"] = "Profil güncellendi.";
            }
            else
            {
                TempData["err"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Edit));
        }

        /// <summary>Profil fotoğrafını kaldır.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto()
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            DeletePhysicalIfExists(webRoot, u.ProfilePhotoPath);
            u.ProfilePhotoPath = null;

            var res = await _users.UpdateAsync(u);
            if (res.Succeeded)
            {
                await _signIn.RefreshSignInAsync(u);
                TempData["ok"] = "Profil fotoğrafı kaldırıldı.";
            }
            else
            {
                TempData["err"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }
            return RedirectToAction(nameof(Edit));
        }

        /// <summary>Telefon değişikliği: mevcut şifre doğruysa günceller.
        /// Giriş: 10 haneli TR numara (+90/0 yazılsa da normalize edilir).
        /// Kaydedilirken “123 456 78 90” formatında tutulur.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePhone(string newPhoneNumber, string currentPassword)
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                TempData["err"] = "Telefon güncellemek için şifrenizi girin.";
                return RedirectToAction(nameof(Edit));
            }

            var okPass = await _signIn.CheckPasswordSignInAsync(u, currentPassword, lockoutOnFailure: false);
            if (!okPass.Succeeded)
            {
                TempData["err"] = "Şifre yanlış.";
                return RedirectToAction(nameof(Edit));
            }

            // Boş ise sil
            if (string.IsNullOrWhiteSpace(newPhoneNumber))
            {
                u.PhoneNumber = null;
            }
            else
            {
                // Normalize: sadece rakamlar, 10 haneye indir; baştaki +90/90/0 kaldırılır.
                var normalized = NormalizeTrPhoneTo10Digits(newPhoneNumber);
                if (normalized is null)
                {
                    TempData["err"] = "Telefon numarası 10 haneli olmalıdır (örn: 5XX XXX XX XX).";
                    return RedirectToAction(nameof(Edit));
                }

                // Görünüm için istenen format: 3 3 2 2 (boşluklu)
                var pretty = $"{normalized[..3]} {normalized.Substring(3, 3)} {normalized.Substring(6, 2)} {normalized.Substring(8, 2)}";

                // Tekillik (başka kullanıcıda var mı?)
                var exists = await _users.Users
                    .AnyAsync(x => x.Id != u.Id && x.PhoneNumber == pretty);
                if (exists)
                {
                    TempData["err"] = "Bu telefon numarası başka bir hesap tarafından kullanılıyor.";
                    return RedirectToAction(nameof(Edit));
                }

                u.PhoneNumber = pretty; // veritabanına boşluklu formatla yaz
            }

            var res = await _users.UpdateAsync(u);
            TempData[res.Succeeded ? "ok" : "err"] =
                res.Succeeded ? "Telefon güncellendi." :
                                string.Join("; ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Edit));
        }

        /// <summary>Şifre değişimi için e-postaya sıfırlama bağlantısı gönderir.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPasswordReset()
        {
            var u = await _users.GetUserAsync(User);
            if (u is null || string.IsNullOrWhiteSpace(u.Email))
            {
                TempData["err"] = "Şifre sıfırlama için geçersiz kullanıcı.";
                return RedirectToAction(nameof(Edit));
            }

            var token = await _users.GeneratePasswordResetTokenAsync(u);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var callbackUrl = Url.Page(
                pageName: "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encodedCode, email = u.Email },
                protocol: Request.Scheme);

            var html = $@"
<p>Merhaba {u.DisplayName},</p>
<p>Şifreni değiştirmek için aşağıdaki bağlantıya tıkla:</p>
<p><a href=""{callbackUrl}"">Şifreyi Sıfırla</a></p>
<small>Eğer bu talebi sen yapmadıysan yok sayabilirsin.</small>";

            await _email.SendEmailAsync(u.Email!, "Şifre Sıfırlama", html);

            // Bilgi sızıntısını önlemek için genel mesaj
            TempData["ok"] = "Eğer hesabın varsa e-posta gönderildi.";
            return RedirectToAction(nameof(Edit));
        }

        /// <summary>Oturumu kapat.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            TempData["ok"] = "Oturum kapatıldı.";
            return RedirectToAction("Index", "Home");
        }

        /// <summary>Hesabı sil (parola doğrulaması ile). İlgili yüklenen dosyaları da temizler.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(string currentPassword)
        {
            var u = await _users.GetUserAsync(User);
            if (u is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                TempData["err"] = "Hesabı silmek için şifrenizi girin.";
                return RedirectToAction(nameof(Edit));
            }

            var okPass = await _signIn.CheckPasswordSignInAsync(u, currentPassword, lockoutOnFailure: false);
            if (!okPass.Succeeded)
            {
                TempData["err"] = "Şifre yanlış.";
                return RedirectToAction(nameof(Edit));
            }

            // Kullanıcıya ait yüklenen dosyaları temizle (profil ve görevler)
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            TryDeleteDirectory(Path.Combine(webRoot, "uploads", "profile", u.Id));
            TryDeleteDirectory(Path.Combine(webRoot, "uploads", "tasks", u.Id));

            var res = await _users.DeleteAsync(u);
            if (res.Succeeded)
            {
                await _signIn.SignOutAsync();
                TempData["ok"] = "Hesabınız silindi.";
                return RedirectToAction("Index", "Home");
            }

            TempData["err"] = string.Join("; ", res.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Edit));
        }

        // ---------------- Helpers ----------------
        private static void DeletePhysicalIfExists(string webRoot, string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath)) return;
            var phys = Path.Combine(webRoot, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(phys))
            {
                try { System.IO.File.Delete(phys); } catch { /* swallow */ }
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch { /* loglamak istersen burayı doldurabilirsin */ }
        }

        private static async Task<string?> SaveFileAsync(
            IFormFile file,
            string destRoot,
            string namePrefix,
            string[] allowedExt,
            string[] allowedContentTypes,
            long maxBytes,
            CancellationToken ct = default)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var typeOk = allowedContentTypes.Contains(file.ContentType);
            var extOk = allowedExt.Contains(ext);
            var sizeOk = file.Length <= maxBytes;

            if (!(extOk && typeOk && sizeOk)) return null;

            Directory.CreateDirectory(destRoot);
            var fileName = $"{namePrefix}_{Guid.NewGuid():N}{ext}";
            var phys = Path.Combine(destRoot, fileName);
            using var fs = new FileStream(phys, FileMode.Create);
            await file.CopyToAsync(fs, ct);
            return fileName; // uploads/profile/{userId}/{fileName}
        }

        /// <summary>
        /// Kullanıcı girişini normalize eder:
        /// - Yalnızca rakamlar alınır
        /// - Başındaki +90 / 90 / 0 temizlenir
        /// - Geriye 10 hane kalmalı; aksi halde null döner
        /// </summary>
        private static string? NormalizeTrPhoneTo10Digits(string input)
        {
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("90") && digits.Length >= 12)
                digits = digits.Substring(digits.Length - 10); // son 10
            else if (digits.StartsWith("0") && digits.Length >= 11)
                digits = digits.Substring(digits.Length - 10);
            // zaten 10 haneliyse bırak
            if (digits.Length == 10) return digits;
            return null;
        }
    }
}





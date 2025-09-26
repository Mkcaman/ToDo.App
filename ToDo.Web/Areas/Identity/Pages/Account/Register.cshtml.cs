using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore; // <- AnyAsync için
using Microsoft.Extensions.Logging;
using ToDo.Domain;

namespace ToDo.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Ad zorunludur."), Display(Name = "Ad")]
            public string FirstName { get; set; } = "";

            [Required(ErrorMessage = "Soyad zorunludur."), Display(Name = "Soyad")]
            public string LastName { get; set; } = "";

            [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress, Display(Name = "E-posta")]
            public string Email { get; set; } = "";

            // Telefonu zorunlu yapmak istersen: [Required(ErrorMessage="Telefon zorunludur.")]
            [Phone, Display(Name = "Telefon")]
            public string? PhoneNumber { get; set; }

            [Required(ErrorMessage = "Şifre zorunludur."), DataType(DataType.Password), Display(Name = "Şifre")]
            public string Password { get; set; } = "";

            [DataType(DataType.Password), Display(Name = "Şifre (tekrar)")]
            [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; } = "";
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl ??= Url.Content("~/");
            returnUrl ??= ReturnUrl;

            // Sunucu tarafı telefon normalizasyonu + tekillik (opsiyonel ama önerilir)
            string? prettyPhone = null;
            if (!string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                var digits = NormalizeTrPhoneTo10Digits(Input.PhoneNumber!);
                if (digits is null)
                {
                    ModelState.AddModelError("Input.PhoneNumber", "Telefon numarası 10 haneli olmalıdır (örn: 5XX XXX XX XX).");
                }
                else
                {
                    prettyPhone = $"{digits[..3]} {digits.Substring(3, 3)} {digits.Substring(6, 2)} {digits.Substring(8, 2)}";

                    // DB tarafında unique index var; ama daha güzel bir hata için ön-kontrol yapıyoruz
                    var exists = await _userManager.Users.AnyAsync(u => u.PhoneNumber == prettyPhone);
                    if (exists)
                        ModelState.AddModelError("Input.PhoneNumber", "Bu telefon numarası başka bir hesap tarafından kullanılıyor.");
                }
            }

            if (!ModelState.IsValid)
                return Page();

            var user = new ApplicationUser
            {
                UserName = Input.Email.Trim(),
                Email = Input.Email.Trim(),
                FirstName = Input.FirstName.Trim(),
                LastName = Input.LastName.Trim(),
                PhoneNumber = prettyPhone // null ya da "123 456 78 90" biçimi
            };

            var createResult = await _userManager.CreateAsync(user, Input.Password);

            if (createResult.Succeeded)
            {
                // varsayılan rol
                var roleResult = await _userManager.AddToRoleAsync(user, "Employee");
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                    foreach (var e in roleResult.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);
                    return Page();
                }

                _logger.LogInformation("Yeni kullanıcı oluşturuldu ve Employee rolü verildi: {Email}", user.Email);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        /// <summary>
        /// Yalnızca rakamları alır, baştaki +90/90/0’ı düşer ve geriye tam 10 hane kalırsa döndürür; aksi halde null.
        /// </summary>
        private static string? NormalizeTrPhoneTo10Digits(string input)
        {
            var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
            // +90/90/0 at
            if (digits.StartsWith("90") && digits.Length >= 12)
                digits = digits[^10..]; // son 10
            else if (digits.StartsWith("0") && digits.Length >= 11)
                digits = digits[^10..];

            if (digits.Length == 10) return digits;
            return null;
        }
    }
}

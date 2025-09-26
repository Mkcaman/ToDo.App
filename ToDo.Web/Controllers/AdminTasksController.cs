using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToDo.Domain;
using ToDo.Infrastructure;
using ToDo.Web.Models;

namespace ToDo.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminTasksController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailSender _email;

        public AdminTasksController(
            AppDbContext db,
            UserManager<ApplicationUser> users,
            IWebHostEnvironment env,
            IEmailSender email)
        {
            _db = db;
            _users = users;
            _env = env;
            _email = email;
        }

        // ---------------- LISTE (değişmedi)
        public async Task<IActionResult> Index(string? status, string? q)
        {
            var now = DateTime.UtcNow;
            var query = _db.TodoItems.Include(t => t.Owner).AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var ql = q.Trim().ToLowerInvariant();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(ql) ||
                    (((t.Owner!.FirstName ?? "") + " " + (t.Owner!.LastName ?? "")).ToLower().Contains(ql)) ||
                    (t.Owner!.Email ?? "").ToLower().Contains(ql));
            }

            query = status switch
            {
                "active" => query.Where(t => !t.IsDone && t.DueAt > now),
                "overdue" => query.Where(t => !t.IsDone && t.DueAt <= now),
                "done" => query.Where(t => t.IsDone),
                _ => query
            };

            var raw = await query
                .OrderBy(t => t.IsDone).ThenBy(t => t.DueAt).ThenBy(t => t.Id)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    FirstName = t.Owner!.FirstName,
                    LastName = t.Owner!.LastName,
                    OwnerEmail = t.Owner!.Email!,
                    AssignedAt = t.CreatedAt,
                    DueAt = t.DueAt,
                    t.IsDone,
                    Priority = t.Priority,
                    Attachment = t.AttachmentPath
                })
                .ToListAsync();

            var list = raw.Select(x =>
            {
                var ownerName = string.Join(" ", new[] { x.FirstName, x.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var isOverdue = !x.IsDone && x.DueAt <= DateTime.UtcNow;
                var ts = x.DueAt - DateTime.UtcNow;
                var remaining = ts.TotalSeconds <= 0 ? "Süre doldu" : Format(ts);

                return new AdminTaskRowVm
                {
                    Id = x.Id,
                    Title = x.Title,
                    OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "-" : ownerName,
                    OwnerEmail = x.OwnerEmail,
                    AssignedAtUtc = x.AssignedAt,
                    DueAtUtc = x.DueAt,
                    IsDone = x.IsDone,
                    IsOverdue = isOverdue,
                    RemainingText = remaining,
                    Priority = x.Priority,
                    AttachmentUrl = x.Attachment
                };
            }).ToList();

            ViewBag.Status = status;
            ViewBag.Q = q;
            return View(list);
        }

        private static string Format(TimeSpan ts)
        {
            var parts = new List<string>();
            if (ts.Days > 0) parts.Add($"{ts.Days}g");
            if (ts.Hours > 0) parts.Add($"{ts.Hours}s");
            if (ts.Minutes > 0) parts.Add($"{ts.Minutes}d");
            if (parts.Count == 0) parts.Add($"{Math.Max(0, ts.Seconds)}sn");
            return string.Join(" ", parts);
        }

        // ---------------- CREATE
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new AdminTaskCreateVm
            {
                PriorityScore = 50,
                // Form local zamanı alır → yarın 00:00 (local)
                DueAtUtc = DateTime.Now.Date.AddDays(1)
            };
            var users = await GetAssignableUsersAsync();
            ViewBag.AssignableUsers = users;
            ViewBag.Users = users;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminTaskCreateVm m)
        {
            // 1) Formdan gelen değeri local kabul et → UTC yaz
            var dueLocal = DateTime.SpecifyKind(m.DueAtUtc, DateTimeKind.Local);
            var dueUtc = dueLocal.ToUniversalTime();

            var minLocal = DateTime.Now.Date.AddDays(1);
            var minUtc = TimeZoneInfo.ConvertTimeToUtc(minLocal);
            if (dueUtc < minUtc)
                ModelState.AddModelError(nameof(m.DueAtUtc), "Son tarih yarın 00:00 veya sonrası olmalı.");

            if (!ModelState.IsValid)
            {
                var usersErr = await GetAssignableUsersAsync();
                ViewBag.AssignableUsers = usersErr; ViewBag.Users = usersErr;
                return View(m);
            }

            var owner = await _users.FindByIdAsync(m.OwnerId);
            if (owner is null)
            {
                ModelState.AddModelError(nameof(m.OwnerId), "Kullanıcı bulunamadı.");
                var usersErr = await GetAssignableUsersAsync();
                ViewBag.AssignableUsers = usersErr; ViewBag.Users = usersErr;
                return View(m);
            }
            if (await _users.IsInRoleAsync(owner, "Admin"))
            {
                ModelState.AddModelError(nameof(m.OwnerId), "Admin bir kullanıcıya görev atanamaz.");
                var usersErr = await GetAssignableUsersAsync();
                ViewBag.AssignableUsers = usersErr; ViewBag.Users = usersErr;
                return View(m);
            }

            var item = new TodoItem
            {
                OwnerId = owner.Id,
                Title = m.Title.Trim(),
                Description = (m.Description?? "").Trim(),
                DueAt = dueUtc,                   // DB'ye her zaman UTC
                Priority = Math.Clamp(m.PriorityScore, 0, 100),
                CreatedAt = DateTime.UtcNow
            };

            _db.TodoItems.Add(item);
            await _db.SaveChangesAsync(); // Id lazım

            // ---- Çoklu dosya (varsa) ----
            var files = m.Attachments ?? new List<IFormFile>();
            var titles = m.AttachmentTitles ?? new List<string>();

            if (files.Count > 0)
            {
                var okExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".zip" };

                var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var dir = Path.Combine(root, "uploads", "tasks", owner.Id, item.Id.ToString());
                Directory.CreateDirectory(dir);

                for (int i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    if (f is not { Length: > 0 }) continue;

                    var ext = Path.GetExtension(f.FileName);
                    if (string.IsNullOrWhiteSpace(ext) || !okExt.Contains(ext)) continue;

                    var stored = $"{Guid.NewGuid():N}{ext}";
                    var phys = Path.Combine(dir, stored);
                    using (var fs = new FileStream(phys, FileMode.Create))
                        await f.CopyToAsync(fs);

                    var title = (i < titles.Count ? titles[i] : null)?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                        title = Path.GetFileNameWithoutExtension(f.FileName);

                    _db.TaskAttachments.Add(new TaskAttachment
                    {
                        TodoItemId = item.Id,
                        FileTitle = title,
                        FileNameOriginal = f.FileName,
                        Url = $"/uploads/tasks/{owner.Id}/{item.Id}/{stored}",
                        ContentType = f.ContentType,
                        SizeBytes = f.Length
                    });
                }
                await _db.SaveChangesAsync();
            }

            // ---- (1) UYGULAMA-İÇİ BİLDİRİM ----
            try
            {
                var dueLocalTxt = dueUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                _db.Notifications.Add(new Notification
                {
                    UserId = owner.Id,
                    Title = "Yeni Görev Atandı",
                    Message = $"“{item.Title}” (son tarih: {dueLocalTxt})",
                    Url = $"/Tasks/Page/{item.Id}", // sende varsa detay sayfasına götür
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    // İstersen Category/Severity alanların varsa doldur
                });
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Notifications tablosu yoksa sessiz geç
            }

            // ---- (2) E-POSTA ----
            if (!string.IsNullOrWhiteSpace(owner.Email))
            {
                var pageUrl = Url.Action("Page", "Tasks", new { id = item.Id }, Request.Scheme);
                var subject = "Yeni Görev Ataması";
                var dueLocalText = dueUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                var body = $@"
<p>Merhaba {owner.DisplayName},</p>
<p>Size yeni bir görev atandı:</p>
<ul>
  <li><strong>Başlık:</strong> {item.Title}</li>
  <li><strong>Son Tarih:</strong> {dueLocalText}</li>
  <li><strong>Öncelik:</strong> {item.Priority}</li>
</ul>
<p><a href=""{pageUrl}"">Görev sayfasını aç</a></p>
<p>Detaylar için uygulamaya giriş yapabilirsiniz.</p>";
                try { await _email.SendEmailAsync(owner.Email!, subject, body); } catch { /* logla istersen */ }

            }

            TempData["ok"] = "Görev atandı.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- INLINE DELETE (değişmedi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteInline(int id, string? status, string? q)
        {
            var t = await _db.TodoItems.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id);
            if (t is null)
            {
                TempData["err"] = "Görev bulunamadı.";
                return RedirectToAction(nameof(Index), new { status, q });
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            DeletePhysicalIfExists(webRoot, t.AttachmentPath);

            try
            {
                var atts = await _db.TaskAttachments.Where(a => a.TodoItemId == t.Id).ToListAsync();
                foreach (var a in atts) DeletePhysicalIfExists(webRoot, a.Url);
                _db.TaskAttachments.RemoveRange(atts);
            }
            catch { }

            var taskDir = Path.Combine(webRoot, "uploads", "tasks", t.OwnerId, t.Id.ToString());
            TryDeleteDirectory(taskDir);

            _db.TodoItems.Remove(t);
            await _db.SaveChangesAsync();

            TempData["ok"] = "Görev silindi.";
            return RedirectToAction(nameof(Index), new { status, q });
        }

        // -------- JSON (değişmedi)
        [HttpGet]
        public async Task<IActionResult> AssignableUsersJson()
            => Json(await GetAssignableUsersAsync());

        private async Task<List<UserPickVm>> GetAssignableUsersAsync()
        {
            var adminIds = (await _users.GetUsersInRoleAsync("Admin")).Select(a => a.Id).ToHashSet();

            var raw = await _users.Users
                .Where(u => !adminIds.Contains(u.Id))
                .OrderBy(u => u.Email)
                .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, u.ProfilePhotoPath })
                .ToListAsync();

            static string NormalizeAvatar(string? p)
            {
                if (string.IsNullOrWhiteSpace(p)) return "/assets/images/avatars/user.png";
                var s = p.Trim();
                if (s.StartsWith("~/")) s = s[1..];
                if (!s.StartsWith("/")) s = "/" + s;
                return s;
            }

            var list = new List<UserPickVm>();
            foreach (var u in raw)
            {
                var full = $"{(u.FirstName ?? "").Trim()} {(u.LastName ?? "").Trim()}".Trim();
                list.Add(new UserPickVm
                {
                    Id = u.Id,
                    Display = string.IsNullOrWhiteSpace(full) ? (u.Email ?? u.Id) : full,
                    AvatarUrl = NormalizeAvatar(u.ProfilePhotoPath)
                });
            }
            return list;
        }

        // ---- Helpers (değişmedi)
        private static void DeletePhysicalIfExists(string webRoot, string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath)) return;
            var phys = Path.Combine(webRoot, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(phys))
            {
                try { System.IO.File.Delete(phys); } catch { }
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch { }
        }
    }
}

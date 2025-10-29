using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using ToDo.Domain;
using ToDo.Infrastructure;
using ToDo.Web.Models; // TaskPageVm, TaskCommentVm, CommentAttachmentVm

namespace ToDo.Web.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IWebHostEnvironment _env;

    public TasksController(AppDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _env = env;
    }

    // ---------------- LIST ----------------
    // sort: "due" | "priority" | "title_az" | "title_za"
    public async Task<IActionResult> Index(string? q, string? filter, string? sort = "due")
    {
        var user = await _users.GetUserAsync(User);
        var userId = user!.Id;

        var nowUtc = DateTime.UtcNow;
        var localToday = DateTime.Now.Date;
        var localTomorrow = localToday.AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localToday);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localTomorrow);

        var query = _db.TodoItems.Where(t => t.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(t => t.Title.Contains(q));
        }

        query = filter switch
        {
            "active" => query.Where(t => !t.IsDone),
            "overdue" => query.Where(t => !t.IsDone && t.DueAt < nowUtc),
            "today" => query.Where(t => t.DueAt >= startUtc && t.DueAt < endUtc),
            "done" => query.Where(t => t.IsDone),
            _ => query
        };

        // ---- SIRALAMA ----
        Expression<Func<TodoItem, bool>> isOverdueExpr =
            t => !t.IsDone && t.DueAt != null && t.DueAt < nowUtc;

        query = (sort?.ToLowerInvariant()) switch
        {
            "priority" => query
                .OrderBy(isOverdueExpr)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.Id),

            "title_az" => query
                .OrderBy(isOverdueExpr)
                .ThenBy(t => t.Title)
                .ThenBy(t => t.Id),

            "title_za" => query
                .OrderBy(isOverdueExpr)
                .ThenByDescending(t => t.Title)
                .ThenBy(t => t.Id),

            _ => query // "due"
                .OrderBy(isOverdueExpr)
                .ThenBy(t => t.DueAt == null)
                .ThenBy(t => t.DueAt)
                .ThenBy(t => t.Id)
        };

        var items = await query.AsNoTracking().ToListAsync();

        ViewBag.Q = q;
        ViewBag.Filter = filter;
        ViewBag.Sort = sort?.ToLowerInvariant() ?? "due";
        ViewBag.SortOptions = new[]
        {
            new { Value = "due",       Text = "En yakın son tarih" },
            new { Value = "priority",  Text = "En büyük öncelik"   },
            new { Value = "title_az",  Text = "Başlık A'dan Z'ye"  },
            new { Value = "title_za",  Text = "Başlık Z'den A'ya"  },
        };

        return View(items);
    }

    // ---------------- TASK PAGE (sadece sahibi veya Admin) ----------------
    [HttpGet]
    public async Task<IActionResult> Page(int id)
    {
        var uid = _users.GetUserId(User)!;

        var t = await _db.TodoItems
            .Include(x => x.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t is null) return NotFound();

        if (t.OwnerId != uid && !User.IsInRole("Admin"))
            return Forbid();

        // Ekler
        var atts = new List<AttachmentVm>();
        try
        {
            atts = await _db.TaskAttachments
                .Where(a => a.TodoItemId == t.Id)
                .AsNoTracking()
                .Select(a => new AttachmentVm
                {
                    Id = a.Id,
                    FileTitle = string.IsNullOrWhiteSpace(a.FileTitle)
                        ? System.IO.Path.GetFileNameWithoutExtension(a.FileNameOriginal)
                        : a.FileTitle,
                    Url = a.Url,
                    ContentType = a.ContentType,
                    SizeBytes = a.SizeBytes
                })
                .ToListAsync();
        }
        catch { /* tablo yoksa sessiz geç */ }

        if (atts.Count == 0 && !string.IsNullOrWhiteSpace(t.AttachmentPath))
        {
            atts.Add(new AttachmentVm
            {
                Id = 0,
                FileTitle = System.IO.Path.GetFileName(t.AttachmentPath),
                Url = t.AttachmentPath,
                ContentType = null,
                SizeBytes = 0
            });
        }

        // ----- Yorumlar (+ çoklu ekler & başlık) -----
        var comments = await _db.TaskComments
            .Where(c => c.TodoItemId == t.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TaskCommentVm
            {
                Id = c.Id,
                UserDisplay = c.User.DisplayName ?? c.User.UserName,
                UserPhotoUrl = c.User.ProfilePhotoPath, // avatar
                Body = c.Body,
                CreatedAt = c.CreatedAt,
                Attachments = c.Attachments
                    .Select(a => new CommentAttachmentVm
                    {
                        Id = a.Id,
                        Url = a.Url,
                        ContentType = a.ContentType,
                        SizeBytes = a.SizeBytes,
                        Title = a.FileTitle
                    }).ToList()
            })
            .AsNoTracking()
            .ToListAsync();

        var now = DateTime.UtcNow;
        var ts = t.DueAt - now;

        var vm = new TaskPageVm
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DueAtUtc = t.DueAt,
            IsDone = t.IsDone,
            Priority = t.Priority,
            OwnerDisplay = t.Owner?.DisplayName ?? "-",
            Attachments = atts,
            IsOverdue = !t.IsDone && t.DueAt <= now,
            RemainingText = ts.TotalSeconds <= 0 ? "Süre doldu" : Format(ts),
            Comments = comments
        };

        return View("Page", vm);
    }

    // ----- Yorum ekleme (+ @mention & ÇOKLU dosya & başlık) -----
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int taskId, string body, List<IFormFile>? commentFiles, List<string>? commentFileTitles)
    {
        var me = await _users.GetUserAsync(User);
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["err"] = "Yorum boş olamaz.";
            return RedirectToAction(nameof(Page), new { id = taskId });
        }

        var t = await _db.TodoItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId);
        if (t is null) return NotFound();

        // yalnız sahibi veya Admin yorum eklesin (istersen gevşetebilirsin)
        if (t.OwnerId != me!.Id && !User.IsInRole("Admin"))
            return Forbid();

        var trimmed = body.Trim();

        var comment = new TaskComment
        {
            TodoItemId = t.Id,
            UserId = me.Id,
            Body = trimmed,
            CreatedAt = DateTime.UtcNow
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync(); // comment.Id için

        if (commentFiles is { Count: > 0 })
        {
            for (int i = 0; i < commentFiles.Count; i++)
            {
                var file = commentFiles[i];
                if (file is null || file.Length <= 0) continue;

                var saved = await SaveCommentAttachmentAsync(me.Id, file);
                if (saved == null) continue;

                var (url, contentType, size) = saved.Value;

                // aynı indeksli başlık
                string? title = null;
                if (commentFileTitles != null && i < commentFileTitles.Count)
                    title = string.IsNullOrWhiteSpace(commentFileTitles[i]) ? null : commentFileTitles[i].Trim();

                if (string.IsNullOrWhiteSpace(title))
                    title = System.IO.Path.GetFileNameWithoutExtension(file.FileName);

                _db.TaskCommentAttachments.Add(new TaskCommentAttachment
                {
                    TaskCommentId = comment.Id,
                    Url = url,
                    ContentType = contentType,
                    SizeBytes = size,
                    FileTitle = title,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync();
        }

        await CreateMentionNotificationsAsync(t, me, trimmed);

        TempData["ok"] = "Yorum eklendi.";
        return RedirectToAction(nameof(Page), new { id = taskId });
    }

    // ----- Yorum düzenleme -----
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(int id, string body)
    {
        var me = await _users.GetUserAsync(User);
        var c = await _db.TaskComments
            .Include(x => x.TodoItem)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var isOwnerOrAdmin = c.UserId == me!.Id || User.IsInRole("Admin");
        if (!isOwnerOrAdmin) return Forbid();

        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["err"] = "Yorum boş olamaz.";
            return RedirectToAction(nameof(Page), new { id = c.TodoItemId });
        }

        c.Body = body.Trim();
        await _db.SaveChangesAsync();

        TempData["ok"] = "Yorum güncellendi.";
        return RedirectToAction(nameof(Page), new { id = c.TodoItemId });
    }

    // ----- Yorum silme (eklerle birlikte) -----
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var me = await _users.GetUserAsync(User);
        var c = await _db.TaskComments
            .Include(x => x.Attachments)
            .Include(x => x.TodoItem)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var isOwnerOrAdmin = c.UserId == me!.Id || User.IsInRole("Admin");
        if (!isOwnerOrAdmin) return Forbid();

        foreach (var att in c.Attachments)
            DeletePhysicalFileIfExists(att.Url);

        _db.TaskComments.Remove(c);
        await _db.SaveChangesAsync();

        TempData["ok"] = "Yorum silindi.";
        return RedirectToAction(nameof(Page), new { id = c.TodoItemId });
    }

    // ----- Mention bildirim üretici -----
    // TasksController içinde mevcut metodu bununla değiştir
    private async Task CreateMentionNotificationsAsync(TodoItem task, ApplicationUser author, string commentText)
    {
        // Sadece @admin anahtarını kontrol et (case-insensitive)
        var hasAdminKeyword = System.Text.RegularExpressions.Regex
            .IsMatch(commentText ?? string.Empty, @"@admin\b", RegexOptions.IgnoreCase);

        var notified = new HashSet<string>(); // userId set

        // 1) @admin ⇒ tüm adminlere bildir
        if (hasAdminKeyword)
        {
            var admins = await _users.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                if (admin.Id == author.Id) continue;   // kendine bildirim gönderme
                if (!notified.Add(admin.Id)) continue; // çokluluk/mükerrer kontrol

                var url = Url.Action("Page", "Tasks", new { id = task.Id });
                _db.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title = "Biri senden bahsetti.",
                    Message = $"{author.DisplayName ?? author.UserName} bir yorumda senden bahsetti: \"{Truncate(commentText, 180)}\"",
                    Url = url,
                    TodoItemId = task.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    IsArchived = false
                });
            }
        }

        // 2) Görev sahibine her durumda bilgi (yazarı kendisi değilse)
        //    → Özellikle admin yorum yazınca da tetiklenir.
        if (!string.IsNullOrEmpty(task.OwnerId)
            && task.OwnerId != author.Id
            && !notified.Contains(task.OwnerId!))
        {
            var url = Url.Action("Page", "Tasks", new { id = task.Id });
            _db.Notifications.Add(new Notification
            {
                UserId = task.OwnerId!,
                Title = "Görevine yeni yorum",
                Message = $"{author.DisplayName ?? author.UserName} görevine yorum yaptı: \"{Truncate(commentText, 180)}\"",
                Url = url,
                TodoItemId = task.Id,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                IsArchived = false
            });
        }

        await _db.SaveChangesAsync();
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string Format(TimeSpan ts)
    {
        var parts = new List<string>();
        if (ts.Days > 0) parts.Add($"{ts.Days}g");
        if (ts.Hours > 0) parts.Add($"{ts.Hours}s");
        if (ts.Minutes > 0) parts.Add($"{ts.Minutes}d");
        if (parts.Count == 0) parts.Add($"{Math.Max(0, ts.Seconds)}sn");
        return string.Join(" ", parts);
    }

    // ---------------- CREATE ----------------
    [HttpGet]
    public IActionResult Create() =>
        View(new TodoItem { Priority = 1, DueAt = DateTime.UtcNow.Date.AddDays(1) });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TodoItem m, IFormFile? attachment)
    {
        var user = await _users.GetUserAsync(User);

        if (string.IsNullOrWhiteSpace(m.Title))
            ModelState.AddModelError(nameof(TodoItem.Title), "Başlık zorunludur.");

        if (m.DueAt == default)
            ModelState.AddModelError(nameof(TodoItem.DueAt), "Son tarih zorunludur.");

        m.Priority = Math.Clamp(m.Priority, 0, 2);

        if (!ModelState.IsValid) return View(m);

        m.Title = m.Title.Trim();
        m.Description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim();
        m.OwnerId = user!.Id;
        m.CreatedAt = DateTime.UtcNow;

        if (attachment is { Length: > 0 })
        {
            var webPath = await SaveTaskAttachmentAsync(user.Id, attachment);
            if (webPath is null)
            {
                ModelState.AddModelError(string.Empty, "Dosya türü desteklenmiyor.");
                return View(m);
            }
            m.AttachmentPath = webPath;
        }

        _db.TodoItems.Add(m);
        await _db.SaveChangesAsync();

        TempData["ok"] = "Görev eklendi.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------- EDIT ----------------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _users.GetUserAsync(User);
        var m = await _db.TodoItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user!.Id);
        if (m is null) return NotFound();
        return View(m);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TodoItem m, IFormFile? attachment)
    {
        var user = await _users.GetUserAsync(User);
        var dbItem = await _db.TodoItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user!.Id);
        if (dbItem is null) return NotFound();

        if (string.IsNullOrWhiteSpace(m.Title))
            ModelState.AddModelError(nameof(TodoItem.Title), "Başlık zorunludur.");
        if (m.DueAt == default)
            ModelState.AddModelError(nameof(TodoItem.DueAt), "Son tarih zorunludur.");
        m.Priority = Math.Clamp(m.Priority, 0, 2);

        if (!ModelState.IsValid) return View(dbItem);

        dbItem.Title = m.Title.Trim();
        dbItem.Description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim();
        dbItem.DueAt = m.DueAt;
        dbItem.Priority = m.Priority;

        if (attachment is { Length: > 0 })
        {
            DeletePhysicalFileIfExists(dbItem.AttachmentPath);

            var webPath = await SaveTaskAttachmentAsync(user!.Id, attachment);
            if (webPath is null)
            {
                TempData["err"] = "Dosya türü desteklenmiyor.";
                return View(dbItem);
            }
            dbItem.AttachmentPath = webPath;
        }

        await _db.SaveChangesAsync();
        TempData["ok"] = "Görev güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------- TOGGLE DONE ----------------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await _users.GetUserAsync(User);
        var m = await _db.TodoItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user!.Id);
        if (m is null) return NotFound();

        m.IsDone = !m.IsDone;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ---------------- DELETE ----------------
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _users.GetUserAsync(User);
        var m = await _db.TodoItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user!.Id);
        if (m is null) return NotFound();
        return View(m);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _users.GetUserAsync(User);
        var m = await _db.TodoItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user!.Id);
        if (m is null) return NotFound();

        _db.TodoItems.Remove(m);
        await _db.SaveChangesAsync();

        DeletePhysicalFileIfExists(m.AttachmentPath);

        TempData["ok"] = "Görev silindi.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------- Helpers ----------------
    private async Task<string?> SaveTaskAttachmentAsync(string userId, IFormFile file)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",".doc",".docx",".xls",".xlsx",".txt",
            ".png",".jpg",".jpeg",".gif",".webp",
            ".zip",".rar",".7z"
        };

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            return null;

        var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var dir = Path.Combine(root, "uploads", "tasks", userId);
        Directory.CreateDirectory(dir);

        var name = $"att_{Guid.NewGuid():N}{ext}";
        var phys = Path.Combine(dir, name);

        try
        {
            using var fs = new FileStream(phys, FileMode.Create);
            await file.CopyToAsync(fs);
        }
        catch
        {
            return null;
        }

        return $"/uploads/tasks/{userId}/{name}";
    }

    // yorum eki kaydetme (tek dosya) — AddComment içinde döngü ile çağrılır
    private async Task<(string url, string contentType, long size)?> SaveCommentAttachmentAsync(string userId, IFormFile file)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",".jpg",".jpeg",".gif",".webp",
            ".pdf",".doc",".docx",".xls",".xlsx",".txt",
            ".zip",".rar",".7z"
        };

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            return null;

        var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var dir = Path.Combine(root, "uploads", "comments", userId);
        Directory.CreateDirectory(dir);

        var name = $"cmt_{Guid.NewGuid():N}{ext}";
        var phys = Path.Combine(dir, name);

        try
        {
            using var fs = new FileStream(phys, FileMode.Create);
            await file.CopyToAsync(fs);
        }
        catch { return null; }

        var url = $"/uploads/comments/{userId}/{name}";
        return (url, file.ContentType, file.Length);
    }

    private void DeletePhysicalFileIfExists(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath)) return;

        var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var rel = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var phys = Path.Combine(root, rel);

        try
        {
            if (System.IO.File.Exists(phys))
                System.IO.File.Delete(phys);
        }
        catch
        {
            // istersen logla
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToDo.Domain;
using ToDo.Infrastructure;
using ToDo.Web.Models; // <-- Page view modeli için

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
    public async Task<IActionResult> Index(string? q, string? filter)
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

        var items = await query
            .OrderBy(t => t.IsDone)
            .ThenBy(t => t.DueAt)
            .ThenBy(t => t.Id)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Q = q;
        ViewBag.Filter = filter;
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

        // sadece sahibi veya admin görebilsin
        if (t.OwnerId != uid && !User.IsInRole("Admin"))
            return Forbid();

        // Ekleri topla: önce çoklu TaskAttachments, yoksa tek dosya alanı
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
        catch
        {
            // TaskAttachment tablosu yoksa sessiz geç
        }

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
            RemainingText = ts.TotalSeconds <= 0 ? "Süre doldu" : Format(ts)
        };

        return View("Page", vm); // Views/Tasks/Page.cshtml
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

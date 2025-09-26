using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ToDo.Domain;
using ToDo.Infrastructure;
using ToDo.Web.Models;

namespace ToDo.Web.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _db;
        public NotificationsController(AppDbContext db) => _db = db;

        // /Notifications?filter=unread|archived|all  (default: all)
        [HttpGet]
        public async Task<IActionResult> Index(string? filter = "all")
        {
            var uid = CurrentUserId();
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            filter = (filter ?? "all").ToLowerInvariant();

            var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == uid);
            if (filter == "unread") q = q.Where(n => !n.IsRead && !n.IsArchived);
            else if (filter == "archived") q = q.Where(n => n.IsArchived);
            // "all" için filtre yok

            var list = await q
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationListItemVm
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Url = n.Url,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead

                })
                .ToListAsync();

            ViewBag.Filter = filter;
            return View(list);
        }

        // Detay: sahibi görüntüleyebilir; açılınca otomatik okundu yapılır
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var uid = CurrentUserId();
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
            if (n is null) return NotFound();

            if (!n.IsRead)
            {
                n.IsRead = true;
                await _db.SaveChangesAsync();
            }

            var vm = new NotificationDetailsVm
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                Url = n.Url,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id, string? filter = "all", string? returnTo = null)
        {
            var n = await FindOwnedAsync(id);
            if (n is null) return NotFound();

            if (!n.IsRead)
            {
                n.IsRead = true;
                await _db.SaveChangesAsync();
            }

            if (string.Equals(returnTo, "details", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(Details), new { id });

            return RedirectToAction(nameof(Index), new { filter });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnread(int id, string? filter = "all")
        {
            var n = await FindOwnedAsync(id);
            if (n is null) return NotFound();

            n.IsRead = false;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { filter = filter ?? "all" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id, string? filter = "all")
        {
            var n = await FindOwnedAsync(id);
            if (n is null) return NotFound();

            n.IsArchived = true;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { filter = filter ?? "all" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Unarchive(int id, string? filter = "archived")
        {
            var n = await FindOwnedAsync(id);
            if (n is null) return NotFound();

            n.IsArchived = false;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { filter = filter ?? "archived" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? filter = "all")
        {
            var n = await FindOwnedAsync(id);
            if (n is null) return NotFound();

            _db.Notifications.Remove(n);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { filter = filter ?? "all" });
        }

        // Tüm okunmamışları okundu yap (arşivde olmayan)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead(string? filter = "all")
        {
            var uid = CurrentUserId();
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            var unread = await _db.Notifications
                .Where(x => x.UserId == uid && !x.IsRead && !x.IsArchived)
                .ToListAsync();

            if (unread.Count > 0)
            {
                foreach (var x in unread) x.IsRead = true;
                await _db.SaveChangesAsync();
            }

            TempData["ok"] = "Tüm bildirimler okundu olarak işaretlendi.";
            return RedirectToAction(nameof(Index), new { filter = filter ?? "all" });
        }

        // ---- Helpers ----
        private string? CurrentUserId()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User?.Claims?.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier"))?.Value
               ?? User?.Identity?.Name;

        private async Task<Notification?> FindOwnedAsync(int id)
        {
            var uid = CurrentUserId();
            if (string.IsNullOrEmpty(uid)) return null;

            return await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == uid);
        }
    }
}

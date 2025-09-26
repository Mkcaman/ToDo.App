using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using ToDo.Domain;
using ToDo.Infrastructure;
using ToDo.Web.Models;

namespace ToDo.Web.Controllers
{
    [Authorize] // varsayılan: korumalı
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public HomeController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            // View için her durumda bir model dönelim
            var vm = new HomeIndexVm();

            if (User.Identity?.IsAuthenticated == true)
            {
                var u = await _users.GetUserAsync(User);
                var userId = u!.Id;

                // Yerel gün başlangıç/bitişi → UTC
                var localToday = DateTime.Now.Date;
                var localTomorrow = localToday.AddDays(1);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localToday);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(localTomorrow);

                var nowUtc = DateTime.UtcNow;

                // DbContext tek instance → sorguları sırayla çalıştır
                ViewBag.MyActive = await _db.TodoItems.CountAsync(x => x.OwnerId == userId && !x.IsDone);
                ViewBag.Overdue = await _db.TodoItems.CountAsync(x => x.OwnerId == userId && !x.IsDone && x.DueAt < nowUtc);
                ViewBag.Today = await _db.TodoItems.CountAsync(x => x.OwnerId == userId && x.DueAt >= startUtc && x.DueAt < endUtc);
                ViewBag.Done = await _db.TodoItems.CountAsync(x => x.OwnerId == userId && x.IsDone);

                // Son 5 bildirim (CreatedAt alanına göre, en yeni 5)
                vm.Last5Notifications = await _db.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)        // tabloda alan adı "CreatedAt"
                    .Take(5)
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
            }
            else
            {
                ViewBag.MyActive = ViewBag.Overdue = ViewBag.Today = ViewBag.Done = 0;
            }

            return View(vm);
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden; // 403
            TempData["err"] = "Bu sayfaya erişim yetkin yok.";
            return View();
        }

        // UseExceptionHandler("/Home/Error") için
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            ViewBag.ErrorPath = feature?.Path;

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        // Basit sağlık kontrolü
        [HttpGet, AllowAnonymous, Route("healthz")]
        public IActionResult Health() => Ok("ok");
    }
}

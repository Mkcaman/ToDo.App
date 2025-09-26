using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToDo.Domain;
using ToDo.Web.Models;

namespace ToDo.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;

        public AdminUsersController(UserManager<ApplicationUser> users) => _users = users;

        // --- Helpers ---
        private async Task<bool> IsLastAdminAsync(ApplicationUser user)
        {
            var admins = await _users.GetUsersInRoleAsync("Admin");
            return admins.Count == 1 && admins.Any(a => a.Id == user.Id);
        }

        private string? CurrentUserId => _users.GetUserId(User);

        private static bool IsAllowedRole(string role)
            => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);

        // --- Actions ---

        public async Task<IActionResult> Index()
        {
            var all = await _users.Users.ToListAsync();

            var list = new List<UserWithRolesVm>(all.Count);
            foreach (var u in all)
            {
                list.Add(new UserWithRolesVm
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Roles = await _users.GetRolesAsync(u)
                });
            }

            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeAdmin(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["err"] = "Geçersiz kullanıcı.";
                return RedirectToAction(nameof(Index));
            }

            var u = await _users.FindByIdAsync(id);
            if (u is null)
            {
                TempData["err"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (await _users.IsInRoleAsync(u, "Admin"))
            {
                TempData["ok"] = $"{u.Email} zaten Admin.";
                return RedirectToAction(nameof(Index));
            }

            var res = await _users.AddToRoleAsync(u, "Admin");
            TempData[res.Succeeded ? "ok" : "err"] =
                res.Succeeded ? $"{u.Email} Admin yapıldı." :
                string.Join("; ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeEmployee(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["err"] = "Geçersiz kullanıcı.";
                return RedirectToAction(nameof(Index));
            }

            var u = await _users.FindByIdAsync(id);
            if (u is null)
            {
                TempData["err"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (await _users.IsInRoleAsync(u, "Employee"))
            {
                TempData["ok"] = $"{u.Email} zaten Employee.";
                return RedirectToAction(nameof(Index));
            }

            var res = await _users.AddToRoleAsync(u, "Employee");
            TempData[res.Succeeded ? "ok" : "err"] =
                res.Succeeded ? $"{u.Email} Employee yapıldı." :
                string.Join("; ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromRole(string id, string role)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(role))
            {
                TempData["err"] = "Geçersiz istek.";
                return RedirectToAction(nameof(Index));
            }

            if (!IsAllowedRole(role))
            {
                TempData["err"] = "İzin verilmeyen rol.";
                return RedirectToAction(nameof(Index));
            }

            var u = await _users.FindByIdAsync(id);
            if (u is null)
            {
                TempData["err"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Güvenlik: Kendi adminliğini kaldıramazsın.
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && u.Id == CurrentUserId)
            {
                TempData["err"] = "Kendi Admin rolünü kaldıramazsın.";
                return RedirectToAction(nameof(Index));
            }

            // Güvenlik: Sistemde en az bir admin kalmalı.
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                && await _users.IsInRoleAsync(u, "Admin")
                && await IsLastAdminAsync(u))
            {
                TempData["err"] = "Son Admin kaldırılamaz. Sistemde en az bir Admin kalmalı.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _users.IsInRoleAsync(u, role))
            {
                TempData["ok"] = $"{u.Email} zaten {role} rolünde değil.";
                return RedirectToAction(nameof(Index));
            }

            var res = await _users.RemoveFromRoleAsync(u, role);
            TempData[res.Succeeded ? "ok" : "err"] =
                res.Succeeded ? $"{u.Email} '{role}' rolünden çıkarıldı." :
                string.Join("; ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }
    }
}



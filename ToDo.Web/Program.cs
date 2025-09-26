using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using ToDo.Domain;
using ToDo.Infrastructure;
using ToDo.Web.Services;
// using ToDo.Web.Services; // SmtpEmailSender, EmailOptions (SMTP kullanacaksan aç)

var builder = WebApplication.CreateBuilder(args);

// ---------------- DB (PostgreSQL) ----------------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ---------------- Identity ----------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.SignIn.RequireConfirmedAccount = false;
        opt.User.RequireUniqueEmail = true;

        opt.Password.RequireDigit = false;
        opt.Password.RequireLowercase = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// Cookie yollarý
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Identity/Account/Login";
    opt.AccessDeniedPath = "/Home/AccessDenied";
    opt.LogoutPath = "/Identity/Account/Logout";
});

// ---------------- Authorization Politikalarý ----------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("Staff", p => p.RequireRole("Admin", "Employee"));
});

// ---------------- MVC + Razor Pages ----------------
// Global: tüm Controller action'larý login gerektirsin (Razor Pages etkilenmez)
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddRazorPages();

// ---------------- E-posta (þimdilik pas) ----------------
builder.Services.AddTransient<IEmailSender, NoopEmailSender>();


// SMTP kullanacaksan bunlarý aç:
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();


// ---------------- Dosya Yükleme Limitleri ----------------
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

var app = builder.Build();

// ---------------- Hata Sayfalarý ----------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Upload klasörleri garanti oluþturulsun
{
    var root = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    Directory.CreateDirectory(Path.Combine(root, "uploads", "tasks"));
    Directory.CreateDirectory(Path.Combine(root, "uploads", "profile"));
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Varsayýlan MVC rotasý
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------------- Seed: Roller + Ýlk Admin + (opsiyonel) Ýlk Employee ----------------
using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    string[] roles = { "Admin", "Employee" };
    foreach (var r in roles)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    // --- Admin seed ---
    var email = cfg["AdminUser:Email"];
    var first = cfg["AdminUser:FirstName"] ?? "Admin";
    var last = cfg["AdminUser:LastName"] ?? "User";
    var pass = cfg["AdminUser:Password"];

    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pass))
    {
        var admin = await userMgr.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = first,
                LastName = last,
                EmailConfirmed = true
            };
            var created = await userMgr.CreateAsync(admin, pass);
            if (created.Succeeded)
                await userMgr.AddToRoleAsync(admin, "Admin");
        }
        else if (!await userMgr.IsInRoleAsync(admin, "Admin"))
        {
            await userMgr.AddToRoleAsync(admin, "Admin");
        }
    }

    // --- Employee seed (dropdown boþ kalmasýn) ---
    var seedEmpEmail = cfg["SeedEmployee:Email"];
    var seedEmpPass = cfg["SeedEmployee:Password"];
    var seedEmpFirst = cfg["SeedEmployee:FirstName"] ?? "Demo";
    var seedEmpLast = cfg["SeedEmployee:LastName"] ?? "Employee";

    if (!string.IsNullOrWhiteSpace(seedEmpEmail) && !string.IsNullOrWhiteSpace(seedEmpPass))
    {
        var emp = await userMgr.FindByEmailAsync(seedEmpEmail);
        if (emp is null)
        {
            emp = new ApplicationUser
            {
                UserName = seedEmpEmail,
                Email = seedEmpEmail,
                FirstName = seedEmpFirst,
                LastName = seedEmpLast,
                EmailConfirmed = true
            };
            var createdEmp = await userMgr.CreateAsync(emp, seedEmpPass);
            if (createdEmp.Succeeded)
                await userMgr.AddToRoleAsync(emp, "Employee");
        }
        else if (!await userMgr.IsInRoleAsync(emp, "Employee"))
        {
            await userMgr.AddToRoleAsync(emp, "Employee");
        }
    }
    else
    {
        // Config verilmemiþse ve hiç Employee yoksa varsayýlan bir demo kullanýcý oluþtur
        var anyEmployee = (await userMgr.GetUsersInRoleAsync("Employee")).Any();
        if (!anyEmployee)
        {
            var demoEmail = "employee@todo.local";
            var demoPass = "P@ssw0rd!";

            if (await userMgr.FindByEmailAsync(demoEmail) is null)
            {
                var emp = new ApplicationUser
                {
                    UserName = demoEmail,
                    Email = demoEmail,
                    FirstName = "Demo",
                    LastName = "Employee",
                    EmailConfirmed = true
                };
                var createdEmp = await userMgr.CreateAsync(emp, demoPass);
                if (createdEmp.Succeeded)
                    await userMgr.AddToRoleAsync(emp, "Employee");
            }
        }
    }
}

app.Run();

// ---------------- Noop Email Sender ----------------
public sealed class NoopEmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => Task.CompletedTask; // hiçbir þey yapmaz
}

/*
// SMTP kullanacaksan gereken POCO (EmailOptions) örneði:
public sealed class EmailOptions
{
    public string SmtpHost { get; set; } = "";
    public int    SmtpPort { get; set; } = 587;
    public string User     { get; set; } = "";
    public string Password { get; set; } = "";
    public string From     { get; set; } = "";
    public bool   EnableSsl{ get; set; } = true;
} */


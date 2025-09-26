using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Admin")]
public class ToolsController : Controller
{
    private readonly IEmailSender _email;
    public ToolsController(IEmailSender email) => _email = email;

    // /tools/test-mail?to=hedef@ornek.com
    [HttpGet("/tools/test-mail")]
    public async Task<IActionResult> TestMail(string to = "hedef@ornek.com")
    {
        try
        {
            await _email.SendEmailAsync(
                to,
                "SMTP Test",
                "<b>Merhaba!</b> Bu bir test e-postasıdır."
            );
            return Content($"OK: {to} adresine gönderildi.");
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            return Content("HATA: " + ex.Message);
        }
    }
}

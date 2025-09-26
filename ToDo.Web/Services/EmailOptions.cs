using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace ToDo.Web.Services;


public class EmailOptions
{
    public string Host { get; set; } = "";          // appsettings: Email:Host
    public int Port { get; set; } = 587;            // Email:Port
    public bool EnableSsl { get; set; } = true;     // Email:EnableSsl

    public string UserName { get; set; } = "";      // Email:UserName (Gmail adresi)
    public string Password { get; set; } = "";      // secrets: Email:Password

    public string FromAddress { get; set; } = "";   // Email:FromAddress
    public string? FromName { get; set; }           // Email:FromName (isteğe bağlı)
}

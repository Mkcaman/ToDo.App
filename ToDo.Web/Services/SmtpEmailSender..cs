using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using ToDo.Web.Services;

namespace ToDo.Web.Services;

    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;
        public SmtpEmailSender(IOptions<EmailOptions> opt) => _opt = opt.Value;

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using var client = new SmtpClient(_opt.Host, _opt.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                EnableSsl = _opt.EnableSsl,
                Credentials = new NetworkCredential(_opt.UserName, _opt.Password)
            };

            var from = new MailAddress(_opt.FromAddress, _opt.FromName ?? _opt.FromAddress);
            var to = new MailAddress(email);

            using var msg = new MailMessage(from, to)
            {
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            await client.SendMailAsync(msg);
        }

}

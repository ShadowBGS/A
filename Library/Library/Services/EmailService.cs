using System.Net.Mail;
using System.Net;

namespace Library.Services
{
    public class EmailService
    {
        private readonly string _fromEmail = "demiladefajolu@gmail.com";
        private readonly string _appPassword = "zpii drki casy dvgl";

        public void SendVerificationEmail(string toEmail, string subject, string body)
        {
            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_fromEmail, _appPassword),
                EnableSsl = true
            };

            var message = new MailMessage(_fromEmail, toEmail, subject, body)
            {
                IsBodyHtml = true
            };

            smtp.Send(message);
        }
    }

}

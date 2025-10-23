// src/ZooSanMarino.Application/Interfaces/IEmailService.cs
using System.Threading.Tasks;

namespace ZooSanMarino.Application.Interfaces;

public interface IEmailService
{
    Task SendPasswordRecoveryEmailAsync(string email, string newPassword);
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
}




// src/ZooSanMarino.Application/DTOs/AuthDtos.cs
namespace ZooSanMarino.Application.DTOs;


public sealed class ChangeEmailDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewEmail        { get; set; } = string.Empty;
}

namespace ZooSanMarino.Application.DTOs;

public sealed class AdminResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AdminResetPasswordResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? EmailQueueId { get; set; }
    public bool EmailQueued { get; set; }
}

namespace ZooSanMarino.Domain.Entities
{
    /// <summary>
    /// Single-use password reset token. Token is valid for 15 minutes and consumed on first use.
    /// </summary>
    public class PasswordResetToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; } = null!;  // CSPRNG-generated, random + hashed before storage
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime? UsedAt { get; set; }

        public User User { get; set; } = null!;
    }
}

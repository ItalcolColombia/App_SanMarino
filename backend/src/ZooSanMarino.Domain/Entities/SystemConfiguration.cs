using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Domain.Entities
{
    /// <summary>
    /// Entidad para almacenar configuraciones del sistema de forma segura
    /// </summary>
    public class SystemConfiguration
    {
        [Key]
        public string Key { get; set; } = null!;
        
        public string Value { get; set; } = null!;
        
        public string? Description { get; set; }
        
        public bool IsEncrypted { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
    }
}

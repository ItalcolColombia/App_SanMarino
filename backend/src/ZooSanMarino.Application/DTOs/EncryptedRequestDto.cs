// ZooSanMarino.Application/DTOs/EncryptedRequestDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para recibir datos encriptados del frontend
/// </summary>
public class EncryptedRequestDto
{
    /// <summary>Datos encriptados en base64</summary>
    public string EncryptedData { get; set; } = null!;
}






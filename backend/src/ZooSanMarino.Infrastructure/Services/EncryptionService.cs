// ZooSanMarino.Infrastructure/Services/EncryptionService.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio de encriptación para comunicación segura con el frontend
/// Usa AES-256-CBC para encriptar/desencriptar datos
/// </summary>
public class EncryptionService
{
    private readonly string _frontendDecryptionKey; // Llave para desencriptar datos del frontend
    private readonly string _backendEncryptionKey;  // Llave para encriptar datos al frontend

    public EncryptionService(IConfiguration configuration)
    {
        // Obtener llaves del appsettings.json
        _frontendDecryptionKey = configuration["Encryption:RemitenteFrontend"] 
            ?? throw new InvalidOperationException("Encryption:RemitenteFrontend no configurada");
        
        _backendEncryptionKey = configuration["Encryption:RemitenteBackend"] 
            ?? throw new InvalidOperationException("Encryption:RemitenteBackend no configurada");
    }

    /// <summary>
    /// Desencripta datos recibidos del frontend
    /// </summary>
    public T DecryptFromFrontend<T>(string encryptedData)
    {
        try
        {
            var decrypted = Decrypt(encryptedData, _frontendDecryptionKey);
            
            // Configurar opciones JSON para manejar camelCase desde el frontend
            // El frontend envía JSON con camelCase (email, password), pero LoginDto usa PascalCase (Email, Password)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Esto permite que "email" mapee a "Email"
                PropertyNamingPolicy = null // No aplicar ninguna política de nombres, usar los nombres exactos de las propiedades
            };
            
            var result = JsonSerializer.Deserialize<T>(decrypted, jsonOptions);
            
            if (result == null)
                throw new InvalidOperationException("Error al deserializar datos desencriptados: resultado nulo");
                
            return result;
        }
        catch (JsonException ex)
        {
            // Intentar obtener el JSON desencriptado para debugging
            string? decryptedJson = null;
            try
            {
                decryptedJson = Decrypt(encryptedData, _frontendDecryptionKey);
            }
            catch { }
            
            var jsonPreview = decryptedJson != null && decryptedJson.Length > 200 
                ? decryptedJson.Substring(0, 200) + "..." 
                : decryptedJson ?? "No se pudo obtener";
                
            throw new InvalidOperationException($"Error al deserializar JSON desencriptado: {ex.Message}. JSON recibido (primeros chars): {jsonPreview}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al desencriptar datos del frontend: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Encripta datos antes de enviarlos al frontend
    /// </summary>
    public string EncryptForFrontend<T>(T data)
    {
        try
        {
            // Configurar opciones JSON para usar camelCase (como espera el frontend)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Convertir a camelCase
                PropertyNameCaseInsensitive = true
            };
            
            var jsonString = JsonSerializer.Serialize(data, jsonOptions);
            return Encrypt(jsonString, _backendEncryptionKey);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al encriptar datos para el frontend: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Encripta un string usando AES-256-CBC
    /// </summary>
    private string Encrypt(string plaintext, string keyString)
    {
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Derivar llave de 256 bits desde el string usando PBKDF2
            var key = DeriveKey(keyString, 32); // 32 bytes = 256 bits
            aes.Key = key;

            // Generar IV aleatorio
            aes.GenerateIV();
            var iv = aes.IV;

            // Encriptar
            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                ms.Write(iv, 0, iv.Length); // Escribir IV al inicio
                
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plaintext);
                }

                var encrypted = ms.ToArray();
                return Convert.ToBase64String(encrypted);
            }
        }
    }

    /// <summary>
    /// Desencripta un string usando AES-256-CBC con una llave específica
    /// Útil para desencriptar SECRET_UP u otros valores encriptados
    /// </summary>
    public string Decrypt(string ciphertext, string keyString)
    {
        try
        {
            var encryptedBytes = Convert.FromBase64String(ciphertext);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Derivar llave de 256 bits
                var key = DeriveKey(keyString, 32);
                aes.Key = key;

                // Leer IV desde el inicio de los datos encriptados
                var iv = new byte[16];
                Array.Copy(encryptedBytes, 0, iv, 0, 16);
                aes.IV = iv;

                // Desencriptar
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedBytes, 16, encryptedBytes.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al desencriptar: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deriva una llave de bytes desde un string usando PBKDF2
    /// </summary>
    private byte[] DeriveKey(string keyString, int keyLength)
    {
        var salt = Encoding.UTF8.GetBytes("sanmarino-salt"); // Salt fijo para consistencia
        using (var pbkdf2 = new Rfc2898DeriveBytes(keyString, salt, 10000, HashAlgorithmName.SHA256))
        {
            return pbkdf2.GetBytes(keyLength);
        }
    }
}


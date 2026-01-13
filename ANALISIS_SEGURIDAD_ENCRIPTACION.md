# 🔐 ANÁLISIS DE SEGURIDAD Y ENCRIPTACIÓN

## 📋 RESUMEN EJECUTIVO

La aplicación implementa un sistema de seguridad multicapa que incluye:

1. **Encriptación AES-256-CBC** para datos sensibles en tránsito
2. **Hashing de contraseñas** con ASP.NET Core Identity PasswordHasher
3. **JWT (JSON Web Tokens)** para autenticación
4. **SECRET_UP encriptado** para validación de peticiones
5. **HTTPS** recomendado para producción

---

## 🔒 SISTEMA DE ENCRIPTACIÓN

### 1. Encriptación de Datos en Tránsito

#### Algoritmo: **AES-256-CBC**
- **Tamaño de llave**: 256 bits
- **Modo**: CBC (Cipher Block Chaining)
- **Padding**: PKCS7
- **Derivación de llave**: PBKDF2 con 10,000 iteraciones y SHA-256

#### Implementación:

**Frontend** (`encryption.service.ts`):
- Usa **Web Crypto API** cuando está disponible (HTTPS/localhost)
- Fallback a **crypto-js** para compatibilidad HTTP
- Métodos principales:
  - `encryptForBackend<T>()` - Encripta datos antes de enviar al backend
  - `decryptFromBackend<T>()` - Desencripta respuestas del backend
  - `encryptSecretUp()` - Encripta el SECRET_UP

**Backend** (`EncryptionService.cs`):
- Usa `System.Security.Cryptography` de .NET
- Métodos principales:
  - `DecryptFromFrontend<T>()` - Desencripta datos del frontend
  - `EncryptForFrontend<T>()` - Encripta respuestas para el frontend
  - `Decrypt()` - Desencripta valores genéricos (ej: SECRET_UP)

#### Llaves de Encriptación:

| Dirección | Llave | Ubicación Frontend | Ubicación Backend |
|-----------|-------|-------------------|-------------------|
| Frontend → Backend | `RemitenteFrontend` | `environment.encryptionKeys.remitenteFrontend` | `appsettings.json:Encryption:RemitenteFrontend` |
| Backend → Frontend | `RemitenteBackend` | `environment.encryptionKeys.remitenteBackend` | `appsettings.json:Encryption:RemitenteBackend` |

**⚠️ IMPORTANTE**: Las llaves deben ser diferentes en desarrollo y producción, y nunca deben commitearse al repositorio.

---

### 2. Hashing de Contraseñas

#### Algoritmo: **ASP.NET Core Identity PasswordHasher**

**Implementación**:
- Usa `IPasswordHasher<Login>` de Microsoft.AspNetCore.Identity
- Algoritmo: PBKDF2 con SHA-256
- **Iteraciones**: Configurado automáticamente por ASP.NET Core Identity (típicamente 10,000+)
- **Salt**: Generado automáticamente y único por contraseña

**Ubicación**:
- `backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs`
- `backend/src/ZooSanMarino.Infrastructure/Services/UserService.cs`

**Uso**:
```csharp
// Hash de contraseña al crear usuario
PasswordHash = _hasher.HashPassword(null!, dto.Password);

// Verificación de contraseña al login
var result = _hasher.VerifyHashedPassword(login, login.PasswordHash, dto.Password);
if (result == PasswordVerificationResult.Failed)
    throw new UnauthorizedAccessException("Credenciales inválidas");
```

**✅ Seguridad**: 
- Las contraseñas **NUNCA** se almacenan en texto plano
- Cada contraseña tiene su propio salt único
- Resistente a ataques de fuerza bruta y rainbow tables

---

### 3. Autenticación JWT (JSON Web Tokens)

#### Configuración:

**Algoritmo de firma**: HMAC-SHA256
**Duración**: Configurable (por defecto 120 minutos)
**Claims incluidos**:
- `NameIdentifier` - ID del usuario (Guid)
- `Sub` - Subject (ID del usuario)
- `Email` - Email del usuario
- `Role` - Roles del usuario (múltiples)
- `company_id` - IDs de empresas asociadas
- `permission` - Permisos específicos del usuario

**Ubicación**:
- `backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs`
- `backend/src/ZooSanMarino.API/Program.cs`

**Configuración en `appsettings.json`**:
```json
{
  "JwtSettings": {
    "Key": "[SECRET_KEY_MINIMO_32_CARACTERES]",
    "Issuer": "ZooSanMarino",
    "Audience": "ZooSanMarinoApp",
    "DurationInMinutes": 120
  }
}
```

**✅ Seguridad**:
- Token firmado con HMAC-SHA256
- Validación de issuer y audience
- Expiración automática
- Tokens almacenados en `sessionStorage` (se eliminan al cerrar sesión)

---

### 4. Sistema SECRET_UP

#### Descripción:
Todas las peticiones HTTP deben incluir un SECRET_UP encriptado que identifica la plataforma autorizada.

#### Implementación:

**Frontend** (`auth.interceptor.ts`):
- Intercepta todas las peticiones HTTP
- Agrega header `X-Secret-Up` con SECRET_UP encriptado
- También agrega `Authorization: Bearer [token]` si está autenticado

**Backend** (`PlatformSecretMiddleware.cs`):
- Valida el SECRET_UP antes de procesar cualquier petición
- Se ejecuta después de CORS pero antes de Authentication/Authorization
- Rechaza peticiones sin SECRET_UP válido con `401 Unauthorized`

#### Configuración:

| Componente | Ubicación Frontend | Ubicación Backend |
|------------|-------------------|-------------------|
| SECRET_UP Frontend | `environment.platformSecret.secretUpFrontend` | `appsettings.json:PlatformSecret:SecretUpFrontend` |
| Llave de Encriptación | `environment.platformSecret.encryptionKey` | `appsettings.json:PlatformSecret:EncryptionKey` |

**✅ Seguridad**:
- Previene acceso no autorizado desde otras aplicaciones
- El SECRET_UP se encripta antes de enviarse
- Validación en middleware antes de llegar a los controladores

---

## 🛡️ CAPAS DE SEGURIDAD

### Arquitectura de Seguridad:

```
┌─────────────────────────────────────────────────────┐
│                 FRONTEND                            │
│                                                     │
│  1. EncryptionService                              │
│     - Encripta datos sensibles (login)             │
│     - Encripta SECRET_UP                            │
│                                                     │
│  2. AuthInterceptor                                │
│     - Agrega headers de seguridad                  │
│     - X-Secret-Up: [encriptado]                    │
│     - Authorization: Bearer [JWT]                   │
│                                                     │
└─────────────────────────────────────────────────────┘
                      │
                      │ HTTPS (Recomendado)
                      │
┌─────────────────────────────────────────────────────┐
│                 BACKEND                             │
│                                                     │
│  1. PlatformSecretMiddleware                       │
│     - Valida SECRET_UP encriptado                  │
│     - Rechaza peticiones no autorizadas            │
│                                                     │
│  2. Authentication/Authorization                   │
│     - Valida JWT token                             │
│     - Verifica permisos y roles                    │
│                                                     │
│  3. EncryptionService                              │
│     - Desencripta datos del frontend              │
│     - Encripta respuestas al frontend              │
│                                                     │
│  4. PasswordHasher                                 │
│     - Hash de contraseñas                          │
│     - Verificación segura                          │
│                                                     │
└─────────────────────────────────────────────────────┘
```

---

## ✅ EVALUACIÓN DE SEGURIDAD

### Fortalezas:

1. **✅ Encriptación AES-256-CBC**
   - Algoritmo robusto y ampliamente usado
   - Llave de 256 bits (máxima seguridad)
   - PBKDF2 con 10,000 iteraciones (resistente a fuerza bruta)

2. **✅ Hashing de Contraseñas**
   - ASP.NET Core Identity PasswordHasher (estándar de la industria)
   - Salt único por contraseña
   - Resistente a rainbow tables

3. **✅ JWT con HMAC-SHA256**
   - Tokens firmados criptográficamente
   - Expiración automática
   - Claims para autorización granular

4. **✅ SECRET_UP**
   - Previene acceso no autorizado
   - Encriptado antes de enviarse
   - Validación en middleware

5. **✅ Separación de responsabilidades**
   - Encriptación separada para frontend→backend y backend→frontend
   - Llaves diferentes para diferentes propósitos

### Áreas de Mejora:

1. **⚠️ HTTPS en Producción**
   - **CRÍTICO**: La aplicación debe usar HTTPS en producción
   - Sin HTTPS, los datos encriptados pueden ser interceptados
   - **Recomendación**: Configurar certificado SSL/TLS en el servidor

2. **⚠️ Rotación de Llaves**
   - Implementar rotación periódica de llaves (cada 3-6 meses)
   - Usar Azure Key Vault o similar en producción
   - Documentar proceso de rotación

3. **⚠️ Gestión de Secretos**
   - Las llaves actuales están en `appsettings.json` (desarrollo)
   - En producción, usar variables de entorno o Azure Key Vault
   - **NUNCA** commitear llaves al repositorio

4. **⚠️ Rate Limiting**
   - Implementar rate limiting en endpoints de login
   - Prevenir ataques de fuerza bruta
   - Bloquear IPs después de múltiples intentos fallidos

5. **⚠️ Logging de Seguridad**
   - Registrar intentos de login fallidos
   - Registrar peticiones con SECRET_UP inválido
   - Alertas para patrones sospechosos

6. **⚠️ Validación de Entrada**
   - Sanitizar todas las entradas del usuario
   - Validar formato de email
   - Prevenir SQL injection (usar parámetros)

---

## 🔧 CONFIGURACIÓN RECOMENDADA PARA PRODUCCIÓN

### 1. Variables de Entorno

```bash
# Backend (.env o Azure App Settings)
ENCRYPTION__REMITENTEFRONTEND=<llave-segura-256-bits>
ENCRYPTION__REMITENTEBACKEND=<llave-segura-256-bits>
PLATFORMSECRET__SECRETUPFRONTEND=<secret-up-unico>
PLATFORMSECRET__ENCRYPTIONKEY=<llave-encriptacion-secret>
JWTSETTINGS__KEY=<jwt-secret-minimo-32-caracteres>
```

### 2. HTTPS

```csharp
// Program.cs
app.UseHttpsRedirection(); // Redirigir HTTP a HTTPS
app.UseHsts(); // HTTP Strict Transport Security
```

### 3. Headers de Seguridad

```csharp
// Program.cs
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000");
    await next();
});
```

### 4. Rate Limiting

```csharp
// Instalar: Microsoft.AspNetCore.RateLimiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5; // 5 intentos por minuto
    });
});

// Aplicar en endpoint de login
[EnableRateLimiting("login")]
[HttpPost("login")]
public async Task<IActionResult> Login(...)
```

---

## 📊 MATRIZ DE SEGURIDAD

| Componente | Algoritmo | Fortaleza | Estado |
|------------|-----------|-----------|--------|
| Encriptación Login | AES-256-CBC | ⭐⭐⭐⭐⭐ | ✅ Implementado |
| Hashing Contraseñas | PBKDF2-SHA256 | ⭐⭐⭐⭐⭐ | ✅ Implementado |
| JWT | HMAC-SHA256 | ⭐⭐⭐⭐ | ✅ Implementado |
| SECRET_UP | AES-256-CBC | ⭐⭐⭐⭐⭐ | ✅ Implementado |
| HTTPS | TLS 1.2+ | ⭐⭐⭐⭐⭐ | ⚠️ Requerido en producción |
| Rate Limiting | - | ⭐⭐⭐ | ❌ No implementado |
| Logging Seguridad | - | ⭐⭐⭐ | ⚠️ Parcial |

---

## 🎯 CONCLUSIÓN

### Seguridad General: **BUENA** ⭐⭐⭐⭐

La aplicación implementa un sistema de seguridad robusto con:
- ✅ Encriptación AES-256-CBC para datos sensibles
- ✅ Hashing seguro de contraseñas
- ✅ JWT para autenticación
- ✅ SECRET_UP para validación de peticiones

### Recomendaciones Críticas:

1. **🔴 ALTA PRIORIDAD**: Configurar HTTPS en producción
2. **🟡 MEDIA PRIORIDAD**: Implementar rate limiting en login
3. **🟡 MEDIA PRIORIDAD**: Mover llaves a Azure Key Vault o variables de entorno
4. **🟢 BAJA PRIORIDAD**: Mejorar logging de seguridad
5. **🟢 BAJA PRIORIDAD**: Implementar rotación de llaves

### Conclusión Final:

El sistema de encriptación y seguridad es **sólido y bien implementado**. Con las mejoras recomendadas (especialmente HTTPS en producción), el sistema alcanzaría un nivel de seguridad **excelente** ⭐⭐⭐⭐⭐.

---

## 📚 REFERENCIAS

- [AES-256-CBC](https://en.wikipedia.org/wiki/Advanced_Encryption_Standard)
- [PBKDF2](https://tools.ietf.org/html/rfc2898)
- [JWT](https://jwt.io/)
- [ASP.NET Core Identity PasswordHasher](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/)
- [Web Crypto API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Crypto_API)

---

**Última actualización**: 2024
**Versión**: 1.0


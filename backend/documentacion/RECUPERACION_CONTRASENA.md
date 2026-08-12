# Recuperación de contraseña

> Actualizado el 12-ago-2026. La versión anterior de este documento describía un flujo que ya no
> existe (una «contraseña temporal» enviada por correo y una sección de configuración `EmailSettings`
> que el código no lee). Hoy el flujo es por **enlace de un solo uso**.

## Cómo funciona

```
Usuario                        Frontend                    Backend                     Correo
  │                               │                           │                          │
  ├─ "¿Olvidaste tu contraseña?" ─▶ /password-recovery        │                          │
  │                               ├─ POST /api/Auth/recover-password ─▶                  │
  │                               │                           ├─ genera token (64 car.,  │
  │                               │                           │   CSPRNG, 15 min, 1 uso) │
  │                               │                           ├─ invalida tokens previos │
  │                               │                           ├─ encola el correo ───────▶ enlace
  │                               ◀─ respuesta NEUTRA ────────┤                          │
  │                                                                                      │
  ├─ abre el enlace del correo ──▶ /reset-password?token=…                                │
  │                               ├─ POST /api/Auth/reset-password ──▶                    │
  │                               │                           ├─ valida y CONSUME token  │
  │                               │                           ├─ re-hashea la contraseña │
  │                               ◀─ éxito ───────────────────┤                          │
  ├─ entra con la contraseña nueva                                                        │
```

### Reglas

| Regla | Valor | Dónde vive |
|---|---|---|
| Longitud del token | 64 caracteres, CSPRNG | `AuthService.GeneratePasswordResetToken` |
| Vigencia | **15 minutos** | `AuthService.RecoverPasswordAsync` · `CorreosCuenta.MinutosVigencia` |
| Usos | **Uno solo** (se marca `is_used` al canjearlo) | `AuthService.ValidateAndUsePasswordResetTokenAsync` |
| Tokens previos | Se invalidan al pedir uno nuevo | `AuthService.RecoverPasswordAsync` |
| Contraseña nueva | Mínimo 8, máximo 100, al menos una letra y un número | `ValidatePasswordResetTokenDto` + validadores del componente |

### La respuesta es neutra a propósito

`POST /api/Auth/recover-password` devuelve **siempre** lo mismo, exista o no el correo:

```json
{ "success": true, "message": "Si el correo está registrado, recibirás un mensaje con instrucciones…",
  "userFound": false, "emailSent": false }
```

Es anti-enumeración: si la respuesta distinguiera, cualquiera podría averiguar qué correos tienen
cuenta. **Consecuencia para depurar: el resultado real NO se lee en la respuesta HTTP**, se lee en la
tabla `email_queue`.

## Archivos

**Backend**

| Archivo | Rol |
|---|---|
| `API/Controllers/AuthController.cs` | `POST recover-password` · `POST reset-password` |
| `Infrastructure/Services/AuthService.cs` | Emisión y canje del token |
| `Infrastructure/Services/EmailService.cs` | Encola el correo (no envía) |
| `Application/Correos/CorreosCuenta.cs` | Cuerpo del correo y armado del enlace |
| `Application/Correos/EmailLayout.cs` · `EmailComponentes.cs` · `EmailTema.cs` | Sistema de plantillas |
| `API/BackgroundServices/EmailQueueProcessorService.cs` | **Único** punto que habla con el servidor SMTP |

**Frontend**

| Archivo | Rol |
|---|---|
| `features/auth/password-recovery/` | Pedir el enlace |
| `features/auth/reset-password/` | Canjear el token por una contraseña nueva |
| `core/services/auth/password-recovery.service.ts` | `recoverPassword()` · `resetPassword()` |

## Configuración

Las claves que el código lee de verdad (`appsettings.json` o variables de entorno en ECS con `__`):

```json
{
  "Email": {
    "BrandName": "ItalGranja",
    "Tagline": "Gestión de granjas avícolas · Italcol",
    "LogoUrl": "https://…/logo.png",
    "ApplicationUrl": "https://zootecnico.sanmarino.com.co",
    "Smtp": { "Host": "smtp.office365.com", "Port": "587", "Username": "…", "Password": "…", "EnableSsl": "true" },
    "From": { "Address": "zootecnico@sanmarino.com.co", "Name": "ItalGranja" },
    "Queue": { "Enabled": true }
  }
}
```

`Email:ApplicationUrl` es la base del enlace del correo: si está mal, el enlace lleva al lugar
equivocado. `Email:Queue:Enabled` **está en `false` en `appsettings.Development.json`**: en local los
correos se encolan y nunca se envían salvo que se levante el backend con
`Email__Queue__Enabled=true`.

## Cómo probarlo en local

```bash
ASPNETCORE_ENVIRONMENT=Development PORT=5099 Email__Queue__Enabled=true dotnet run
```

1. `POST http://localhost:5099/api/Auth/recover-password` con `{"email":"<un correo de la tabla logins>"}`.
2. El resultado se mira en la base, no en la respuesta:
   `SELECT id, to_email, status, error_type FROM email_queue ORDER BY id DESC LIMIT 1;`
   El procesador corre cada 30 s: la fila pasa de `pending` a `sent`.
3. El token para armar el enlace a mano:
   `SELECT token, expires_at FROM password_reset_tokens WHERE is_used = false ORDER BY created_at DESC LIMIT 1;`
4. Abrir `http://localhost:4200/reset-password?token=<token>` y fijar la contraseña nueva.

**Nota:** `/api/Auth/reset-password` pasa por `PlatformSecretMiddleware`, así que exige el header
`X-Secret-Up` (el interceptor del frontend lo agrega solo). Para probarlo con curl hay que generarlo:
AES-256-CBC con clave PBKDF2(`PlatformSecret:EncryptionKey`, salt `sanmarino-salt`, 10000, SHA256),
salida `IV(16 bytes) + ciphertext` en base64.

## Si el correo no llega

El orden correcto para diagnosticar:

1. `SELECT status, error_type, error_message FROM email_queue ORDER BY created_at DESC LIMIT 5;`
2. Si dice `smtp_auth` / `5.7.139` / `5.7.57`: **no es la contraseña ni el protocolo**. Es una política
   del tenant de Microsoft 365 que rechaza según el origen de la conexión. Ver
   [`CORREO_PROD_INFORME_TECNICO.md`](CORREO_PROD_INFORME_TECNICO.md).
3. Si no hay ninguna fila nueva: el correo no llegó a encolarse — revisar los registros de la
   aplicación al momento de la solicitud.
4. Si la fila queda en `pending` para siempre: el procesador no está corriendo
   (`Email:Queue:Enabled` en `false`).

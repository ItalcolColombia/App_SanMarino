# Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)

**Fecha:** 2026-08-05
**Motivo:** producción no envía correos. Microsoft retiró la **autenticación básica para SMTP
Client Submission** en Exchange Online (rechazo desde el 1-mar-2026, refuerzo total 30-abr-2026).
El error documentado por Microsoft es `550 5.7.30 Basic authentication is not supported for
Client Submission`.

**Decisión del usuario (05-ago-2026):** transporte destino = **Microsoft Graph API**.

---

## 1. Estado actual (auditado)

| Pieza | Rol | Archivo |
|---|---|---|
| `EmailService` | Arma el HTML y **encola** (bienvenida, recuperación) | `Infrastructure/Services/EmailService.cs` |
| `TicketService` | **Encola** 4 notificaciones de tickets | `Infrastructure/Services/TicketService.cs` |
| `EmailQueueService` | Persiste en la tabla `email_queue` | `Infrastructure/Services/EmailQueueService.cs` |
| **`EmailQueueProcessorService`** | **ÚNICO punto que realmente envía** | `API/BackgroundServices/EmailQueueProcessorService.cs:213-305` |

⇒ **Hay un solo lugar que hablar con el servidor de correo.** Todo lo demás sólo escribe filas
en `email_queue`; el `BackgroundService` las levanta cada 30 s (máx. 10 por ciclo) y las manda.

**El emisor de hoy:** `System.Net.Mail.SmtpClient` + `NetworkCredential(usuario, contraseña)`
contra `smtp.office365.com:587` con STARTTLS.

### Blocker técnico

`System.Net.Mail.SmtpClient` **no implementa XOAUTH2**: no existe forma de pasarle un token OAuth.
Cambiar la contraseña no arregla nada — hay que cambiar el emisor. Por eso el arreglo es de código,
no de configuración.

---

## 2. Enfoque arquitectónico

Se introduce una **abstracción de transporte** en vez de reemplazar el SMTP a lo bruto. Motivo: el
incidente actual es la segunda vez que un cambio del proveedor tumba el correo; con la abstracción,
el próximo cambio es un archivo nuevo + una variable de entorno, no una cirugía.

```
Application/
├── Interfaces/IEmailSender.cs              # contrato del transporte (+ EnvioCorreoResultado)
└── Calculos/EnvioCorreoCalculos.cs         # PURO: resolver proveedor, clasificar errores, armar payload

Infrastructure/Services/Email/
├── SmtpEmailSender.cs                      # el código de HOY, movido tal cual (dev local + rollback)
├── GraphEmailSender.cs                     # POST /v1.0/users/{buzon}/sendMail
└── GraphTokenProvider.cs                   # client_credentials + caché de token

API/BackgroundServices/EmailQueueProcessorService.cs   # delega en IEmailSender; retries/metadata INTACTOS
```

**Sin paquetes NuGet nuevos.** Graph `sendMail` es un POST JSON y el token un POST form-urlencoded:
se resuelve con `HttpClient` (vía `IHttpClientFactory`, ya usado para reCAPTCHA en `Program.cs:165`).
Se evita arrastrar el SDK de Graph + `Azure.Identity` a un build .NET 10 con versiones pinneadas.

### Selección de proveedor (fail-safe)

`EnvioCorreoCalculos.ResolverProveedor(provider, hayGraph, haySmtp)`:

| `Email:Provider` | Config Graph completa | Resultado |
|---|---|---|
| `graph` | sí | **Graph** |
| `graph` | no | `NoConfigurado` (log crítico, correos quedan `pending`) |
| `smtp` | — | **Smtp** |
| vacío / `auto` | sí | **Graph** ← auto-detección |
| vacío / `auto` | no | **Smtp** (comportamiento de hoy) |

⇒ **Dev local no cambia**: `appsettings.Development.json` no tiene sección Graph ⇒ resuelve `Smtp`.

---

## 3. Cambios por archivo

### Nuevos

1. **`Application/Interfaces/IEmailSender.cs`**
   - `Task<EnvioCorreoResultado> EnviarAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)`
   - `record EnvioCorreoResultado(bool Exitoso, string? TipoError, string? Detalle)` — `Detalle` alimenta
     `email_queue.error_message` con el mismo nivel de diagnóstico que hoy.

2. **`Application/Calculos/EnvioCorreoCalculos.cs`** (static, sin I/O)
   - `ResolverProveedor(...)` → `ProveedorCorreo { NoConfigurado, Smtp, Graph }`
   - `ClasificarErrorGraph(int httpStatus, string? codigoGraph)` → `tipo_error` estable
   - `DiagnosticoGraph(int httpStatus, string? codigoGraph)` → texto accionable (qué permiso falta, etc.)
   - `ConstruirPayloadSendMail(from, fromName, to, subject, html, saveToSentItems)` → objeto a serializar
   - `TokenVigente(expiraUtc, ahoraUtc, margen)` → bool

3. **`Infrastructure/Services/Email/GraphTokenProvider.cs`**
   - `POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token`
     con `grant_type=client_credentials`, `scope=https://graph.microsoft.com/.default`
   - Caché en memoria con `SemaphoreSlim`, renovación con **5 min de margen** antes del vencimiento.

4. **`Infrastructure/Services/Email/GraphEmailSender.cs`**
   - `POST https://graph.microsoft.com/v1.0/users/{buzon}/sendMail`, éxito = **202 Accepted**.
   - `from` se envía con la **misma dirección** del buzón de la URL (no requiere SendAs) para conservar
     el nombre visible `Email:From:Name`.
   - Ante 401 invalida el token cacheado y **reintenta una vez** (token revocado a mitad de vuelo).

5. **`Infrastructure/Services/Email/SmtpEmailSender.cs`**
   - Traslado **literal** de `SendEmailAsync` + `BuildSmtpExceptionDetails` + `GetErrorType`.
     Mismos mensajes, mismos `tipo_error`, mismo timeout de 60 s.

### Modificados

6. **`API/BackgroundServices/EmailQueueProcessorService.cs`**
   - Recibe `IEmailSender` por DI; `SendEmailAsync` pasa a ser una llamada delegada.
   - **Se conserva sin tocar**: bucle de 30 s, `Take(10)`, máquina de estados
     `pending→processing→sent/failed`, `RetryCount`, `ErrorType`, metadata con `error_history`,
     y los constructores de mensajes de error.
   - ⚠️ **Cambio deliberado de comportamiento (mejora, se documenta):** el constructor de hoy hace
     `throw` si falta `Email:Smtp:Host/Username/Password`. Como el servicio se registra con
     `AddHostedService`, esa excepción **puede tumbar el arranque de la app en ECS**. Se elimina el
     `throw`: config incompleta ⇒ log crítico + los correos quedan `pending` (recuperable y visible
     en la tabla). Coherente con el historial de crash-loops del proyecto.

7. **`API/Program.cs`**
   - `AddHttpClient("graph-email")` + registro singleton de `GraphTokenProvider`, `SmtpEmailSender`,
     `GraphEmailSender` y del `IEmailSender` resuelto por config.

8. **`appsettings.json` / `appsettings.Development.json`** — sección `Email:Graph` con valores vacíos
   (documentan la forma; los reales van por variables de entorno).

9. **`backend/ecs-taskdef-new-aws.json`** — variables `Email__Provider=graph` y `Email__Graph__*`.
   El `ClientSecret` **NO se commitea**: va como `secrets` de AWS Secrets Manager.

10. **`backend/documentacion/MIGRACION_CORREO_GRAPH_API.md`** — instructivo del app registration.

---

## 4. Configuración nueva

```jsonc
"Email": {
  "Provider": "graph",                 // graph | smtp | auto (default)
  "Graph": {
    "TenantId": "<GUID del tenant>",
    "ClientId": "<GUID de la app>",
    "ClientSecret": "<secreto>",       // ← Secrets Manager, nunca en git
    "SenderMailbox": "zootecnico@sanmarino.com.co",  // default: Email:From:Address
    "SaveToSentItems": "false"
  },
  "Smtp": { /* se conserva para dev local y rollback */ }
}
```

Variables de entorno ECS: `Email__Provider`, `Email__Graph__TenantId`, `Email__Graph__ClientId`,
`Email__Graph__ClientSecret`, `Email__Graph__SenderMailbox`.

**Rollback:** `Email__Provider=smtp` y redeploy. No hay migración ni cambio de esquema que revertir.

---

## 5. Requisito administrativo (bloquea la puesta en producción, no el código)

En **Entra ID** (portal.azure.com → App registrations):

1. Nueva app registration (p. ej. `ItalGranja-Correo`) → anotar **Application (client) ID** y **Directory (tenant) ID**.
2. **Certificates & secrets** → New client secret → copiar el **Value** (se muestra una sola vez).
3. **API permissions** → Microsoft Graph → **Application permissions** → `Mail.Send` →
   **Grant admin consent** (requiere administrador del tenant).
4. **Recomendado (principio de mínimo privilegio):** `Mail.Send` de aplicación habilita el envío desde
   **cualquier** buzón del tenant. Acotarlo con una *Application Access Policy* en Exchange Online:

   ```powershell
   New-ApplicationAccessPolicy -AppId <ClientId> `
     -PolicyScopeGroupId zootecnico@sanmarino.com.co `
     -AccessRight RestrictAccess `
     -Description "ItalGranja: solo el buzon zootecnico"
   ```

---

## 6. Reglas de negocio / invariantes a preservar

- El HTML de los correos (bienvenida, recuperación, tickets) **no se toca**.
- El contrato de `email_queue` no cambia: mismos estados, mismos `tipo_error` para SMTP.
- `Email:Queue:Enabled=false` sigue desactivando el procesador (dev local sin envíos reales).
- El nombre visible del remitente (`Email:From:Name`) se conserva en Graph vía el campo `from`.

## 7. Casos de prueba (xUnit, `tests/ZooSanMarino.Application.Tests/`)

`EnvioCorreoCalculosTests`:

1. `ResolverProveedor`: los 6 casos de la tabla del §2 (incluye **auto sin Graph ⇒ Smtp**, la
   retrocompatibilidad de dev local).
2. `ResolverProveedor` con `provider` en mayúsculas/espacios (`" GRAPH "`) ⇒ Graph.
3. `ClasificarErrorGraph`: 401→`graph_auth`, 403→`graph_permisos`, 404→`graph_buzon`,
   429→`graph_throttling`, 5xx→`graph_transitorio`, otro→`graph_http_{code}`.
4. `DiagnosticoGraph` 403 menciona `Mail.Send` y el consentimiento de administrador.
5. `ConstruirPayloadSendMail`: estructura exacta (`message.body.contentType='HTML'`,
   `toRecipients[0].emailAddress.address`, `from` con dirección del buzón, `saveToSentItems`).
6. `TokenVigente`: vencido, dentro del margen de 5 min, y vigente.
7. Config Graph incompleta (falta secret / falta tenant) ⇒ `hayGraph=false`.

## 8. Validación

- `cd backend && dotnet build` — 0 errores, 0 advertencias nuevas.
- `cd backend && dotnet test` — suite completa verde (1.573 previos + los nuevos).
- Smoke local con `Email:Queue:Enabled=true` y provider `smtp` ⇒ ruta legacy intacta.
- Smoke Graph: sólo posible con las credenciales reales del app registration (paso §5).
  Se deja el procedimiento escrito en la documentación para ejecutarlo al recibirlas.

## 9. Fuera de alcance (se deja anotado, no se ejecuta)

- 🔴 **Credenciales en el repositorio**: la contraseña SMTP está en texto plano en
  `appsettings.json:77`, `appsettings.Development.json:30` y `ecs-taskdef-new-aws.json:48`
  (junto con la cadena de conexión de RDS prod y la clave JWT). Debe rotarse y moverse a
  Secrets Manager — es un trabajo propio, con su propio riesgo de despliegue.
- Migración a SES / proveedor transaccional (evaluada y descartada para este fix: requiere
  verificación DNS del dominio y salida del sandbox).

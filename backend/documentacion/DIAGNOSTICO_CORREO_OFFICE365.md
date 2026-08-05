# Diagnóstico: el correo no sale en producción (Office 365)

**Verificado el 05-ago-2026.** Reemplaza a `SOLUCION_ERROR_535_5.7.139_PRODUCCION.md`,
`SOLUCION_ERROR_SMTP_AUTH.md` y `EXPLICACION_ERROR_535_DETALLADA.md`, cuyas recetas
(habilitar SMTP AUTH, generar una App Password, cambiar la contraseña) **no resuelven este caso**.

---

## 1. El error real

Guardado en `email_queue.error_message`:

```
530 5.7.57 Client not authenticated to send mail.
535 5.7.139 Authentication unsuccessful, the request did not meet the criteria
to be authenticated successfully. Contact your administrator.
```

⚠️ **No es** `550 5.7.30 Basic authentication is not supported`. El retiro global de la
autenticación básica que Microsoft aplicó en marzo-abril de 2026 **no** es la causa acá.

## 2. Qué se probó, y qué quedó descartado

| Hipótesis | Prueba | Veredicto |
|---|---|---|
| Auth básica retirada por Microsoft | Probe SMTP a mano: `EHLO` → `STARTTLS` → `AUTH LOGIN` | ❌ `235 Authentication successful` |
| Contraseña vencida o incorrecta | Las credenciales de producción | ❌ autentican bien |
| Falta forzar la versión de TLS | Handshake con TLS 1.2, 1.3 y default | ❌ los tres autentican |
| TLS implícito en el puerto 465 | Conexión a `smtp.office365.com:465` | ❌ puerto cerrado en Office 365 |
| SMTP AUTH deshabilitado en el buzón | El servidor anuncia `250-AUTH LOGIN XOAUTH2` y acepta el LOGIN | ❌ está habilitado |
| Bug en el código del emisor | Envío real con el bloque `SmtpClient` idéntico, sobre **.NET 10** | ❌ **el correo se envía OK** |

**Credenciales ✅ · código ✅ · protocolo ✅** (587 + STARTTLS).

## 3. Conclusión

El mismo binario, con la misma configuración desplegada, **envía desde la red corporativa y falla
desde donde corre el servidor**. Lo que rechaza la autenticación es una **política del tenant que
depende del origen de la conexión**. El propio mensaje de Exchange lo dice: *"did not meet the
criteria to be authenticated successfully. **Contact your administrator**"* — es una decisión
administrativa, no un problema de credenciales.

### El historial de la cola lo confirma

`SELECT to_char(created_at,'YYYY-MM'), count(*) FILTER (WHERE status='sent'), count(*) FILTER (WHERE status='failed') FROM email_queue GROUP BY 1 ORDER BY 1;`

| Mes | Enviados | Fallidos |
|---|---:|---:|
| 2025-11 | 6 | 8 |
| 2025-12 | 0 | 1 |
| 2026-01 | 0 | 4 |
| 2026-02 | 2 | 0 |
| 2026-03 | 29 | 0 |
| 2026-04 | 6 | 0 |
| 2026-05 | 8 | 0 |
| **2026-06** | **1** | **6** |
| 2026-07 | 0 | 35 |
| 2026-08 | 0 | 6 |

**No es intermitente.** Funcionó sin un solo fallo de febrero a mayo de 2026 (45 correos) y cortó
**de golpe** en junio. El emisor no se tocó en ese período ⇒ **cambió el tenant, no el software**.

Y ya había pasado antes: el bloque nov-2025 / ene-2026 es el mismo síntoma, y se resolvió del lado
administrativo (a partir de febrero el envío volvió solo). Es decir, **este error ya se destrabó una
vez en Microsoft 365, no en el código.**

> ⚠️ **Esto no se arregla con un despliegue.** Ningún cambio de código puede levantar una política
> de Microsoft 365.

## 4. Qué pedirle al administrador de Microsoft 365

### 4.1 Conditional Access / Security Defaults (la causa más probable)

Es lo que explica que funcione desde la oficina y falle desde el servidor. En
**Entra ID → Protección → Acceso condicional**, revisar si hay una política que **bloquee la
autenticación heredada** (*legacy authentication* / *other clients*) por **ubicación o IP de
origen**. Si la hay: excluir el origen del servidor de aplicaciones, o excluir la cuenta de
servicio `zootecnico@sanmarino.com.co`.

También revisar si están activados los **Security Defaults** del tenant (bloquean legacy auth de
forma global).

### 4.2 SMTP AUTH habilitado, por buzón y por organización

```powershell
Connect-ExchangeOnline

Get-CASMailbox 'zootecnico@sanmarino.com.co' | Select SmtpClientAuthenticationDisabled
Get-TransportConfig | Select SmtpClientAuthenticationDisabled
```

Ambos deben devolver **`False`**. Si `Get-TransportConfig` da `True`, está deshabilitado para toda
la organización y el permiso por buzón no alcanza.

### 4.3 Cómo verificar que quedó resuelto

Sin tocar el código: reenviar un correo desde la aplicación y mirar la cola.

```sql
SELECT id, to_email, status, retry_count, error_type,
       left(error_message, 300) AS error, created_at, sent_at
FROM email_queue
ORDER BY created_at DESC
LIMIT 10;
```

`status = 'sent'` ⇒ resuelto. El procesador reintenta cada 30 segundos.

Para reprocesar lo que quedó fallido durante el corte:

```sql
UPDATE email_queue
SET status = 'pending', retry_count = 0, error_message = NULL,
    error_type = NULL, processed_at = NULL, failed_at = NULL
WHERE status = 'failed' AND created_at >= '2026-06-03';
```

⚠️ Revisar antes qué correos son: puede haber contraseñas temporales ya vencidas y tickets viejos.

## 5. Cómo reproducir el diagnóstico

**Probe SMTP a mano** — da el veredicto en 30 segundos, sin suponer nada. Conectar por TCP a
`smtp.office365.com:587`, y enviar en orden: `EHLO`, `STARTTLS`, hacer el handshake TLS, `EHLO` de
nuevo, `AUTH LOGIN` y el usuario/contraseña en base64. La respuesta `235` significa credenciales
válidas; `535 5.7.139` significa rechazo administrativo.

**Envío real sobre .NET 10** — un proyecto de consola con el bloque `SmtpClient` copiado tal cual
de `SmtpEmailSender`. Es la única prueba que vale para este backend: en **.NET Framework** algunas
propiedades de `SmtpClient` se comportan distinto y llevan a conclusiones equivocadas.

## 6. Si hay que migrar a OAuth 2.0

Si la política no se puede levantar —o llegando **diciembre de 2026**, cuando Microsoft retira
definitivamente la auth básica de SMTP— el camino es **Microsoft Graph** con client credentials
(`sendMail` sobre el buzón del sistema), que es inmune a las políticas contra autenticación
heredada.

Esa implementación **existió y se revirtió** para dejar el sistema con un solo transporte. Está en
el historial de git (commit `c7b6834`, `git show c7b6834`) e incluye el emisor por Graph, el
proveedor de token con caché y el instructivo del *app registration* en Entra ID
(permiso de aplicación `Mail.Send` + consentimiento de administrador).

## 7. Estado del código

- **Un solo transporte:** `SmtpEmailSender` (Infrastructure), detrás de `IEmailSender`.
- **Un solo punto de envío real:** `EmailQueueProcessorService`. `EmailService`, `TicketService` y
  `AuthService` sólo encolan en `email_queue`.
- Los diagnósticos que se guardan en la cola ya **no** culpan a la contraseña: indican política del
  tenant y traen los comandos de esta guía.

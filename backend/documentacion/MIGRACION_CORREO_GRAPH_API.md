# Migración del envío de correo a Microsoft Graph API

**Fecha:** 2026-08-05

> Este documento **reemplaza** a `SOLUCION_ERROR_535_5.7.139_PRODUCCION.md` y
> `SOLUCION_ERROR_SMTP_AUTH.md`. Las soluciones de esos documentos (habilitar SMTP AUTH, generar una
> App Password) **ya no funcionan**: Microsoft retiró el mecanismo completo.

---

## 1. Qué pasó

Exchange Online **retiró la autenticación básica para SMTP Client Submission**. El rechazo empezó el
**1-mar-2026** y quedó reforzado por completo el **30-abr-2026**. Cualquier aplicación que se autentique
con usuario + contraseña contra `smtp.office365.com` recibe:

```
550 5.7.30 Basic authentication is not supported for Client Submission
```

También puede aparecer como `535 5.7.139 Authentication unsuccessful`.

**Esto NO se arregla:**

- ❌ cambiando la contraseña
- ❌ generando una App Password
- ❌ habilitando SMTP AUTH con `Set-CASMailbox`
- ❌ tocando `EnableSsl` o el puerto

El mecanismo de autenticación fue eliminado. Hay que usar OAuth 2.0.

## 2. Qué se cambió en el código

`System.Net.Mail.SmtpClient` **no soporta XOAUTH2**, así que no alcanzaba con cambiar credenciales:
hubo que cambiar el emisor.

El envío pasó a hacerse por **Microsoft Graph API**
(`POST https://graph.microsoft.com/v1.0/users/{buzón}/sendMail`) autenticando con el flujo
**client credentials** de Entra ID. Sale por **HTTPS 443**, así que ya no depende de que el puerto
587 esté abierto desde ECS.

El transporte quedó detrás de la interfaz `IEmailSender`, con tres implementaciones:

| Transporte | Cuándo se usa |
|---|---|
| `GraphEmailSender` | Producción (Microsoft 365) |
| `SmtpEmailSender` | Desarrollo local, servidores SMTP propios, **rollback** |
| `SinTransporteEmailSender` | No hay configuración utilizable: los correos quedan en cola con el motivo |

**Lo que NO cambió:** el HTML de los correos, la tabla `email_queue`, los reintentos, y los
`error_type` históricos de la ruta SMTP.

## 3. Qué hay que hacer en Microsoft 365 (requiere administrador del tenant)

### 3.1 Crear el app registration

1. Entrar a <https://portal.azure.com> → **Microsoft Entra ID** → **App registrations** → **New registration**.
2. Nombre: `ItalGranja-Correo` (o el que prefieran). Cuenta: *Accounts in this organizational directory only*.
3. **Register**. En la pantalla *Overview* anotar:
   - **Application (client) ID** → va a `Email__Graph__ClientId`
   - **Directory (tenant) ID** → va a `Email__Graph__TenantId`

### 3.2 Generar el secreto

4. Menú **Certificates & secrets** → **New client secret**.
5. Descripción `ItalGranja backend`, vencimiento (recomendado 24 meses).
6. Copiar **el campo `Value`**, no el `Secret ID`. **Se muestra una sola vez.**
   → va a `Email__Graph__ClientSecret`.

> ⚠️ **Anotar la fecha de vencimiento en el calendario.** Cuando el secreto vence, el correo deja de
> salir con un `401` y el diagnóstico `invalid_client` en `email_queue.error_message`.

### 3.3 Otorgar el permiso

7. Menú **API permissions** → **Add a permission** → **Microsoft Graph**.
8. Elegir **Application permissions** (NO *Delegated*) → buscar y marcar **`Mail.Send`**.
9. **Add permissions** → después **Grant admin consent for \<tenant\>** (botón arriba).
   La columna *Status* debe quedar con el tilde verde **Granted**.

### 3.4 Acotar el alcance (recomendado, no obligatorio)

`Mail.Send` de aplicación habilita el envío desde **cualquier buzón del tenant**. Para restringirlo
sólo al buzón del sistema, ejecutar en Exchange Online PowerShell:

```powershell
Connect-ExchangeOnline

New-ApplicationAccessPolicy `
  -AppId <Application (client) ID> `
  -PolicyScopeGroupId zootecnico@sanmarino.com.co `
  -AccessRight RestrictAccess `
  -Description "ItalGranja: solo el buzon zootecnico"

# Verificar
Test-ApplicationAccessPolicy -Identity zootecnico@sanmarino.com.co -AppId <Application (client) ID>
```

## 4. Configuración de la aplicación

Variables de entorno de la TaskDef de ECS (`backend/ecs-taskdef-new-aws.json`):

| Variable | Valor |
|---|---|
| `Email__Provider` | `auto` (o `graph` para forzarlo) |
| `Email__Graph__TenantId` | Directory (tenant) ID del paso 3.1 |
| `Email__Graph__ClientId` | Application (client) ID del paso 3.1 |
| `Email__Graph__ClientSecret` | el `Value` del paso 3.2 |
| `Email__Graph__SenderMailbox` | `zootecnico@sanmarino.com.co` |
| `Email__Graph__SaveToSentItems` | `false` |

### Cómo elige el transporte

`Email__Provider = auto` (el valor que quedó en la TaskDef) auto-detecta:

- Las 4 variables de Graph completas ⇒ **usa Graph**.
- Alguna vacía ⇒ **usa SMTP** (comportamiento anterior).

⇒ **Desplegar la TaskDef sin llenar las variables no cambia nada**: recién al cargar las credenciales
el sistema conmuta solo. Con `Email__Provider = graph` explícito y config incompleta, en cambio, los
correos quedan en cola con el motivo escrito (no cae a SMTP en silencio, porque volvería a fallar).

### El secreto no debería vivir en el repositorio

`Email__Graph__ClientSecret` quedó como variable de entorno en la TaskDef para no bloquear el
despliegue, pero **lo correcto es moverlo a AWS Secrets Manager**:

```jsonc
// quitar la entrada de "environment" y agregar en el contenedor:
"secrets": [
  {
    "name": "Email__Graph__ClientSecret",
    "valueFrom": "arn:aws:secretsmanager:us-east-2:196080479890:secret:italgranja/email/graph-client-secret"
  }
]
```

El rol `ecsTaskExecutionRole` necesita `secretsmanager:GetSecretValue` sobre ese ARN.

## 5. Verificación después del despliegue

1. **Log de arranque** (CloudWatch, grupo `/ecs/sanmarino-back-task`):
   ```
   📧 Transporte de correo: Microsoft Graph API (buzón zootecnico@sanmarino.com.co)
   🚀 EmailQueueProcessorService iniciado (transporte: graph). ...
   ```
   Si dice `Transporte de correo: SMTP`, las variables de Graph no llegaron.

2. **Probar el flujo real:** recuperación de contraseña con un correo válido.

3. **Mirar la cola** (el procesador corre cada 30 s):
   ```sql
   SELECT id, to_email, status, retry_count, error_type,
          left(error_message, 300) AS error, created_at, sent_at
   FROM email_queue
   ORDER BY created_at DESC
   LIMIT 20;
   ```
   `status = 'sent'` ⇒ funcionando.

4. **Reprocesar los correos que quedaron fallidos** durante el corte (opcional):
   ```sql
   UPDATE email_queue
   SET status = 'pending', retry_count = 0, error_message = NULL,
       error_type = NULL, processed_at = NULL, failed_at = NULL
   WHERE status = 'failed'
     AND created_at >= '2026-03-01';
   ```
   ⚠️ Revisar antes qué correos son: pueden ser contraseñas temporales ya vencidas o tickets viejos.

## 6. Diagnóstico de errores

El motivo completo queda en `email_queue.error_message` y en los logs. Guía rápida:

| `error_type` | Qué significa | Qué hacer |
|---|---|---|
| `graph_token` | Entra ID no dio el token | Revisar TenantId / ClientId / ClientSecret (¿venció?) |
| `graph_auth` (401) | Graph rechazó el token | Ídem; el sistema ya reintenta una vez con token nuevo |
| `graph_permisos` (403) | Falta `Mail.Send` de aplicación, el consentimiento de administrador, o la Application Access Policy no incluye el buzón | Pasos 3.3 y 3.4 |
| `graph_buzon` (404) | `SenderMailbox` no es un buzón real de Exchange Online | Verificar que no sea un alias ni un grupo, y que esté licenciado |
| `graph_throttling` (429) | Límite de envío de Microsoft | Se reintenta solo |
| `graph_transitorio` (5xx) | Falla temporal de Microsoft | Se reintenta solo |
| `sin_transporte` | No hay configuración utilizable | El propio mensaje enumera las variables que faltan |
| `smtp_auth` | Ruta SMTP: auth rechazada | Es el error del retiro de auth básica ⇒ pasar a Graph |

## 7. Rollback

`Email__Provider = smtp` + redeploy. No hay migración ni cambio de esquema que revertir.

⚠️ Contra Office 365 el rollback **no va a enviar** (justamente por el retiro de la auth básica);
sirve para apuntar a otro servidor SMTP.

## 8. Referencias

- [Exchange Online to retire Basic auth for Client Submission (SMTP AUTH)](https://techcommunity.microsoft.com/blog/exchange/exchange-online-to-retire-basic-auth-for-client-submission-smtp-auth/4114750)
- [Microsoft Graph: `user: sendMail`](https://learn.microsoft.com/en-us/graph/api/user-sendmail)
- [Limiting application permissions to specific mailboxes](https://learn.microsoft.com/en-us/graph/auth-limit-mailbox-access)

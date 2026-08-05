> ⛔ **OBSOLETO (05-ago-2026).** Se comprobó que las credenciales SÍ autentican (`235`), que SMTP AUTH
> está habilitado y que este mismo código envía correctamente. Las causas que enumera este documento
> (SMTP AUTH deshabilitado, contraseña incorrecta, App Password) quedaron **descartadas con pruebas**.
> El rechazo viene de una política del tenant según el origen de la conexión.
> 👉 Ver [`DIAGNOSTICO_CORREO_OFFICE365.md`](DIAGNOSTICO_CORREO_OFFICE365.md).

# Explicación Detallada del Error 535 5.7.139

## Error Completo Analizado

```json
{
  "userName": "desarrollo moises",
  "emailType": "password_recovery",
  "last_error": "SMTP Error Details:\n  Status Code: MustIssueStartTlsFirst\n  Message: The SMTP server requires a secure connection or the client was not authenticated. The server response was: 5.7.57 Client not authenticated to send mail. Error: 535 5.7.139 Authentication unsuccessful, the request did not meet the criteria to be authenticated successfully. Contact your administrator. [CH0PR08CA0021.namprd08.prod.outlook.com 2026-01-09T16:12:30.678Z 08DE4F6D4916B5B3]\n  To Email: moisesmurillo@sanmarino.com.co\n  SMTP Host: smtp.office365.com\n  SMTP Port: 587\n  SSL Enabled: True\n  From Email: zootecnico@sanmarino.com.co\n",
  "error_history": "Attempt 3: ...",
  "last_error_at": "2026-01-09 16:12:49 UTC",
  "total_retries": 3
}
```

## 🔍 Análisis del Error

### 1. **Status Code: MustIssueStartTlsFirst**
**Significado:** Office 365 está indicando que necesita establecer una conexión TLS/SSL segura ANTES de intentar autenticarse.

**Estado actual:** ✅ `SSL Enabled: True` - La configuración está correcta
**Problema:** Aunque SSL está habilitado, Office 365 rechaza la autenticación por otra razón (ver abajo)

### 2. **Error 535 5.7.139: Authentication unsuccessful**
**Significado:** Este es el error PRINCIPAL. Office 365 está rechazando las credenciales de autenticación.

**Causas posibles:**
- ❌ **SMTP AUTH no está habilitado** para la cuenta `zootecnico@sanmarino.com.co` en Office 365
- ❌ **Contraseña incorrecta** o necesita ser una **App Password** (si tiene 2FA)
- ❌ **La cuenta no tiene permisos** para enviar correos SMTP

### 3. **Error 5.7.57: Client not authenticated to send mail**
**Significado:** Office 365 está diciendo explícitamente que el cliente (nuestra aplicación) NO está autenticado para enviar correos.

**Esto confirma:** El problema NO es la configuración SSL, sino la **autenticación/autorización**.

### 4. **Configuración Verificada:**
- ✅ `SMTP Host: smtp.office365.com` - Correcto
- ✅ `SMTP Port: 587` - Correcto (puerto para STARTTLS)
- ✅ `SSL Enabled: True` - Correcto (STARTTLS habilitado)
- ✅ `From Email: zootecnico@sanmarino.com.co` - Correcto

### 5. **Intentos Realizados:**
- `total_retries: 3` - Se intentó 3 veces y todas fallaron
- `last_error_at: 2026-01-09 16:12:49 UTC` - Último intento fallido

## 🎯 Conclusión

**El problema NO es la configuración técnica** (SSL, puerto, host están correctos).

**El problema ES la AUTENTICACIÓN:**
- Office 365 está **rechazando las credenciales** porque:
  1. **SMTP AUTH no está habilitado** para la cuenta (más probable)
  2. O la contraseña es incorrecta/necesita App Password

## ✅ Solución Requerida

### Paso 1: Habilitar SMTP AUTH en Office 365 (CRÍTICO)

**Como Administrador de Office 365:**

1. Acceder a: https://admin.microsoft.com
2. Ir a: **Configuración** > **Configuración de correo**
3. Buscar: **Autenticación SMTP** o **SMTP AUTH**
4. Habilitar para: `zootecnico@sanmarino.com.co`
5. Guardar cambios

**O usando PowerShell (como Admin):**
```powershell
Connect-ExchangeOnline
Set-CASMailbox -Identity "zootecnico@sanmarino.com.co" -SmtpClientAuthenticationDisabled $false
```

### Paso 2: Verificar/Actualizar Contraseña

Si la cuenta tiene **autenticación de dos factores (2FA)**:
1. Generar **App Password**: https://account.microsoft.com/security
2. Usar esa App Password en lugar de la contraseña normal
3. Actualizar en la task definition de ECS

### Paso 3: Verificar Permisos

Asegurarse de que la cuenta `zootecnico@sanmarino.com.co` tenga permisos para:
- Enviar correos
- Usar SMTP AUTH

## 📊 Resumen

| Componente | Estado | Acción |
|------------|--------|--------|
| SSL/TLS | ✅ Correcto | Ninguna |
| Puerto | ✅ Correcto | Ninguna |
| Host | ✅ Correcto | Ninguna |
| **SMTP AUTH** | ❌ **NO habilitado** | **HABILITAR en Office 365** |
| Credenciales | ❓ Verificar | Usar App Password si hay 2FA |

## 🔗 Referencias

- [Habilitar SMTP AUTH](https://aka.ms/smtp_auth_disabled)
- [App Passwords](https://support.microsoft.com/es-es/account-billing/crear-y-usar-contraseñas-de-aplicación-para-aplicaciones-que-no-admiten-la-verificación-en-dos-pasos-5896ed9b-4263-e681-128a-a6f2979a7944)


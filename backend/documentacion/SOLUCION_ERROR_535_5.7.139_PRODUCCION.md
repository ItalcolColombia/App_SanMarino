# Solución: Error 535 5.7.139 en Producción - Office 365 SMTP

## Error Observado en Producción

```
Status Code: MustIssueStartTlsFirst
Message: The SMTP server requires a secure connection or the client was not authenticated. 
The server response was: 5.7.57 Client not authenticated to send mail. 
Error: 535 5.7.139 Authentication unsuccessful, the request did not meet the criteria to be authenticated successfully.
```

## Diagnóstico

Este error indica que **Office 365 está rechazando la autenticación SMTP** por una de estas razones:

1. **SMTP AUTH no está habilitado** para la cuenta `zootecnico@sanmarino.com.co`
2. **La contraseña es incorrecta** o necesita ser una **App Password** (si tiene 2FA)
3. **La cuenta no tiene permisos** para enviar correos

## Soluciones (En orden de prioridad)

### ✅ Solución 1: Habilitar SMTP AUTH en Office 365 (REQUERIDO)

**Requisitos:** Acceso de administrador a Office 365

#### Opción A: Portal Web (Recomendado)

1. Acceder al **Centro de administración de Microsoft 365:**
   - URL: https://admin.microsoft.com
   - Iniciar sesión con cuenta de administrador

2. Navegar a configuración de correo:
   - **Configuración** > **Configuración de correo**
   - O directamente: https://admin.microsoft.com/AdminPortal/Home#/Settings/Services/:/Settings/L1/Email

3. Buscar y habilitar **SMTP AUTH:**
   - Buscar la opción "Autenticación SMTP" o "SMTP AUTH"
   - Habilitar para el usuario: `zootecnico@sanmarino.com.co`
   - Guardar cambios

#### Opción B: PowerShell (Para administradores avanzados)

```powershell
# Conectar a Exchange Online
Connect-ExchangeOnline

# Habilitar SMTP AUTH para la cuenta
Set-CASMailbox -Identity "zootecnico@sanmarino.com.co" -SmtpClientAuthenticationDisabled $false

# Verificar que se habilitó
Get-CASMailbox -Identity "zootecnico@sanmarino.com.co" | Select SmtpClientAuthenticationDisabled
# Debe mostrar: SmtpClientAuthenticationDisabled : False
```

### ✅ Solución 2: Usar App Password (Si tiene 2FA habilitado)

Si la cuenta tiene **autenticación de dos factores (2FA)** habilitada, **DEBE** usar una **App Password** en lugar de la contraseña normal.

1. **Generar App Password:**
   - Acceder a: https://account.microsoft.com/security
   - Iniciar sesión con `zootecnico@sanmarino.com.co`
   - Ir a **Seguridad** > **Contraseñas de aplicación**
   - Hacer clic en **Crear una nueva contraseña de aplicación**
   - Nombre: "ZooSanMarino Backend" o similar
   - **Copiar la contraseña generada** (solo se muestra una vez - 16 caracteres sin espacios)

2. **Actualizar configuración en producción:**
   - La contraseña debe actualizarse en las variables de entorno o configuración de producción
   - **NO** actualizar en `appsettings.json` (ese archivo no se usa en producción)
   - Actualizar en:
     - Variables de entorno de ECS Task Definition
     - O en el sistema de configuración de AWS (Parameter Store, Secrets Manager, etc.)

### ✅ Solución 3: Verificar Configuración EnableSsl

Aunque el error menciona "MustIssueStartTlsFirst", la configuración debe ser:

- **Puerto 587:** `EnableSsl = true` (usa STARTTLS)
- **Puerto 465:** `EnableSsl = true` (usa SSL directo)

**Verificar en producción:**
- Variable de entorno: `Email__Smtp__EnableSsl` debe ser `"true"` (string)
- O en configuración: `Email:Smtp:EnableSsl` = `"true"`

## Configuración Actual en Código

```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.office365.com",
      "Port": "587",
      "Username": "zootecnico@sanmarino.com.co",
      "Password": "[DEBE SER APP PASSWORD SI HAY 2FA]",
      "EnableSsl": "true"
    }
  }
}
```

## Verificación Post-Solución

Después de aplicar la solución:

1. **Reiniciar el servicio backend** en ECS para que cargue la nueva configuración
2. **Verificar logs de CloudWatch:**
   - Buscar: `✅ Correo enviado exitosamente`
   - O verificar que no aparezcan más errores 535
3. **Probar el módulo de recuperación de contraseña:**
   - Intentar recuperar contraseña con un email válido
   - Verificar que el correo llegue correctamente

## Logs Mejorados

Con las mejoras implementadas, los logs ahora incluyen:
- Diagnóstico específico del error
- Instrucciones paso a paso para solucionarlo
- Verificación de configuración actual
- Links directos a las herramientas de Office 365

## Referencias

- [Habilitar SMTP AUTH en Office 365](https://aka.ms/smtp_auth_disabled)
- [Configurar App Passwords](https://support.microsoft.com/es-es/account-billing/crear-y-usar-contraseñas-de-aplicación-para-aplicaciones-que-no-admiten-la-verificación-en-dos-pasos-5896ed9b-4263-e681-128a-a6f2979a7944)
- [Troubleshooting SMTP AUTH](https://learn.microsoft.com/en-us/exchange/troubleshoot/configure-mail-clients/smtp-authentication-issues)

## Nota Importante

⚠️ **La solución más común es habilitar SMTP AUTH en Office 365**. Este es un paso administrativo que debe realizarse en el portal de Office 365, no en el código.


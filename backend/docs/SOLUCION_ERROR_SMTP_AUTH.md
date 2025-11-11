# Solución: Error SMTP AUTH en Office 365

## Error
```
5.7.139 Authentication unsuccessful, SmtpClientAuthentication is disabled for the Tenant
```

## Causa
Office 365 tiene deshabilitada la autenticación SMTP básica (usuario/contraseña) para el tenant.

## Soluciones

### Opción 1: Habilitar SMTP AUTH en Office 365 (Recomendado para solución rápida)

**Requisitos:** Permisos de administrador en Office 365

1. **Acceder al Centro de administración de Microsoft 365:**
   - Ve a https://admin.microsoft.com
   - Inicia sesión con una cuenta de administrador

2. **Habilitar SMTP AUTH:**
   - Ve a **Configuración** > **Configuración de correo**
   - O directamente: https://admin.microsoft.com/AdminPortal/Home#/Settings/Services/:/Settings/L1/Email
   - Busca **Autenticación SMTP** o **SMTP AUTH**
   - Habilita la opción para el usuario `zootecnico@sanmarino.com.co`

3. **Alternativa usando PowerShell (para administradores):**
   ```powershell
   Connect-ExchangeOnline
   Set-CASMailbox -Identity "zootecnico@sanmarino.com.co" -SmtpClientAuthenticationDisabled $false
   ```

### Opción 2: Usar App Password (Contraseña de aplicación)

Si la cuenta tiene autenticación de dos factores (2FA) habilitada, necesitas usar una "App Password" en lugar de la contraseña normal.

1. **Generar App Password:**
   - Ve a https://account.microsoft.com/security
   - Inicia sesión con `zootecnico@sanmarino.com.co`
   - Ve a **Seguridad** > **Contraseñas de aplicación**
   - Genera una nueva contraseña de aplicación
   - **Copia la contraseña generada** (solo se muestra una vez)

2. **Actualizar configuración:**
   - Reemplaza la contraseña en `appsettings.json` con la App Password generada
   - La contraseña será una cadena de 16 caracteres sin espacios

### Opción 3: Usar Microsoft Graph API (Solución moderna y segura)

Esta es la solución más segura y recomendada para producción, pero requiere más configuración.

**Ventajas:**
- No requiere SMTP AUTH
- Más seguro (OAuth2)
- Mejor para producción

**Desventajas:**
- Requiere registro de aplicación en Azure AD
- Requiere cambios en el código

## Configuración Actual

La configuración actual en `appsettings.json`:
```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.office365.com",
      "Port": "587",
      "Username": "zootecnico@sanmarino.com.co",
      "Password": "bvzlsfqddklxldkv",
      "EnableSsl": "true"
    }
  }
}
```

## Verificación

Después de aplicar la solución:

1. **Reinicia el backend** para que cargue la nueva configuración
2. **Verifica los logs** - deberías ver:
   ```
   ✅ Correo enviado exitosamente: ID=X, To=email@example.com
   ```
3. **Prueba creando un nuevo usuario** y verifica que el correo se envíe correctamente

## Referencias

- [Habilitar SMTP AUTH en Office 365](https://aka.ms/smtp_auth_disabled)
- [Configurar App Passwords](https://support.microsoft.com/es-es/account-billing/crear-y-usar-contraseñas-de-aplicación-para-aplicaciones-que-no-admiten-la-verificación-en-dos-pasos-5896ed9b-4263-e681-128a-a6f2979a7944)
- [Microsoft Graph API para envío de correos](https://learn.microsoft.com/en-us/graph/api/user-sendmail)



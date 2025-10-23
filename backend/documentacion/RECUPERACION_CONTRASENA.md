# Funcionalidad de Recuperación de Contraseña

## Descripción
Sistema completo de recuperación de contraseña que permite a los usuarios solicitar una nueva contraseña temporal por correo electrónico cuando olvidan su contraseña actual.

## Características Implementadas

### Backend (.NET Core)
- ✅ Servicio de email con configuración flexible
- ✅ Endpoint de recuperación de contraseña
- ✅ Generación de contraseñas aleatorias seguras
- ✅ Validación de usuarios existentes
- ✅ Envío de emails HTML con diseño profesional
- ✅ Manejo de errores robusto

### Frontend (Angular)
- ✅ Componente de recuperación de contraseña
- ✅ Servicio para comunicación con el backend
- ✅ Integración con el login existente
- ✅ Diseño responsive y accesible
- ✅ Estados de carga y feedback visual

## Archivos Creados/Modificados

### Backend
```
src/ZooSanMarino.Application/Interfaces/IEmailService.cs
src/ZooSanMarino.Infrastructure/Services/EmailService.cs
src/ZooSanMarino.Application/DTOs/PasswordRecoveryDto.cs
src/ZooSanMarino.Infrastructure/Services/AuthService.cs (modificado)
src/ZooSanMarino.API/Controllers/AuthController.cs (modificado)
src/ZooSanMarino.API/Program.cs (modificado)
src/ZooSanMarino.API/appsettings.json (modificado)
src/ZooSanMarino.API/appsettings.Development.json (modificado)
```

### Frontend
```
src/app/core/services/auth/password-recovery.service.ts
src/app/features/auth/password-recovery/password-recovery.component.ts
src/app/features/auth/password-recovery/password-recovery.component.html
src/app/features/auth/password-recovery/password-recovery.component.scss
src/app/features/auth/login/login.component.ts (modificado)
src/app/features/auth/login/login.component.html (modificado)
src/app/features/auth/login/login.component.scss (modificado)
src/app/app.config.ts (modificado)
```

## Configuración Requerida

### 1. Configuración de Email
Agregar en `appsettings.json` o `appsettings.Development.json`:

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "tu-email@gmail.com",
    "SmtpPassword": "tu-app-password",
    "FromEmail": "tu-email@gmail.com",
    "FromName": "Zoo San Marino"
  }
}
```

### 2. Variables de Entorno (Alternativa)
```bash
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=tu-email@gmail.com
SMTP_PASSWORD=tu-app-password
FROM_EMAIL=tu-email@gmail.com
FROM_NAME=Zoo San Marino
```

## Uso

### 1. Desde el Frontend
1. Ir a la página de login (`/login`)
2. Hacer clic en "¿Olvidaste tu contraseña?"
3. Ingresar el correo electrónico
4. Hacer clic en "Enviar Nueva Contraseña"
5. Revisar el correo electrónico
6. Usar la nueva contraseña para iniciar sesión

### 2. Desde la API
```bash
POST /api/Auth/recover-password
Content-Type: application/json

{
  "email": "usuario@ejemplo.com"
}
```

**Respuesta exitosa:**
```json
{
  "success": true,
  "message": "Se ha enviado una nueva contraseña a tu correo electrónico",
  "userFound": true,
  "emailSent": true
}
```

## Flujo de Funcionamiento

1. **Solicitud**: Usuario ingresa su email en el formulario
2. **Validación**: Backend verifica que el usuario existe y está activo
3. **Generación**: Se genera una contraseña aleatoria de 12 caracteres
4. **Actualización**: Se actualiza la contraseña en la base de datos
5. **Envío**: Se envía un email HTML con la nueva contraseña
6. **Confirmación**: Se muestra mensaje de éxito al usuario

## Seguridad

- ✅ Contraseñas generadas con caracteres seguros
- ✅ Validación de usuarios existentes
- ✅ Verificación de estado activo del usuario
- ✅ Manejo seguro de errores
- ✅ No exposición de información sensible

## Personalización

### Email Template
El template del email se puede personalizar en `EmailService.cs` en el método `SendPasswordRecoveryEmailAsync()`.

### Estilos del Frontend
Los estilos se pueden modificar en `password-recovery.component.scss`.

### Configuración de Contraseña
La longitud y caracteres de la contraseña se pueden modificar en el método `GenerateRandomPassword()`.

## Pruebas

### Backend
1. Configurar credenciales de email
2. Iniciar el backend
3. Ir a `/swagger`
4. Probar el endpoint `POST /api/Auth/recover-password`

### Frontend
1. Iniciar el frontend
2. Ir a `/password-recovery`
3. Ingresar un email válido
4. Verificar que se muestre el mensaje de éxito
5. Revisar el correo electrónico

## Troubleshooting

### Error de Email
- Verificar credenciales SMTP
- Comprobar configuración de firewall
- Revisar logs del backend

### Usuario No Encontrado
- Verificar que el email existe en la base de datos
- Comprobar que el usuario está activo

### Frontend No Responde
- Verificar que la ruta está configurada correctamente
- Comprobar que el servicio está inyectado
- Revisar la consola del navegador

## Mejoras Futuras

- [ ] Implementar tokens de expiración para contraseñas temporales
- [ ] Agregar rate limiting para prevenir spam
- [ ] Implementar logs de auditoría
- [ ] Agregar notificaciones push
- [ ] Implementar verificación por SMS como alternativa




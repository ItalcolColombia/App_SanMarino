# Validación de reCAPTCHA - Configuración Completa

## Credenciales Configuradas

### Backend (appsettings.json)
- **SiteKey**: `6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA`
- **SecretKey**: `6LdjOggsAAAAAC5pp71MI12x_d1stTIIBlxbMIXo`
- **Enabled**: `true`

### Frontend (environment.prod.ts)
- **SiteKey**: `6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA`
- **Enabled**: `true`

## Flujo de Validación

### 1. Frontend - Login Component
1. Usuario ingresa email y contraseña
2. Si está en producción, se muestra el widget de reCAPTCHA
3. Usuario completa el reCAPTCHA
4. Se obtiene el token de reCAPTCHA
5. El token se incluye en el payload de login
6. Los datos se encriptan y se envían al backend

**Archivo**: `frontend/src/app/features/auth/login/login.component.ts`

### 2. Backend - AuthController
1. Recibe la petición encriptada
2. Desencripta los datos
3. Valida que el token de reCAPTCHA esté presente (solo en producción)
4. Llama al servicio de reCAPTCHA para validar con Google
5. Si la validación falla, retorna error
6. Si la validación es exitosa, procede con el login

**Archivo**: `backend/src/ZooSanMarino.API/Controllers/AuthController.cs`

### 3. Backend - RecaptchaService
1. Verifica que reCAPTCHA esté habilitado
2. Valida que el token no esté vacío
3. Envía petición a Google reCAPTCHA API
4. Valida la respuesta:
   - `Success` debe ser `true`
   - Si es reCAPTCHA v3, el `Score` debe ser >= 0.5
5. Retorna `true` si es válido, `false` en caso contrario

**Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/RecaptchaService.cs`

## Configuración por Ambiente

### Desarrollo
- **Backend**: `appsettings.Development.json` - `Enabled: false`
- **Frontend**: `environment.ts` - `enabled: false`
- **Comportamiento**: reCAPTCHA deshabilitado, login funciona sin validación

### Producción
- **Backend**: `appsettings.json` - `Enabled: true`
- **Frontend**: `environment.prod.ts` - `enabled: true`
- **Comportamiento**: reCAPTCHA requerido, validación obligatoria

## Validaciones Implementadas

### Frontend
- ✅ Widget de reCAPTCHA se muestra solo en producción
- ✅ Validación de token antes de enviar login
- ✅ Mensaje de error si falta el token
- ✅ Token se incluye en el payload de login

### Backend
- ✅ Validación solo en producción
- ✅ Verificación de token con Google
- ✅ Validación de score para reCAPTCHA v3
- ✅ Logging de intentos fallidos
- ✅ Manejo de errores con mensajes descriptivos

## Pruebas de Validación

### Prueba 1: Login sin reCAPTCHA en Producción
**Esperado**: Error "Validación de seguridad requerida"

### Prueba 2: Login con reCAPTCHA inválido
**Esperado**: Error "Validación de seguridad fallida"

### Prueba 3: Login con reCAPTCHA válido
**Esperado**: Login exitoso

### Prueba 4: Login en Desarrollo
**Esperado**: Login sin reCAPTCHA (funciona normalmente)

## Verificación de Configuración

### Backend
```json
{
  "Recaptcha": {
    "Enabled": true,
    "SiteKey": "6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA",
    "SecretKey": "6LdjOggsAAAAAC5pp71MI12x_d1stTIIBlxbMIXo"
  }
}
```

### Frontend
```typescript
recaptcha: {
  enabled: true,
  siteKey: '6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA'
}
```

## Notas Importantes

1. **reCAPTCHA v3**: El código soporta reCAPTCHA v3 con validación de score
2. **IP del Cliente**: Se envía la IP del cliente a Google para mejor validación
3. **Timeout**: 10 segundos para la petición a Google
4. **Score Mínimo**: 0.5 para acciones como login (configurable)

## Troubleshooting

### Error: "Validación de seguridad requerida"
- Verificar que el widget de reCAPTCHA se esté mostrando
- Verificar que el token se esté generando correctamente
- Revisar consola del navegador para errores de JavaScript

### Error: "Validación de seguridad fallida"
- Verificar que las credenciales sean correctas
- Verificar que el dominio esté registrado en Google reCAPTCHA
- Revisar logs del backend para ver el error específico de Google

### reCAPTCHA no se muestra
- Verificar que `environment.production === true`
- Verificar que `recaptcha.enabled === true`
- Verificar que `recaptcha.siteKey` no esté vacío
- Verificar que el módulo `ng-recaptcha` esté instalado





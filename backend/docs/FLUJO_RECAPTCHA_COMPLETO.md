# Flujo Completo de reCAPTCHA - Validación

## ✅ Configuración Aplicada

### Backend
- **Archivo**: `appsettings.json`
- **SiteKey**: `6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA` ✅
- **SecretKey**: `6LdjOggsAAAAAC5pp71MI12x_d1stTIIBlxbMIXo` ✅
- **Enabled**: `true` ✅

### Frontend
- **Archivo**: `environment.prod.ts`
- **SiteKey**: `6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA` ✅
- **Enabled**: `true` ✅

## 🔄 Flujo de Validación Completo

### Paso 1: Usuario en Frontend
1. Usuario accede a la página de login
2. Si `environment.production === true` y `recaptcha.enabled === true`:
   - Se muestra el widget de reCAPTCHA
   - Usuario completa el desafío
   - Se genera un token de reCAPTCHA
3. Usuario ingresa email y contraseña
4. Al hacer submit:
   - Se valida que el token de reCAPTCHA esté presente (si está habilitado)
   - Se incluye el token en el payload: `{ email, password, recaptchaToken }`
   - Los datos se encriptan
   - Se envía al backend

**Código**: `login.component.ts` líneas 65-103

### Paso 2: Backend Recibe Petición
1. `AuthController.Login()` recibe la petición encriptada
2. Desencripta los datos usando `EncryptionService`
3. Obtiene el `LoginDto` con `RecaptchaToken`
4. Valida datos básicos (email, password)

**Código**: `AuthController.cs` líneas 53-87

### Paso 3: Validación de reCAPTCHA (Solo Producción)
1. Verifica que esté en producción: `isProduction && recaptchaEnabled`
2. Si está en producción:
   - Verifica que el token no esté vacío
   - Obtiene la IP del cliente
   - Llama a `RecaptchaService.ValidateRecaptchaAsync()`

**Código**: `AuthController.cs` líneas 95-120

### Paso 4: Validación con Google
1. `RecaptchaService` verifica configuración:
   - `_isEnabled` debe ser `true`
   - `_secretKey` no debe estar vacío
2. Prepara petición a Google:
   - URL: `https://www.google.com/recaptcha/api/siteverify`
   - Body: `{ secret, response, remoteip }`
3. Envía petición HTTP POST
4. Procesa respuesta:
   - `Success` debe ser `true`
   - Si es v3, `Score >= 0.5`
5. Retorna `true` si es válido, `false` en caso contrario

**Código**: `RecaptchaService.cs` líneas 43-139

### Paso 5: Resultado
- ✅ **Válido**: Continúa con el proceso de login normal
- ❌ **Inválido**: Retorna `400 Bad Request` con mensaje de error

## 🧪 Casos de Prueba

### Caso 1: Login en Producción SIN reCAPTCHA
**Input**: Email, Password, Sin token
**Esperado**: `400 Bad Request` - "Validación de seguridad requerida"

### Caso 2: Login en Producción CON reCAPTCHA inválido
**Input**: Email, Password, Token inválido/expirado
**Esperado**: `400 Bad Request` - "Validación de seguridad fallida"

### Caso 3: Login en Producción CON reCAPTCHA válido
**Input**: Email, Password, Token válido
**Esperado**: `200 OK` - Login exitoso

### Caso 4: Login en Desarrollo
**Input**: Email, Password (sin token)
**Esperado**: `200 OK` - Login exitoso (reCAPTCHA omitido)

## 🔍 Verificación de Implementación

### Frontend ✅
- [x] Módulo `ng-recaptcha` instalado (v13.2.1)
- [x] Widget implementado en `login.component.html`
- [x] Token capturado en `onRecaptchaResolved()`
- [x] Token incluido en payload de login
- [x] Validación antes de enviar
- [x] SiteKey configurado en `environment.prod.ts`

### Backend ✅
- [x] Servicio `RecaptchaService` implementado
- [x] Validación en `AuthController`
- [x] Configuración en `appsettings.json`
- [x] Validación con Google API
- [x] Manejo de errores
- [x] Logging de intentos

## 📋 Checklist de Validación

### Configuración
- [x] SiteKey configurado en frontend
- [x] SecretKey configurado en backend
- [x] Enabled = true en producción
- [x] Enabled = false en desarrollo

### Funcionalidad
- [x] Widget se muestra en producción
- [x] Widget NO se muestra en desarrollo
- [x] Token se captura correctamente
- [x] Token se envía al backend
- [x] Backend valida con Google
- [x] Errores se manejan correctamente

### Seguridad
- [x] Validación solo en producción
- [x] IP del cliente se envía a Google
- [x] Score mínimo validado (v3)
- [x] Logging de intentos fallidos

## 🚨 Troubleshooting

### Problema: reCAPTCHA no se muestra
**Solución**:
1. Verificar `environment.production === true`
2. Verificar `recaptcha.enabled === true`
3. Verificar `recaptcha.siteKey` no está vacío
4. Verificar que `ng-recaptcha` esté instalado

### Problema: "Validación de seguridad requerida"
**Solución**:
1. Verificar que el widget se haya completado
2. Verificar que el token se esté generando
3. Revisar consola del navegador
4. Verificar que el token se incluya en el payload

### Problema: "Validación de seguridad fallida"
**Solución**:
1. Verificar que las credenciales sean correctas
2. Verificar que el dominio esté registrado en Google
3. Revisar logs del backend para ver error específico
4. Verificar que no haya problemas de red con Google

## 📝 Notas Adicionales

1. **reCAPTCHA v2 vs v3**: El código soporta ambos, pero valida score para v3
2. **Timeout**: 10 segundos para petición a Google
3. **Score Mínimo**: 0.5 para acciones como login (recomendado por Google)
4. **IP del Cliente**: Se obtiene de headers `X-Forwarded-For` o `X-Real-IP` si está detrás de proxy






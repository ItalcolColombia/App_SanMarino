# Seguridad Aplicada al Login y Conexión con APIs

## 📋 Índice
1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Sistema de Encriptación del Login](#sistema-de-encriptación-del-login)
3. [Sistema SECRET_UP para Validación de Peticiones](#sistema-secret_up-para-validación-de-peticiones)
4. [Configuración](#configuración)
5. [Flujo Completo de Seguridad](#flujo-completo-de-seguridad)
6. [Arquitectura de Seguridad](#arquitectura-de-seguridad)

---

## 🎯 Resumen Ejecutivo

El sistema implementa una seguridad multicapa para proteger el login y todas las comunicaciones entre el frontend y el backend:

1. **Encriptación AES-256-CBC del Login**: Los datos de login (email, contraseña) se encriptan antes de ser enviados al backend.
2. **SECRET_UP Encriptado**: Todas las peticiones HTTP deben incluir un SECRET_UP encriptado que identifica la plataforma autorizada.
3. **Separación del Menú**: El menú se carga en una segunda petición separada para reducir el tamaño de la respuesta encriptada del login.

---

## 🔐 Sistema de Encriptación del Login

### Descripción

El sistema de login utiliza encriptación AES-256-CBC para proteger los datos sensibles (email y contraseña) durante la transmisión.

### Componentes

#### Frontend

- **Archivo**: `frontend/src/app/core/auth/encryption.service.ts`
- **Método**: `encryptForBackend<T>(data: T)`
- **Algoritmo**: AES-256-CBC usando Web Crypto API
- **Derivación de llave**: PBKDF2 con 10,000 iteraciones y SHA-256

#### Backend

- **Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/EncryptionService.cs`
- **Método**: `DecryptFromFrontend<T>(string encryptedData)`
- **Algoritmo**: AES-256-CBC usando `System.Security.Cryptography`
- **Derivación de llave**: PBKDF2 con 10,000 iteraciones y SHA-256

### Llaves de Encriptación

#### Frontend → Backend (Login Request)

- **Llave Frontend**: `RemitenteFrontend` - Usada para encriptar datos enviados al backend
- **Ubicación Frontend**: `environment.ts` → `encryptionKeys.remitenteFrontend`
- **Ubicación Backend**: `appsettings.json` → `Encryption:RemitenteFrontend`

#### Backend → Frontend (Login Response)

- **Llave Backend**: `RemitenteBackend` - Usada para encriptar respuestas enviadas al frontend
- **Ubicación Frontend**: `environment.ts` → `encryptionKeys.remitenteBackend`
- **Ubicación Backend**: `appsettings.json` → `Encryption:RemitenteBackend`

### Flujo del Login Encriptado

```
┌─────────────┐                    ┌─────────────┐
│   Frontend   │                    │   Backend   │
└──────┬───────┘                    └──────┬─────┘
       │                                     │
       │ 1. Usuario ingresa email/password  │
       │                                     │
       │ 2. encryptForBackend(loginDto)      │
       │    → AES-256-CBC                   │
       │    → Base64                         │
       │                                     │
       │ 3. POST /api/Auth/login            │
       │    Body: { encryptedData: "..." }  │
       ──────────────────────────────────────>
       │                                     │
       │                       4. DecryptFromFrontend<LoginDto>
       │                          → Deserializa JSON
       │                          → Valida credenciales
       │                                     │
       │                       5. EncryptForFrontend(AuthResponseDto)
       │                          → Token, usuario, roles, permisos
       │                                     │
       │ 6. Respuesta encriptada (text/plain)│
<──────────────────────────────────────────────
       │                                     │
       │ 7. decryptFromBackend<LoginResult> │
       │    → Guarda en sessionStorage      │
       │    → Redirige a /home              │
       │                                     │
```

### Formato de Datos

**Request Encriptado:**
```json
{
  "encryptedData": "IV(16 bytes) + EncryptedData en Base64"
}
```

**Response Encriptado:**
```
"IV(16 bytes) + EncryptedData en Base64" (text/plain)
```

---

## 🔒 Sistema SECRET_UP para Validación de Peticiones

### Descripción

Todas las peticiones HTTP desde el frontend deben incluir un SECRET_UP encriptado que identifica la plataforma autorizada. El backend valida este SECRET_UP antes de procesar cualquier petición.

### Componentes

#### Frontend

- **Archivo**: `frontend/src/app/core/auth/auth.interceptor.ts`
- **Proceso**: Intercepta todas las peticiones HTTP y agrega el header `X-Secret-Up` con el SECRET_UP encriptado
- **Método**: `encryptSecretUp(secretUp: string)` en `EncryptionService`

#### Backend

- **Middleware**: `backend/src/ZooSanMarino.API/Middleware/PlatformSecretMiddleware.cs`
- **Validación**: Desencripta y valida el SECRET_UP antes de que la petición llegue a los controladores
- **Orden**: Se ejecuta después de CORS pero antes de Authentication/Authorization

### Llaves SECRET_UP

#### SECRET_UP Frontend

- **Valor**: Identificador único de la plataforma frontend
- **Ubicación Frontend**: `environment.ts` → `platformSecret.secretUpFrontend`
- **Ubicación Backend**: `appsettings.json` → `PlatformSecret:SecretUpFrontend`

#### Llave de Encriptación SECRET_UP

- **Valor**: Llave específica para encriptar/desencriptar el SECRET_UP
- **Ubicación Frontend**: `environment.ts` → `platformSecret.encryptionKey`
- **Ubicación Backend**: `appsettings.json` → `PlatformSecret:EncryptionKey`

### Flujo de Validación SECRET_UP

```
┌─────────────┐                    ┌─────────────┐
│   Frontend   │                    │   Backend   │
└──────┬───────┘                    └──────┬─────┘
       │                                     │
       │ 1. Cualquier petición HTTP          │
       │    (GET, POST, PUT, DELETE, etc.)   │
       │                                     │
       │ 2. authInterceptor intercepta      │
       │    → encryptSecretUp(secretUp)     │
       │    → Header: X-Secret-Up           │
       │                                     │
       │ 3. HTTP Request con headers         │
       │    X-Secret-Up: "encriptado..."    │
       │    Authorization: "Bearer token"   │
       ──────────────────────────────────────>
       │                                     │
       │             4. PlatformSecretMiddleware
       │                → Lee X-Secret-Up header
       │                → Decrypt(encrypted, EncryptionKey)
       │                → Compara con SecretUpFrontend
       │                                     │
       │             ¿Válido?                │
       │           /        \               │
       │        SÍ            NO             │
       │       │               │             │
       │       │               └─→ 401       │
       │       │                   Unauthorized
       │       │                             │
       │ 5. Continúa al controlador         │
       │                                     │
```

### Headers Requeridos

Todas las peticiones deben incluir:

```
X-Secret-Up: [SECRET_UP encriptado en Base64]
Authorization: Bearer [JWT Token] (si está autenticado)
X-Active-Company: [Nombre de empresa activa] (opcional)
```

### Endpoints Exentos

Los siguientes endpoints NO requieren SECRET_UP:

- `OPTIONS` requests (preflight CORS)
- `/ping` o `/ping-simple`
- `/health` o `/hc`

### Respuestas de Error

#### SECRET_UP No Proporcionado

```json
{
  "error": "Unauthorized",
  "message": "SECRET_UP no proporcionado en el header X-Secret-Up"
}
```
**Status Code**: `401 Unauthorized`

#### SECRET_UP Inválido o Error de Desencriptación

```json
{
  "error": "Unauthorized",
  "message": "SECRET_UP inválido" | "Error al desencriptar SECRET_UP"
}
```
**Status Code**: `401 Unauthorized`

---

## ⚙️ Configuración

### Backend (`appsettings.json`)

```json
{
  "Encryption": {
    "RemitenteFrontend": "pR7@xW2!dN#9mZ$eH8&",
    "RemitenteBackend": "Q5#vF1@pG*0bT$yK9!r"
  },
  "PlatformSecret": {
    "SecretUpFrontend": "FRONTEND_SECRET_2024_SANMARINO_X9K2@mL7$pN",
    "SecretUpBackend": "BACKEND_SECRET_2024_SANMARINO_V3M8#nT5&wQ",
    "EncryptionKey": "SECRET_UP_ENCRYPTION_KEY_2024_SANMARINO_K9P@xM3#vN"
  }
}
```

### Frontend (`environment.ts` / `environment.prod.ts`)

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5002/api',
  encryptionKeys: {
    remitenteFrontend: 'pR7@xW2!dN#9mZ$eH8&',
    remitenteBackend: 'Q5#vF1@pG*0bT$yK9!r'
  },
  platformSecret: {
    secretUpFrontend: 'FRONTEND_SECRET_2024_SANMARINO_X9K2@mL7$pN',
    secretUpBackend: 'BACKEND_SECRET_2024_SANMARINO_V3M8#nT5&wQ',
    encryptionKey: 'SECRET_UP_ENCRYPTION_KEY_2024_SANMARINO_K9P@xM3#vN'
  }
};
```

### Registro de Servicios (Backend)

```csharp
// Program.cs
builder.Services.AddSingleton<EncryptionService>(); // Singleton para uso en middleware
```

### Registro de Middleware (Backend)

```csharp
// Program.cs
app.UseRouting();
app.UseCors("AppCors");
app.UsePlatformSecret(); // Valida SECRET_UP antes de Authentication
app.UseAuthentication();
app.UseAuthorization();
```

---

## 🔄 Flujo Completo de Seguridad

### 1. Login del Usuario

```
Usuario → Frontend
  ↓
[Email, Password]
  ↓
encryptForBackend({ email, password })
  ↓
POST /api/Auth/login { encryptedData: "..." }
  ↓ (SECRET_UP encriptado en header)
Backend: PlatformSecretMiddleware
  ↓ (Valida SECRET_UP)
Backend: DecryptFromFrontend<LoginDto>
  ↓
Backend: Validar credenciales
  ↓
Backend: EncryptForFrontend(AuthResponseDto)
  ↓ (Token, Usuario, Roles, Permisos)
Frontend: decryptFromBackend<LoginResult>
  ↓
Frontend: Guardar en sessionStorage
  ↓
Frontend: Redirigir a /home
```

### 2. Carga del Menú (Segunda Petición)

```
HomeComponent → Frontend
  ↓
GET /api/Auth/menu
  ↓ (SECRET_UP encriptado + JWT token)
Backend: PlatformSecretMiddleware
  ↓ (Valida SECRET_UP)
Backend: Validar JWT
  ↓
Backend: Obtener menú del usuario
  ↓
Backend: EncryptForFrontend({ menu, menusByRole })
  ↓
Frontend: decryptFromBackend
  ↓
Frontend: Actualizar sesión con menú
```

### 3. Peticiones Subsecuentes

```
Frontend → Cualquier petición HTTP
  ↓
authInterceptor intercepta
  ↓
Agrega headers:
  - X-Secret-Up: [encriptado]
  - Authorization: Bearer [token]
  - X-Active-Company: [empresa]
  ↓
Backend: PlatformSecretMiddleware
  ↓ (Valida SECRET_UP)
Backend: Continúa al controlador
```

---

## 🏗️ Arquitectura de Seguridad

### Capas de Seguridad

```
┌─────────────────────────────────────────────────────┐
│                 FRONTEND                            │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  authInterceptor                            │   │
│  │  - Agrega SECRET_UP encriptado              │   │
│  │  - Agrega JWT token                         │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  EncryptionService                          │   │
│  │  - encryptForBackend()                      │   │
│  │  - decryptFromBackend()                     │   │
│  │  - encryptSecretUp()                        │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
└─────────────────────────────────────────────────────┘
                      │
                      │ HTTPS
                      │
┌─────────────────────────────────────────────────────┐
│                 BACKEND                             │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  PlatformSecretMiddleware                   │   │
│  │  - Valida SECRET_UP encriptado              │   │
│  │  - Rechaza peticiones no autorizadas       │   │
│  └─────────────────────────────────────────────┘   │
│                      ↓                               │
│  ┌─────────────────────────────────────────────┐   │
│  │  Authentication/Authorization                │   │
│  │  - Valida JWT token                         │   │
│  │  - Verifica permisos                        │   │
│  └─────────────────────────────────────────────┘   │
│                      ↓                               │
│  ┌─────────────────────────────────────────────┐   │
│  │  EncryptionService                          │   │
│  │  - DecryptFromFrontend()                    │   │
│  │  - EncryptForFrontend()                     │   │
│  │  - Decrypt() (para SECRET_UP)               │   │
│  └─────────────────────────────────────────────┘   │
│                      ↓                               │
│  ┌─────────────────────────────────────────────┐   │
│  │  Controllers                                │   │
│  │  - Procesan peticiones autorizadas          │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Matriz de Seguridad

| Componente | Encriptación Login | SECRET_UP | JWT Token | Resultado |
|------------|-------------------|-----------|-----------|-----------|
| Login Request | ✅ Sí | ✅ Sí | ❌ No | Permite login |
| Login Response | ✅ Sí | N/A | N/A | Datos protegidos |
| Carga de Menú | ✅ Sí | ✅ Sí | ✅ Sí | Menú autorizado |
| Otras APIs | N/A | ✅ Sí | ✅ Sí | Acceso autorizado |
| Sin SECRET_UP | N/A | ❌ No | ❌ No | **401 Rechazado** |
| SECRET_UP Inválido | N/A | ❌ No | ❌ No | **401 Rechazado** |

---

## 🔑 Gestión de Llaves

### Buenas Prácticas

1. **Nunca commitear llaves en el repositorio**
   - Usar variables de entorno en producción
   - Usar `.env` en desarrollo (añadir a `.gitignore`)

2. **Rotación de llaves**
   - Rotar llaves periódicamente (cada 3-6 meses)
   - Comunicar cambios con suficiente anticipación
   - Mantener versiones anteriores durante periodo de transición

3. **Separación de ambientes**
   - Llaves diferentes para desarrollo, staging y producción
   - Usar `appsettings.Development.json` para desarrollo
   - Usar variables de entorno o Azure Key Vault en producción

### Llaves Actuales (Desarrollo)

⚠️ **NOTA**: Estas llaves son solo para desarrollo. En producción, deben ser diferentes y más seguras.

- **RemitenteFrontend**: `pR7@xW2!dN#9mZ$eH8&`
- **RemitenteBackend**: `Q5#vF1@pG*0bT$yK9!r`
- **SecretUpFrontend**: `FRONTEND_SECRET_2024_SANMARINO_X9K2@mL7$pN`
- **SecretUpBackend**: `BACKEND_SECRET_2024_SANMARINO_V3M8#nT5&wQ`
- **EncryptionKey (SECRET_UP)**: `SECRET_UP_ENCRYPTION_KEY_2024_SANMARINO_K9P@xM3#vN`

---

## 📝 Logging y Auditoría

### Eventos Registrados

1. **Login Exitoso**
   - Email del usuario (sin contraseña)
   - Timestamp
   - IP de origen

2. **Login Fallido**
   - Email intentado
   - Razón del fallo
   - Timestamp
   - IP de origen

3. **SECRET_UP Inválido**
   - IP de origen
   - Razón (faltante, inválido, error desencriptación)
   - Timestamp

4. **Errores de Desencriptación**
   - Tipo de error
   - Preview del dato (primeros caracteres)
   - Timestamp

---

## 🛠️ Troubleshooting

### Error: "SECRET_UP no proporcionado"

**Causa**: El frontend no está enviando el header `X-Secret-Up`

**Solución**:
1. Verificar que `authInterceptor` esté registrado en `app.config.ts`
2. Verificar que `platformSecret.secretUpFrontend` esté configurado en `environment.ts`
3. Verificar que `encryptSecretUp()` se esté llamando correctamente

### Error: "Error al desencriptar SECRET_UP"

**Causa**: La llave de encriptación no coincide entre frontend y backend

**Solución**:
1. Verificar que `platformSecret.encryptionKey` en frontend coincida con `PlatformSecret:EncryptionKey` en backend
2. Verificar que no haya espacios en blanco o caracteres especiales mal copiados

### Error: "SECRET_UP inválido"

**Causa**: El SECRET_UP desencriptado no coincide con el esperado

**Solución**:
1. Verificar que `platformSecret.secretUpFrontend` en frontend coincida con `PlatformSecret:SecretUpFrontend` en backend
2. Verificar que la encriptación/desencriptación se esté haciendo correctamente

### Error: "Cannot resolve scoped service 'EncryptionService'"

**Causa**: `EncryptionService` está registrado como `Scoped` pero se usa en middleware

**Solución**: Cambiar a `Singleton`:
```csharp
builder.Services.AddSingleton<EncryptionService>();
```

---

## ✅ Checklist de Implementación

- [x] Encriptación AES-256-CBC del login
- [x] SECRET_UP encriptado en todas las peticiones
- [x] Middleware de validación SECRET_UP
- [x] Interceptor HTTP en frontend
- [x] Separación del menú del login
- [x] Configuración en `appsettings.json` y `environment.ts`
- [x] Registro correcto de servicios (Singleton para EncryptionService)
- [x] Manejo de errores y logging
- [x] Documentación completa

---

## 📚 Referencias

- **AES-256-CBC**: [Wikipedia - Advanced Encryption Standard](https://en.wikipedia.org/wiki/Advanced_Encryption_Standard)
- **PBKDF2**: [RFC 2898](https://tools.ietf.org/html/rfc2898)
- **Web Crypto API**: [MDN Web Docs](https://developer.mozilla.org/en-US/docs/Web/API/Web_Crypto_API)
- **ASP.NET Core Middleware**: [Microsoft Docs](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/)

---

**Última actualización**: 2024
**Versión**: 1.0
**Autor**: Sistema de Seguridad ZooSanMarino




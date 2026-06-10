# Plan: Usuarios de Plataforma (sin correo real)

**Fecha:** 2026-06-03  
**Objetivo:** Agregar opción de crear "usuarios de plataforma" con email sintético `@zootecnico.com` que no dispara envío de correos.

---

## Contexto

Actualmente al crear un usuario el backend siempre:
1. Almacena el email como credencial de login
2. Envía un correo de bienvenida vía `SendWelcomeEmailAsync`

Se necesita una segunda modalidad: **usuario de plataforma**, que usa un identificador interno del tipo `username@zootecnico.com`, puede iniciar sesión normalmente, pero el correo no existe en la realidad → no se debe disparar ningún envío.

---

## Cambios Backend

### 1. `RegisterDto.cs`
- Agregar propiedad `IsPlatformUser` (bool, default `false`)
- Sin cambiar validaciones de email (el formato `user@zootecnico.com` es válido)

### 2. `AuthService.cs` — `RegisterAsync`
- Cuando `dto.IsPlatformUser == true`:
  - Setear `login.IsEmailLogin = false`
  - Saltar el bloque `try { _emailService.SendWelcomeEmailAsync(...) }`
  - Retornar `response.EmailSent = false`, `response.EmailQueueId = null`

---

## Cambios Frontend

### 3. `user.service.ts`
- Agregar `isPlatformUser?: boolean` a `CreateUserDto`

### 4. `modal-create-edit.component.ts`
- Agregar `isPlatformUser = false` (flag de estado)
- Agregar control `platformUsername` al form (validators: requerido, solo letras/números/puntos/guiones, sin espacios)
- Método `onUserTypeToggle(platform: boolean)`:
  - Activa/desactiva validators de `email` vs `platformUsername`
  - Limpia valores previos
- `saveUser()`: cuando es plataforma, construir `email = platformUsername + '@zootecnico.com'` antes de enviar al backend
- `resetForm()`: resetear `isPlatformUser = false` y `platformUsername`
- `getTabErrorCount('access')`: incluir `platformUsername` cuando aplica

### 5. `modal-create-edit.component.html` — Tab "Acceso"
- Agregar toggle visual "Email Real" / "Usuario de Plataforma" al inicio del tab (solo en creación)
- Cuando "Email Real": mostrar campo email actual (comportamiento existente)
- Cuando "Usuario de Plataforma":
  - Mostrar input con sufijo visual `@zootecnico.com`
  - Ocultar campo email
  - Mostrar nota informativa: "Este usuario no recibirá correos"

### 6. `modal-create-edit.component.scss`
- Estilos para el toggle (`.user-type-toggle`)
- Estilos para el input con sufijo (`.platform-username-wrapper`)
- Estilos para la nota informativa (`.platform-info-note`)

---

## Flujo de datos

```
Frontend:
  platformUsername = "juanperez"
  email (generado) = "juanperez@zootecnico.com"
  isPlatformUser = true

→ POST /api/users → RegisterDto { Email: "juanperez@zootecnico.com", IsPlatformUser: true, ... }

Backend:
  login.IsEmailLogin = false
  SKIP SendWelcomeEmailAsync
  → usuario creado sin email enviado
```

---

## Restricciones

- `platformUsername`: solo letras, números, puntos, guiones y guiones bajos. Sin espacios. Sin `@`. Mínimo 3 caracteres.
- El email resultante `username@zootecnico.com` debe ser único (misma validación ya existente en backend)
- En modo edición: no se muestra el toggle (el tipo de usuario no cambia post-creación)

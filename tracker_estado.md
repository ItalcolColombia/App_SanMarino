# Tracker: Usuarios de Plataforma

**Plan:** [usuarios_plataforma_plan.md](fase_de_desarrollo/usuarios_plataforma_plan.md)  
**Fecha:** 2026-06-03

---

## Checklist de implementación

### Backend
- [x] `RegisterDto.cs` — Agregar campo `IsPlatformUser` (bool, default false)
- [x] `AuthService.cs` — Saltar `SendWelcomeEmailAsync` cuando `IsPlatformUser == true`; setear `IsEmailLogin = false`

### Frontend
- [x] `user.service.ts` — Agregar `isPlatformUser?: boolean` a `CreateUserDto`
- [x] `modal-create-edit.component.ts` — Agregar flag `isPlatformUser`, control `platformUsername`, método `onUserTypeToggle`, ajustar `saveUser()` y `resetForm()`
- [x] `modal-create-edit.component.html` — Agregar toggle visual + input con sufijo `@zootecnico.com` + nota informativa
- [x] `modal-create-edit.component.scss` — Estilos para toggle, input con sufijo y nota informativa

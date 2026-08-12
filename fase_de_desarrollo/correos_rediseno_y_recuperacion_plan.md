# Rediseño de los correos de la aplicación + recuperación de contraseña de punta a punta

> Plan vinculante. Tracker: bloque propio al final de [`tracker_estado.md`](../tracker_estado.md).
> Origen: 12-ago-2026. Disparador: se validó que el envío SMTP funciona (ver
> [`backend/documentacion/DIAGNOSTICO_CORREO_OFFICE365.md`](../backend/documentacion/DIAGNOSTICO_CORREO_OFFICE365.md))
> y al revisar los cuerpos aparecieron dos defectos de contenido, no de transporte.

---

## 1. Qué envía hoy la aplicación (inventario real, verificado en `email_queue`)

| `email_type` | Se genera en | Estado |
|---|---|---|
| `password_recovery` | `EmailService.GeneratePasswordRecoveryEmailBody` | 🔴 **contenido roto** (ver §2) |
| `welcome` | `EmailService.GenerateWelcomeEmailBody` | 🟠 estilo duplicado, contraseña en claro |
| `ticket_creado` | `TicketEmailTemplates.Creado` | 🟡 correcto, diseño mejorable |
| `ticket_transferido` | `TicketEmailTemplates.Asignado` | 🟡 correcto, diseño mejorable |
| `ticket_cerrado` | `TicketEmailTemplates.Cerrado` | 🟡 correcto, diseño mejorable |
| `ticket_solucionado` | `TicketService.BuildSolucionEmailBody` (inline) | 🟠 **fuera del sistema de plantillas**: sin logo, sin footer, sin botón |

Volumen histórico: `welcome` 51 · `password_recovery` 45 · `ticket_solucionado` 14 ·
`ticket_cerrado` 11 · `ticket_creado` 5.

## 2. El defecto de fondo: el correo de recuperación miente

`AuthService.RecoverPasswordAsync` genera un **token de 64 caracteres** (`GeneratePasswordResetToken`,
válido 15 min, un solo uso) y lo pasa al parámetro llamado `newPassword` de
`SendPasswordRecoveryEmailAsync`. La plantilla lo imprime bajo el rótulo
**«Tu nueva contraseña es:»** y ofrece un botón a `/login`.

Consecuencia: quien pide recuperar su contraseña recibe un token que **no sirve como contraseña**,
y no tiene dónde canjearlo — `POST /api/Auth/reset-password` existe en el backend, pero el frontend
solo tiene `/password-recovery` (el formulario para pedirlo). **La recuperación de contraseña no
funciona hoy, ni siquiera con el SMTP sano.**

Segundo problema del mismo método: `AdminResetPasswordAsync` lo reusa pasando una **contraseña real**.
Un mismo cuerpo sirve a dos semánticas opuestas (enlace de restablecimiento vs. credencial asignada).

**Decisión:** se separan los dos casos y el correo de recuperación pasa a llevar un **enlace**, nunca
el secreto en el cuerpo.

## 3. Enfoque arquitectónico

### 3.1 Un solo sistema de plantillas, puro y testeable

Hoy hay **tres** implementaciones del mismo layout (dos `<style>` gemelos en `EmailService` y el
`Wrap` de `TicketEmailTemplates`) más un HTML suelto en `TicketService`. Se unifican en:

```
ZooSanMarino.Application/Correos/
├── EmailLayout.cs        # static: documento, header, footer, preheader (HTML puro, sin DI)
├── EmailComponentes.cs   # static: boton, tarjeta, filaDato, callout, badge, tablaNotas, codigo
└── EmailTema.cs          # tokens de marca (colores, tipografía, anchos)
```

Va en **Application** (no en Infrastructure) porque es lógica pura `string → string`: así queda
cubierta por los tests de `ZooSanMarino.Application.Tests`, que es el gate de CI.

### 3.2 Reglas de maquetación para correo (no es una página web)

Outlook para Windows renderiza con el motor de Word: sin flexbox, sin grid, `<style>` en `<head>`
poco confiable. El estándar que se adopta:

- **Tablas anidadas** `role="presentation"` para toda la estructura; nada de `div` posicionados.
- **Estilos inline** en cada elemento (las clases quedan solo como refuerzo).
- Ancho fijo **600 px**, con `max-width:100%` para móvil.
- **Preheader oculto**: el texto que la bandeja muestra junto al asunto. Hoy no existe en ningún correo.
- Botón *bulletproof*: `<a>` con `padding` + `background-color` sólido (nada de `linear-gradient`,
  que Outlook ignora y deja el texto ilegible).
- Colores de marca de `CLAUDE.md`: `#e85c25` naranja = **acciones**; `#2d7a3e` verde = **éxito**;
  rojo solo peligro. Se abandona el `#f4b428` dorado suelto de las plantillas viejas.
- Todo dato variable pasa por `WebUtility.HtmlEncode`.
- Fallback de texto en el logo (`alt`) porque muchos clientes bloquean imágenes por defecto.

### 3.3 Contenido por correo (UX)

| Correo | Encabezado | Cuerpo | CTA |
|---|---|---|---|
| Restablecer contraseña | «Restablecé tu contraseña» | quién lo pidió, vigencia 15 min, qué hacer si no fue el usuario | **Crear contraseña nueva** → `/reset-password?token=…` + enlace en texto plano |
| Contraseña asignada por admin | «Un administrador restableció tu contraseña» | credencial en bloque monoespaciado, aviso de cambiarla | **Iniciar sesión** |
| Bienvenida | «Tu cuenta está lista» | credenciales + primeros pasos numerados | **Entrar a la plataforma** |
| Ticket creado | «Nuevo ticket `código`» | badges tipo/estado, tabla de datos, descripción | **Ver ticket** |
| Ticket asignado | «Te asignaron el ticket `código`» | quién lo transfirió, qué se espera | **Gestionar ticket** |
| Ticket solucionado | «Tu ticket fue solucionado» (verde) | la solución destacada, pedido explícito de confirmar | **Revisar y confirmar** |
| Ticket cerrado | «Ticket `código` cerrado» | solución + bitácora pública en tabla | **Ver historial** |

## 4. Archivos a crear / modificar

**Backend**
1. `Application/Correos/EmailTema.cs` — *nuevo*, tokens.
2. `Application/Correos/EmailLayout.cs` — *nuevo*, documento + preheader + header + footer.
3. `Application/Correos/EmailComponentes.cs` — *nuevo*, piezas reutilizables.
4. `Application/Interfaces/IEmailService.cs` — *modificar*: agregar `SendPasswordResetLinkEmailAsync`.
5. `Infrastructure/Services/EmailService.cs` — *modificar*: los 3 cuerpos sobre el layout nuevo.
6. `Infrastructure/Services/TicketEmailTemplates.cs` — *modificar*: `Wrap` delega en `EmailLayout`; se agrega `Solucionado`.
7. `Infrastructure/Services/TicketService.cs` — *modificar*: `BuildSolucionEmailBody` pasa a `TicketEmailTemplates.Solucionado`.
8. `Infrastructure/Services/AuthService.cs` — *modificar*: `RecoverPasswordAsync` usa el método del enlace.
9. `tests/ZooSanMarino.Application.Tests/EmailLayoutTests.cs` — *nuevo*.

**Frontend**
10. `features/auth/reset-password/` — *nuevo*: componente + html + scss (`changeDetection: Eager`).
11. `core/services/auth/password-recovery.service.ts` — *modificar*: `resetPassword(token, newPassword)`.
12. `app.config.ts` — *modificar*: ruta `reset-password`.
13. `app.component.ts` — *modificar*: ocultar el menú también en `/reset-password`.
14. `features/auth/password-recovery/password-recovery.component.html` — *modificar*: los textos dicen «te enviaremos una nueva contraseña», y ya no es cierto.

**Sin cambios de BD.** `password_reset_tokens` ya existe con todo lo necesario.

## 5. Reglas de negocio que NO se tocan

- La respuesta de `/recover-password` sigue siendo **neutra** (anti-enumeración): mismo mensaje exista
  o no el correo. El rediseño no puede filtrar si la cuenta existe.
- Token: 64 caracteres CSPRNG, **15 minutos**, un solo uso, invalida los anteriores del mismo usuario.
- La validación de la contraseña nueva es la del DTO: mínimo 8, al menos una letra y un número.
- Los correos se **encolan**; ningún cambio toca el transporte ni el `EmailQueueProcessorService`.
- `email_type` no se renombra (rompería consultas e históricos).

## 6. Casos de prueba

**Unitarios (xUnit, gate de CI)**
1. `EmailLayout.Documento` incluye el preheader y no lo muestra en el cuerpo visible.
2. Un valor con `<script>` sale escapado en todo componente que reciba texto.
3. El botón contiene exactamente la URL recibida y un `href` absoluto.
4. El correo de restablecimiento **no contiene** la palabra «contraseña es» ni imprime el token como credencial.
5. El enlace de restablecimiento arma `…/reset-password?token=<token>` con el token URL-encodeado.
6. `Solucionado` y `Cerrado` renderizan sin notas (caso vacío) sin romper la tabla.

**Manual / smoke**
7. Renderizar los 7 cuerpos a `.html` y revisarlos en el navegador (claro y oscuro, 375 px y 600 px).
8. Envío real de uno de cada familia al correo del desarrollador.
9. Flujo completo: pedir recuperación → abrir el enlace del correo → fijar contraseña nueva → entrar con ella.
10. Token vencido y token reusado → mensaje de error correcto, sin cambiar la contraseña.

## 7. Fuera de alcance (queda anotado)

- La política del tenant de Microsoft 365 que rechaza el envío **desde producción**: no se arregla por
  código. Va por solicitud a los administradores (§ documentos en `backend/documentacion/`).
- `RECUPERACION_CONTRASENA.md` describe un flujo que ya no existe (contraseña temporal, `EmailSettings`).
  Se reescribe al cerrar esta tarea.

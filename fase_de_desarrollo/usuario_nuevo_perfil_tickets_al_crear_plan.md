# Plan — Perfil de atención de tickets alcanzable al CREAR un usuario (no solo al editar)

## Problema reportado

Usuario (16-sep-2026): al crear una empresa y usuario, no puede darle permisos para que los
usuarios nuevos "lo vean como desarrollador global" — en Santa Reyes, con usuarios nuevos, no
pueden abrir un ticket que le llegue a él (tipo "Desarrollo").

## Causa raíz (confirmada leyendo código, no es específico de Santa Reyes)

- `NivelTicket.TiposPermitidos` (`backend/src/ZooSanMarino.Domain/Entities/TicketNivelConstants.cs:13-18`):
  nivel `NORMAL` solo puede crear Soporte/Dudas; `IMPLEMENTADOR` además Desarrollo/Requerimiento.
- Un usuario sin fila en `ticket_perfiles_usuario` nace con nivel `NORMAL` por default
  (`TicketPerfilService.GetTiposPermitidosAsync`, `backend/src/ZooSanMarino.Infrastructure/Services/TicketPerfilService.cs:44-57`).
  Por eso un usuario recién creado nunca ve "Desarrollo" como tipo de ticket hasta que alguien le
  suba el nivel a `IMPLEMENTADOR`.
- La única pantalla para setear ese nivel es la pestaña "Tickets" embebida en el modal de
  Usuarios (`app-ticket-perfil-editor`), pero está gateada a **solo edición**:
  `frontend/src/app/features/config/user-management/components/modal-create-edit/modal-create-edit.component.html:504`
  → `@if (activeTab === 'tickets' && isEditing && editingUser && !soloLectura)`.
- El flujo de CREAR usuario (`modal-create-edit.component.ts:447-491`) nunca deja `editingUser`
  seteado ni llama a `saveTicketPerfilIfLoaded` (sí lo hace el de editar, líneas 415-446) — cierra
  el modal apenas el usuario queda creado.
- Resultado: hoy, para dejar a un usuario nuevo con nivel Implementador, hay que crearlo, cerrar el
  modal, volver a la lista, reabrirlo en modo edición, ir a la pestaña Tickets recién ahí visible,
  y guardar. Ese segundo paso, no obvio, es lo que el usuario reporta como "no puedo darle
  permisos".
- Esto NO es un bug de permisos/autorización ni de Santa Reyes puntual: el resolutor global
  (rol `Admin`/`Administrador`, que es lo que hace que él aparezca como asignable de "Desarrollo")
  ya está sembrado correctamente en Santa Reyes desde el incidente del 4-sep-2026
  (`CompanyService.PerfilAtencionTickets.cs`, ver memoria `tipo-de-ticket-sin-resolutor-no-existe`).
  El hueco es el nivel del usuario SOLICITANTE, que no tiene forma de configurarse en el mismo
  paso de alta.

## Decisión del usuario (confirmada)

Al crear un usuario, el modal **se queda abierto** (no se cierra solo), pasa a modo edición con el
usuario recién creado y **salta automáticamente a la pestaña "Tickets"**, para configurar el Nivel
(y opcionalmente los resolutores) ahí mismo, en la misma sesión de alta.

## Enfoque — solo frontend, sin cambios de BD/SQL/backend

Reutilizar el mecanismo YA existente para edición (`isEditing && editingUser` habilita la pestaña;
`saveTicketPerfilIfLoaded(userId)` persiste el perfil junto con el guardado del usuario) en vez de
inventar uno nuevo. El único hueco es que el flujo de creación no deja al componente en ese estado.

`editingUser` es un `@Input()` cuyo dueño es el padre (`user-management.component.ts`) — mutarlo
solo del lado del hijo se perdería en el próximo change detection (el padre lo sigue bindeando a su
propio `editingUser`, que seguiría en `null`). Por eso el cambio tiene que viajar por un nuevo
`@Output()`, no por una asignación local.

### Archivos a modificar

1. **`frontend/src/app/features/config/user-management/components/modal-create-edit/modal-create-edit.component.ts`**
   - Nuevo `@Output() userCreated = new EventEmitter<UserListItem>();` (aparte de `userSaved`, que
     sigue siendo solo para el guardado en modo edición — no tocar su contrato).
   - Importar e inyectar `ToastService` (`../../../../../shared/services/toast.service`, mismo
     patrón que ya usa `ticket-perfil-editor.component.ts`).
   - En el `next` del `subscribe` de `create()` (hoy líneas ~471-486): en vez de
     `this.userSaved.emit(result); ...closeModal()`, emitir `this.userCreated.emit(result)`,
     `this.activeTab = 'tickets'`, toast informativo, y **no** cerrar el modal. La lógica de
     `emailQueueId` existente se conserva tal cual (no son mutuamente excluyentes).
   - `resetForm()` (línea 333): agregar `this.activeTab = 'personal';` para que la próxima vez que
     se abra "Crear Usuario" no arranque en la pestaña Tickets por un salto anterior.
2. **`frontend/src/app/features/config/user-management/user-management.component.ts`**
   - Nuevo método `onUserCreated(user: UserListItem): void { this.editingUser = user; this.modalSoloLectura = false; }`
     — a propósito NO toca `modalOpen` (se mantiene `true`, el modal sigue abierto). Este es el
     único punto donde el `@Input() editingUser` real (el del padre) cambia, lo que dispara
     `ngOnChanges` en el hijo (`modal-create-edit.component.ts:135-145`) y llama a `loadUserData()`
     — mismo camino que ya corre hoy al abrir "Editar" sobre un usuario existente.
3. **`frontend/src/app/features/config/user-management/user-management.component.html`**
   - `<app-modal-create-edit ...>` (línea 75-81): agregar `(userCreated)="onUserCreated($event)"`
     junto al `(userSaved)` existente.

### Por qué no hace falta tocar nada más

- `ngOnChanges` del modal ya sabe repoblar el formulario (`loadUserData`) cuando `editingUser` pasa
  a tener valor — no hay que duplicar esa lógica.
- El botón "Guardar" del footer y su texto (`submitButtonText`) ya se derivan del mismo estado
  `editingUser`/`isEditing`, así que al quedar en modo edición automáticamente pasa a comportarse
  como "Guardar cambios" (segundo click = `update()` + `saveTicketPerfilIfLoaded`, mismo camino
  probado que usa cualquier edición manual).
- `app-ticket-perfil-editor` en modo `usuario` ya maneja perfil inexistente (`hasProfile:false` →
  nivel sin preseleccionar) sin cambios.

## Riesgos / cosas a verificar en el smoke

- Confirmar que el segundo `PUT /api/Users/{id}` (al guardar desde el modo edición post-creación)
  no reviente por reenviar los mismos datos que ya se acaban de crear (debería ser un PUT idempotente
  normal, igual que cualquier edición).
- Confirmar que `activeTab` vuelve a `'personal'` en la siguiente apertura de "Crear Usuario" (evitar
  regresión del propio cambio).
- Probar en una empresa cualquiera (no hace falta que sea Santa Reyes — el bug es genérico), crear un
  usuario nuevo, verificar que aparece la pestaña Tickets sin cerrar el modal, marcar Nivel =
  Implementador, guardar, y confirmar con ese usuario (o vía `GET /api/ticket-perfiles/tipos-permitidos`)
  que ahora "Desarrollo" aparece como tipo creable.

## Sin cambios de BD/SQL

Ningún endpoint nuevo, ninguna migración. Se reutilizan `PUT /api/Users/{id}` y
`PUT /api/ticket-perfiles/usuario/{userId}`, ya existentes.

## Casos de prueba (smoke manual en navegador — no hay spec previo de este módulo)

1. Crear usuario nuevo (cualquier empresa) → modal NO se cierra solo → queda en modo edición → tab
   "Tickets" visible y seleccionada automáticamente.
2. Setear Nivel = Implementador, click "Guardar cambios" → toast de éxito → modal cierra.
3. Reabrir ese usuario en edición → pestaña Tickets → Nivel Implementador persistido.
4. Abrir "Crear Usuario" de nuevo (usuario distinto) → arranca en pestaña "Personal", no "Tickets".
5. Con el usuario del paso 2 logueado (o vía API), `GET /api/ticket-perfiles/tipos-permitidos` incluye
   "Desarrollo" con al menos un asignable.

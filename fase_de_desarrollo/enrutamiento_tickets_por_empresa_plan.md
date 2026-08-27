# Plan — Enrutamiento de tickets por empresa (Sanmarino, Panamá, Ecuador)

## Pedido
Configurar quién recibe qué tipo de ticket en cada empresa:

| Empresa | SOPORTE/DUDAS → | REQUERIMIENTO → | Quién escala a Desarrollo/Global (moiesbbuga@gmail.com) |
|---|---|---|---|
| Sanmarino | rol "Sistemas sanmarino" | Verenice Morales | Verenice |
| Panamá | rol "sistemas panama" | Ricardo De la Rosa | Ricardo |
| Ecuador | Lady Malave (sin área de sistemas separada) | Lady Malave | Lady Malave |

## Mecanismo real (investigado antes de tocar nada)
El módulo de tickets ya tiene un motor de enrutamiento genérico y activo:
- `ticket_perfil_usuario` (user_id, company_id, nivel NORMAL|IMPLEMENTADOR): qué tipos puede CREAR
  un usuario. IMPLEMENTADOR habilita REQUERIMIENTO/DESARROLLO además de SOPORTE/DUDAS.
- `ticket_resolutor` (user_id, tipo, pais_id, company_id): a qué usuario ESPECÍFICO se puede asignar
  un ticket de ese tipo. `pais_id = NULL` = cualquier país.
- `ticket_resolutor_rol` (role_id, tipo, pais_id, company_id): igual pero por ROL — todos los
  usuarios que tengan ese rol quedan como asignables (`role_permissions`/`user_roles`, no filtra por
  empresa a propósito: un rol admin compartido puede atender varias filiales — ver doc-comment de
  `TicketPerfilService.GetAsignablesInternalAsync`).
- `TicketService.Gestion.PuedeGestionar()` = `tickets.admin` (nombrado `EsSuperAdmin()` en el código,
  engañoso) `|| tickets.gestionar`. **Es el gate real** para tomar/cambiar estado — sin uno de los
  dos, ni siquiera podés operar un ticket que ya está asignado a vos.
- El nivel IMPLEMENTADOR también lo otorga automáticamente tener `tickets.gestionar`/`tickets.admin`
  (bypassa `ticket_perfil_usuario`).

## Auditoría del estado actual (BD local, refleja producción — verificado antes de escribir código)
- Los roles **"Sistemas sanmarino" (id 34, company 1)** y **"sistemas panama" (id 35, company 5)**
  YA EXISTEN — no hay que crearlos (verificado dos veces: la primera consulta tuvo un error propio
  que los ocultó).
  - Rol 34: tiene `tickets.gestionar` y los 3 menús de tickets. Sin ninguna fila en
    `ticket_resolutor_rol` → hoy no recibe nada. Lo tiene **Alexander Mejia**.
  - Rol 35: **sin** `tickets.gestionar`, sin el menú "Bandeja de gestión" (57), sin filas de
    `ticket_resolutor_rol`, y `company_permissions` de Panamá tiene `tickets.gestionar` **apagado**.
    Nadie lo tiene asignado hoy.
- **Verenice** (rol 32 "Implementador Sanmarino Colombia", company 1): hoy es resolutor DIRECTO de
  los 4 tipos (SOPORTE/DUDAS/REQUERIMIENTO/DESARROLLO) — más de lo que corresponde. Su rol solo tiene
  `tickets.crear`: **no podría gestionar ni sus propios requerimientos** (`PuedeGestionar()` da falso).
- **Ricardo** (rol 22 "Admin Panama", company 5): sin `ticket_perfil_usuario` ni `ticket_resolutor`.
  Su rol ya tiene `tickets.admin` → nivel IMPLEMENTADOR automático y `PuedeGestionar()` ya da
  verdadero (no hace falta tocarle permisos).
- **Lady Malave** (rol 10 "Ecuador Administrador", company 3): su `ticket_perfil_usuario` está
  guardado en **company_id=1** (Sanmarino) por error — debería ser 3. Su rol solo tiene
  `tickets.crear`: mismo problema que Verenice, no podría gestionar.
- 🔴 **Bug de código**: `GetAsignablesInternalAsync` no filtra `ticket_resolutor` por `company_id`
  (sólo por tipo+país) — con `pais_id = NULL`, Verenice hoy aparece como asignable en tickets de
  CUALQUIER empresa, no solo Sanmarino. Hay que agregar el filtro.

## Alcance

### A) Código
- `TicketPerfilService.GetAsignablesInternalAsync`: agregar `r.CompanyId == companyId` al filtro de
  `TicketResolutores` (sección 1). La sección 2 (`TicketResolutorRoles`) queda igual — su alcance
  cruzado es intencional y ya está documentado.

### B) Migración — Sanmarino
- `ticket_resolutor_rol`: rol 34 → SOPORTE + DUDAS, company 1, país NULL.
- `ticket_resolutor` de Verenice: desactivar SOPORTE/DUDAS/DESARROLLO, dejar solo REQUERIMIENTO activo.
- `role_permissions`: agregar `tickets.gestionar` al rol 32 (alcanza también a Alex Londoño y
  "costos sanmarino occidente", los otros dos que tienen ese rol — aceptable, es un rol de
  implementador ya de confianza, mismo criterio que "Admin Panama" ya tiene `tickets.admin`).

### C) Migración — Panamá
- `company_permissions`: encender `tickets.gestionar` (company 5).
- `role_permissions`: agregar `tickets.gestionar` al rol 35.
- `role_menus`: agregar menú 57 ("Bandeja de gestión") al rol 35.
- `ticket_resolutor_rol`: rol 35 → SOPORTE + DUDAS, company 5, país NULL.
- `ticket_resolutor` de Ricardo: REQUERIMIENTO, company 5, país NULL. (Sin `ticket_perfil_usuario`:
  ya es IMPLEMENTADOR vía `tickets.admin`.)

### D) Migración — Ecuador
- `company_permissions`: crear/encender `tickets.gestionar` (company 3, hoy sin fila).
- `role_permissions`: agregar `tickets.gestionar` al rol 10 (alcanza también a la cuenta genérica
  "Admin Ecuador" — razonable, es la cuenta admin del país).
- `ticket_perfil_usuario` de Lady Malave: desactivar la fila de company_id=1, crear la de
  company_id=3 (IMPLEMENTADOR).
- `ticket_resolutor` de Lady Malave: SOPORTE + DUDAS + REQUERIMIENTO, company 3, país NULL (sin
  DESARROLLO — eso lo cubre ya el rol Admin global, ver abajo).

### Ya cubierto, sin tocar
- DESARROLLO global → rol 1 "Admin" ya tiene `ticket_resolutor_rol` para company 1/3/4/5
  (moiesbbuga@gmail.com). Falta Santa Reyes (company 6) pero no se pidió — no se toca.

## Validación
- `dotnet build` + `dotnet test`.
- Smoke local: crear ticket SOPORTE/REQUERIMIENTO en cada empresa (impersonando cada usuario) y
  verificar `GET /api/ticket-perfiles/asignables` devuelve exactamente a quien corresponde — y a
  nadie de otra empresa (prueba del fix del filtro).

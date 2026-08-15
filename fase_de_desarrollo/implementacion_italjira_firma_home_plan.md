# Implementación — historia en ItalJira, firma manuscrita de aceptación y pendientes en Home

**Fecha:** 2026-08-15 · **Estado:** propuesta (F0 aplicado)
**Tracker:** bloque `I1..I5` al final de [tracker_estado.md](../tracker_estado.md)
**Hermano:** [vacunacion_cronograma_vivo_plantillas_plan.md](vacunacion_cronograma_vivo_plantillas_plan.md) — comparten el panel de Home.

---

## 0. Lo que se pidió, en una línea

> *"En esta fase solo creo el plan de cumplimiento; el proceso de implementación se ejecuta en
> ItalJira. Cuando yo confirmo que se terminó el plan asignado, a cada usuario le aparece en Home
> «pendiente de aceptación» con lo que se le capacitó y un espacio para firmar con el dedo o el
> mouse."*

---

## 1. Estado actual (verificado en código)

`implementacion_planes` → `implementacion_tareas` → `implementacion_tarea_firmas` ya existen, con:

- **Varios participantes por punto** (`ImplementacionTareaFirma`, una fila por `(tarea, usuario)`,
  soft-delete, quitar solo mientras esté `pendiente`). ✅ **Ya cubre "puedo asignar varios usuarios".**
- Respuesta `pendiente` / `firmada` / `rechazada`, con nota y fecha. ✅
- Doble check: el gestor marca `completada` → el asignado `confirma`. ✅
- Endpoints `PUT tareas/{id}/participantes`, `POST firmar` / `rechazar`, `GET mis-firmas`, `mis-tareas`. ✅

**Lo que falta para el objetivo:**

| Pedido | Hoy | Falta |
|---|---|---|
| Firma **manuscrita** (dedo/mouse) | firma **digitada** (`FirmaTexto`, texto libre) | canvas → imagen + evidencia |
| Aparece **cuando el implementador confirma que terminó** | el participante puede firmar apenas lo agregan | **gate por estado de la tarea** |
| Espacio **en Home**, desplegable | solo dentro de `/implementacion/mis-tareas` | panel en Home |
| El plan **vive en el tablero de ItalJira** | sin vínculo | `historia_id` / `ticket_tarea_id` |
| Ver **qué** se capacitó antes de firmar | parcial (modal) | detalle completo + adjunto |

---

## 2. Diseño

### 2.1 Vínculo con ItalJira (I1)

```
implementacion_planes  + historia_id      BIGINT NULL  → historias.id
implementacion_tareas  + ticket_tarea_id  BIGINT NULL  → ticket_tareas.id
```

- Al crear el plan: *"Crear historia en ItalJira"* (default sí) → nace una `Historia` con el nombre
  y las fechas del plan, y **cada tarea del plan crea su `TicketTarea`** colgada de esa historia
  (`HistoriaId`, `AsignadoUserGuid` = el asignado del punto). También se puede **enlazar** una
  historia existente en vez de crear.
- **Sincronización en un solo sentido, explícita:** cuando la `TicketTarea` pasa a `LISTO`, la tarea
  del plan se marca `completada` (que es justo el gate de firma). Al revés no: marcar completada en
  el plan mueve la tarea de ItalJira a `LISTO` solo si el usuario lo pide desde el botón. Evita
  bucles de estado entre dos tableros.
- Alcance: ItalJira **no filtra por empresa** (es cross-empresa por diseño); la puerta sigue siendo
  el permiso `tickets.gestionar`. Un plan de la empresa X **no** expone su historia a usuarios sin
  ese permiso — el panel de Home nunca muestra datos de ItalJira, solo del plan.

### 2.2 Gate de firma: "aparece cuando yo confirmo" (I2)

Hoy un participante puede firmar en cuanto lo agregan. Se agrega el gate que pidió el usuario:

```
tarea.estado = 'pendiente'   → el participante la VE como "programada", NO firmable
tarea.estado = 'completada'  → se habilita la firma  ← el implementador confirmó que terminó
tarea.estado = 'confirmada'  → sigue firmable para los que faltan
```

El backend rechaza `POST firmar` sobre una tarea `pendiente` (409 con mensaje claro), no solo la UI.

### 2.3 Firma manuscrita como evidencia (I3)

`implementacion_tarea_firmas` gana:

```
+ firma_imagen        TEXT NULL   -- PNG base64 del canvas (dedo en celular / mouse en desktop)
+ firma_tipo          VARCHAR(12) -- 'manuscrita' | 'digitada'
+ contenido_hash      CHAR(64)    -- SHA-256 de lo que se firmó
+ firmado_user_agent  TEXT NULL
+ firmado_ip          VARCHAR(45) NULL
```

- `FirmaTexto` **se conserva** (fallback accesible y compatibilidad: las firmas existentes siguen
  válidas y se leen igual).
- **`contenido_hash` es lo que la vuelve evidencia y no una imagen suelta**: se calcula sobre
  `plan.nombre + tarea.titulo + tarea.descripcion + fecha`. Si alguien edita el punto después de
  firmado, el hash deja de coincidir y el detalle lo muestra como *"el contenido cambió después de
  la firma"*. Sin esto, una firma manuscrita prueba menos que la digitada actual.
- Canvas: `pointerdown/move/up` (cubre mouse, dedo y lápiz con un solo handler), botón *Limpiar*,
  trazo normalizado a 600×200 px, PNG comprimido (~10–25 KB). Se rechaza un canvas vacío.

### 2.4 Home: un solo panel de pendientes (I4) — compartido con Vacunación

**Un endpoint, no dos.** `GET /api/MisPendientes` devuelve las secciones que le tocan al usuario:

```jsonc
{ "firmasImplementacion": [ { tareaId, plan, titulo, tipo, fechaCompletada, … } ],
  "vacunacionPendiente":  [ { cronogramaItemId, lote, galpon, vacuna, fechaObjetivo, diasAtraso } ] }
```

En Home, un **acordeón** (cerrado si no hay nada, con badge de conteo si hay):

```
▸ Pendientes de firma — Capacitación (2)
    Capacitación módulo Inventario · Plan "Implementación Santa Reyes"
    Realizada el 12/08/2026 por Jose Moisés          [ Ver y firmar ]
▸ Vacunas por aplicar (3)
```

Al pulsar *Ver y firmar*: detalle de **qué** se capacitó (título, descripción, fecha, encargado) y
debajo el recuadro de firma. Opción *"No firmo — registrar novedad"* que ya existe hoy (deriva a
ticket).

### 2.5 Plan de capacitación con varios usuarios (ya soportado)

`PUT tareas/{id}/participantes` acepta N usuarios por punto — un punto de capacitación con 8
asistentes genera 8 filas pendientes, y cada uno firma la suya en su Home. **No requiere cambio de
modelo**; sí una mejora de UI: seleccionar participantes **por rol** o *"todos los de la empresa"*
en vez de uno por uno.

---

## 3. Fases

| Fase | Entrega | Riesgo |
|---|---|---|
| **F0** ✅ | Fix de change detection (8 componentes de Implementación) | nulo |
| **I1** | `historia_id` / `ticket_tarea_id` + crear/enlazar historia + tareas en ItalJira | bajo (aditivo) |
| **I2** | Gate de firma por estado de la tarea | **medio** — cambia cuándo puede firmar la gente |
| **I3** | Firma manuscrita + hash de evidencia (conservando la digitada) | bajo |
| **I4** | `GET /api/MisPendientes` + acordeón en Home (con Vacunación W3) | bajo |
| **I5** | Selección de participantes por rol / empresa | bajo |

---

## 4. Reglas de negocio (contrato para los tests)

1. Firmar una tarea en estado `pendiente` ⇒ **409**, nunca se guarda.
2. Una fila de firma por `(tarea, usuario)` viva; quitar participante solo si sigue `pendiente`.
3. Una firma `rechazada` puede retractarse firmando (ya vigente, se conserva).
4. Firma manuscrita ⇒ `firma_imagen` no vacía **y** `contenido_hash` calculado en el servidor
   (nunca enviado por el cliente).
5. Editar el título/descripción de una tarea ya firmada **no borra** la firma: se marca
   `contenido cambió` por hash. La auditoría no se pierde nunca.
6. Borrar un plan con firmas ⇒ soft-delete, jamás borrado físico.
7. `GET /api/MisPendientes` es estrictamente del usuario autenticado (`ICurrentUser.UserGuid`) y de
   la empresa activa; sin `UserGuid` ⇒ 401, nunca lista completa.
8. Sin historia enlazada, el plan funciona igual que hoy (el vínculo es opcional).

## 5. Casos de prueba

- `ImplementacionFirmaCalculos`: puede firmar sí/no por estado; hash estable ante reordenamiento de
  campos; canvas vacío rechazado.
- Integración: firmar `pendiente` ⇒ 409; firmar `completada` ⇒ 200 y desaparece de Home; editar la
  tarea tras firmar ⇒ firma viva + marca de contenido cambiado.
- ItalJira: crear plan con historia ⇒ 1 historia + N tareas; borrar el plan **no** borra la historia
  (queda la evidencia del trabajo).
- Regresión: plan sin historia y firma digitada existentes se comportan igual que hoy.

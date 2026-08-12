# PWA F3.1 — Captura offline (outbox) con idempotencia real

**Fecha:** 2026-08-12
**Plan madre:** [`pwa_offline_first_plan.md`](pwa_offline_first_plan.md) §5.4 (escritura), §5.5 (conflictos), §F3
**Antecedentes:** F1 shell ([`pwa_f1_shell_plan.md`](pwa_f1_shell_plan.md)) · F2 consulta
([`pwa_f2_consulta_offline_plan.md`](pwa_f2_consulta_offline_plan.md)) · alistamiento
([`pwa_alistamiento_campo_plan.md`](pwa_alistamiento_campo_plan.md))

---

## 0. Estado medido antes de empezar (12-ago-2026)

| Hecho | Cómo se midió |
|---|---|
| `Idempotency-Key` / `client_op_id` / outbox: **no existen** en el código fuente | `grep -rli` sobre `backend/src` y `frontend/src` ⇒ solo DLLs de EF |
| `SeguimientoLoteLevanteService.Crud.cs:76,190,275` abre **su propia** transacción | grep `BeginTransactionAsync` |
| `SeguimientoDiarioService.cs:300` y `:967` aceptan `dto.CreatedByUserId` **del cliente** (B5) | lectura directa |
| `ActiveCompanyMiddleware.cs:81` y `:134` degradan al `company_id` del token (B6) | lectura directa |
| La cuenta `moiesbbuga@gmail.com` es super admin hardcodeado (`AuthService.cs:427`) | grep |
| `decidirCacheOffline` devuelve **false** para super admin ⇒ esa cuenta **no cachea nada** | `decidir-cache-offline.funcion.ts:32` |
| `offline-db.ts` está en **v1** con un solo store (`consultas`) | lectura directa |
| Entorno local vivo: back `:5002`, front `:4200`, Postgres `:5433` (sin Docker) | `netstat` + `curl` a swagger |

---

## 1. Alcance

**Dentro:** una sola escritura offline —**crear seguimiento diario de levante**—, encolada en
IndexedDB, empujada por lotes a un endpoint de sync idempotente, con bandeja de pendientes y de
conflictos. Es exactamente el alcance que el plan madre le asigna a F3 (*"Outbox con **una sola**
escritura (seguimiento levante), push por lotes, bandeja de conflictos"*).

**Fuera, explícito:**
- Editar/borrar offline (solo alta). Un `PUT` sincronizado tarde pisa columnas del sistema (§5.5).
- Entidades creadas offline que otras referencian (`client_entity_id`, grafo de ops). No hace falta
  para una sola escritura hoja.
- Los ítems B1 (revocación), B8 (rotar llaves) y B10 (super admin a datos). Se documentan como
  requisitos abiertos, no se resuelven acá.
- **Modelo asíncrono `202 + batch_id`.** Con lotes de 25 y una sola escritura hoja no se llega al
  techo de 20 s; se deja anotado el umbral que obligaría a adoptarlo.

## 2. Enfoque arquitectónico

### 2.1 La decisión que ordena todo: idempotencia en la MISMA transacción

Un outbox sin idempotencia **duplica datos**: el dispositivo envía, el servidor aplica, la respuesta
se pierde en la red, el dispositivo reintenta y aplica dos veces. Con lotes de campo eso es
mortalidad contada doble.

Por eso `sync_operaciones` con `UNIQUE (client_op_id)` es la **primera** migración del proyecto, y el
registro de idempotencia y el efecto se escriben en **una sola transacción**. Si no comparten
transacción hay una ventana en que el efecto quedó aplicado y la marca no: el reintento vuelve a
aplicar.

**El obstáculo, medido:** `SeguimientoLoteLevanteService` abre su propia transacción. EF Core lanza
`InvalidOperationException` si se llama `BeginTransactionAsync` con una ya activa en el mismo
contexto. Corrección quirúrgica y **sin cambio de comportamiento**: la transacción del service pasa a
ser **condicional** a que no haya una ambiente.

```csharp
// null cuando ya hay una transacción ambiente ⇒ el llamador la maneja.
await using var tx = _ctx.Database.CurrentTransaction is null
    ? await _ctx.Database.BeginTransactionAsync()
    : null;
...
if (tx is not null) await tx.CommitAsync();
```

Llamado desde el controller (sin ambiente) se comporta **idéntico** a hoy. Llamado desde el push
participa de la transacción del push.

### 2.2 La empresa sale de la operación, no del header

El push **ignora `X-Active-Company`** y usa el `companyId` embebido en la operación, **validado contra
el JWT**. Una operación capturada el sábado en la empresa A no puede aplicarse en la B porque el lunes
el usuario abrió otra empresa. Ante desajuste: rechazo tipado, **nunca** reasignación silenciosa.

Esto es B6 **en el camino de sync** (donde el daño es peor: escritura reproducida). El fallback del
middleware para el tráfico normal queda como está — sacarlo es un cambio de alcance mayor.

### 2.3 El servidor estampa el autor (B5 en el camino de sync)

El push **ignora** cualquier autor que venga en el cuerpo y estampa el del token. La trazabilidad
offline va en campos **separados y no autoritativos** de `sync_operaciones`
(`device_id`, `capturado_at_dispositivo`), nunca pisando `created_by_user_id`.

### 2.4 Las dos clases de resultado (§5.5)

| Clase | Ejemplos | Tratamiento |
|---|---|---|
| **(a) Error de captura** | fecha futura, lote inexistente, duplicado por fecha, empresa no autorizada, contrato obsoleto | Queda en la bandeja, **editable/descartable**. Estado `rechazada` |
| **(b) Divergencia con el mundo** | (no aplica todavía en levante-alta) | Reservado: `requiere_cuadre`. Se deja el código de estado creado y sin emisor |

⚠️ v1 **no** emite la clase (b): el alta de levante no valida saldos. Se deja el estado modelado para
que F3.2 (inventario/ventas) no tenga que migrar la tabla. **Documentado como no ejercitado.**

### 2.5 Por qué el outbox NO se apaga con el gate D6

`decidirCacheOffline` bloquea la **caché de lectura** para super admin y multiempresa: protege contra
un dispositivo perdido con el snapshot de la operación. El outbox es **otra cosa**: son capturas
propias que el servidor **nunca vio**. Bloquearlo no protege un snapshot — **destruye trabajo de
campo**. Son las mismas dos amenazas distintas del alistamiento, y la respuesta es distinta para cada
una.

Regla derivada, **la más importante del módulo**: el outbox **no se purga nunca** por logout, cambio
de empresa ni kill switch. Solo se borra una operación **confirmada por el servidor** o **descartada
explícitamente por el usuario**.

---

## 3. Archivos

### Backend

| Archivo | Qué |
|---|---|
| `Domain/Entities/SyncOperacion.cs` | entidad nueva |
| `Infrastructure/Persistence/Configurations/SyncOperacionConfiguration.cs` | `UNIQUE (client_op_id)` |
| `Infrastructure/Migrations/*_AddSyncOperaciones.cs` | idempotente (`CREATE TABLE IF NOT EXISTS`) |
| `Application/Calculos/SyncPushCalculos.cs` | **puro**: validación, clasificación, decisión replay |
| `Application/DTOs/Sync/SyncPushDtos.cs` | request/response del lote |
| `Application/Interfaces/ISyncPushService.cs` | contrato |
| `Infrastructure/Services/Sync/SyncPushService.cs` | ancla (campos, ctor, interfaz) |
| `Infrastructure/Services/Sync/Funciones/SyncPushService.Levante.cs` | despacho del tipo `seguimiento_levante_crear` |
| `API/Controllers/SyncController.cs` | `POST /api/Sync/push` |
| `SeguimientoLoteLevanteService.Crud.cs` | transacción **condicional** (3 sitios) |
| `tests/.../SyncPushCalculosTests.cs` | xUnit |

### Frontend

| Archivo | Qué |
|---|---|
| `shared/offline/offline-db.ts` | **v2**: store `outbox` (paso de migración nuevo, acumulativo) |
| `shared/offline/models/outbox.model.ts` | `OperacionPendiente`, `EstadoOperacion` |
| `shared/offline/funciones/decidir-encolable.funcion.ts` | **pura**: lista blanca de mutaciones |
| `shared/offline/funciones/backoff.funcion.ts` | **pura**: exponencial + jitter + `Retry-After` |
| `shared/offline/funciones/clasificar-resultado-push.funcion.ts` | **pura**: (a) vs (b) vs reintentable |
| `shared/offline/outbox.service.ts` | encolar/listar/marcar/descartar |
| `shared/offline/sync.service.ts` | empuje por lotes al reconectar |
| `shared/offline/offline-cache.interceptor.ts` | rama de mutación: `status === 0` ⇒ encolar |
| `features/diagnostico/**` | bandeja de pendientes |
| `core/auth/session-timeout.service.ts` | usar el seam `TRABAJO_PENDIENTE_OFFLINE` ya existente |

---

## 4. Base de datos

```sql
CREATE TABLE IF NOT EXISTS sync_operaciones (
  id                        bigserial PRIMARY KEY,
  client_op_id              uuid        NOT NULL,
  tipo                      varchar(60) NOT NULL,
  user_id                   integer     NOT NULL,
  company_id                integer     NOT NULL,
  device_id                 varchar(80) NULL,
  capturado_at_dispositivo  timestamptz NULL,
  estado                    varchar(20) NOT NULL,   -- aplicada | rechazada | requiere_cuadre
  error_codigo              varchar(40) NULL,
  respuesta_json            jsonb       NULL,
  entidad_id                integer     NULL,
  recibido_at               timestamptz NOT NULL,
  CONSTRAINT ux_sync_operaciones_client_op_id UNIQUE (client_op_id)
);
```

`UNIQUE` **en la BD**, no en el service: es lo único que sobrevive a dos dispositivos enviando el
mismo lote a la vez (misma lección que `stock-inventario-atomico`). `respuesta_json` guarda la
respuesta original para devolverla **idéntica** en el replay.

---

## 5. Reglas de negocio

| # | Regla |
|---|---|
| R1 | `client_op_id` repetido ⇒ se devuelve la respuesta guardada, **no** se reprocesa. Mismo `estado`, mismo cuerpo |
| R2 | Registro de idempotencia y efecto en **la misma transacción**. Si el efecto falla, no queda marca |
| R3 | La empresa sale de la **operación**, validada contra el JWT. Desajuste ⇒ `empresa_no_autorizada`, sin reasignar |
| R4 | El autor lo estampa el **servidor**. El cuerpo no puede fijarlo |
| R5 | Un lote acepta **máximo 25** operaciones. Más ⇒ 400 tipado, no truncar en silencio |
| R6 | Respuesta **por operación**: el lote nunca es todo-o-nada. Una rechazada no bloquea a las demás |
| R7 | Códigos de error **tipados y estables** (`duplicado`, `validacion`, `empresa_no_autorizada`, `contrato_obsoleto`, `error_interno`), nunca texto libre en español |
| R8 | Fail-closed en el cliente: sin `userId`+`companyId`+`paisId` **no se encola** (el `0` cuenta como ausencia) |
| R9 | El outbox **no se purga** por logout, cambio de empresa ni kill switch |
| R10 | Solo se encola una mutación de la **lista blanca**; cualquier otra propaga el error de red como hoy |
| R11 | Solo se encola ante `status === 0`. Un 4xx/5xx **no** encola: hay red y el backend tiene algo que decir |
| R12 | La respuesta sintética es **distinguible** (`__offlinePendiente: true`): la UI no puede decir "guardado" a secas |

---

## 6. Casos de prueba

### Backend — `SyncPushCalculosTests` (puro)
1. Lote de 26 ops ⇒ rechazo `lote_excedido`
2. Op sin `clientOpId` ⇒ `validacion`
3. `clientOpId` no-uuid ⇒ `validacion`
4. Op con `companyId` que no está en el JWT ⇒ `empresa_no_autorizada`
5. Op con `companyId = 0` ⇒ `empresa_no_autorizada` (fail-closed, no "la del token")
6. Tipo desconocido ⇒ `contrato_obsoleto`
7. `capturadoAtDispositivo` en el futuro (> 24 h de tolerancia) ⇒ `validacion`
8. Op válida ⇒ `procesar`
9. Ya existe registro con ese `clientOpId` ⇒ `replay` con la respuesta guardada
10. Dos ops del mismo lote con el **mismo** `clientOpId` ⇒ la segunda `duplicado_en_lote`

### Backend — integración (HTTP real contra el back local)
11. Push de una op válida ⇒ 200, `estado: aplicada`, fila creada en `seguimiento_diario_levante`
12. **Reenviar el mismo lote** ⇒ 200, mismo `entidadId`, y **una sola** fila en la BD ⇒ la idempotencia funciona
13. Push con `companyId` ajeno ⇒ `empresa_no_autorizada` y **cero** filas nuevas
14. Op cuyo efecto falla (lote inexistente) ⇒ `rechazada` y **ninguna** fila en `sync_operaciones`… ver R2

### Frontend — unitarios
15. `decidirEncolable`: ruta de la lista blanca + POST ⇒ sí; GET ⇒ no; ruta fuera de lista ⇒ no
16. `decidirEncolable`: sin los tres ids ⇒ no (R8), con `companyId = 0` ⇒ no
17. `backoff`: crece exponencial, respeta `Retry-After`, tiene techo
18. `clasificarResultadoPush`: `validacion` ⇒ bandeja; `error_interno` ⇒ reintentable; 429 ⇒ frenar
19. `aplicarMigraciones` v1→v2 crea `outbox` **sin** perder `consultas`

### Frontend — integración con IndexedDB real
20. Sin red, un POST de la lista blanca ⇒ se encola y la respuesta trae `__offlinePendiente`
21. Sin red, un POST **fuera** de la lista ⇒ propaga el error (no encola)
22. Con red, un 500 ⇒ **no** encola
23. Logout ⇒ la caché de consultas se purga y **el outbox sobrevive** (R9)
24. Al reconectar ⇒ se empuja, y la operación confirmada **desaparece** de la cola

### End-to-end (back+front locales)
25. Sesión real, cortar la red, capturar un seguimiento, restaurar la red, ver la fila en la BD
26. Repetir el push manualmente ⇒ sigue habiendo **una sola** fila

---

## 7. Validación

- `dotnet build` 0 errores / 0 warnings nuevos · `dotnet test` sin regresión (base **2.237**)
- `yarn build` 0 errores (único warning aceptado: bundle budget preexistente)
- `yarn test` sin regresión (base **221**)
- `verificar-ngsw.js` verde y `verificar-lista-cacheable.js` sin pendientes
- Migración aplicada en local `:5433` y **corrida dos veces** (idempotente)
- 🔑 **Los tests tienen que VER el defecto**: desactivar la unicidad de `client_op_id` y comprobar que
  el caso 12 falla. Un test de idempotencia que pasa con la idempotencia rota no prueba nada
  (misma lección que D6 y que el gate de paridad de A9)

## 8. Riesgos

| Riesgo | Mitigación |
|---|---|
| La transacción condicional cambia el comportamiento del alta normal | El controller no abre transacción ⇒ la rama es la de hoy. Se prueba el alta por HTTP antes y después |
| La respuesta sintética hace creer que se guardó en el servidor | `__offlinePendiente` + contador de pendientes visible. R12 |
| El outbox crece sin límite si el push falla siempre | Tope de operaciones y aviso en la bandeja; nunca borrado automático (R9) |
| Validar con la cuenta super admin no prueba nada | D6 la excluye de la caché. El smoke usa un operario de **una** empresa |

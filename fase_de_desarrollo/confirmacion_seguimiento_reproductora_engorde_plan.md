# Plan — Confirmación por registro en Seguimiento Diario Reproductora (Pollo Engorde)

## Objetivo
Hoy el seguimiento diario de un **lote reproductora aves de engorde** se sincroniza **automáticamente**
al **seguimiento diario de pollo engorde** (`seguimiento_diario_aves_engorde`, filas `origen_cruce=true`)
en cada INSERT/UPDATE/DELETE, vía el trigger PostgreSQL `trg_cruce_reproductora_engorde` →
`fn_cruce_reproductora_a_engorde`.

Se agrega una **validación (confirmación) por registro**: la sincronización (cruce) hacia pollo engorde
**solo ocurre cuando el registro está confirmado**. Con 2+ lotes reproductora en el mismo lote de engorde,
**todos** deben confirmar el registro de esa edad para que ese día sincronice (misma lógica de "todos
tienen el día", pero contando solo confirmados). Un registro confirmado **no se puede editar**; para
corregirlo se **elimina** (el borrado ya restituye inventario y, al recalcular el cruce, "retorna" aves y
consumo) y se vuelve a crear. Los botones **Confirmar** y **Eliminar** quedan gateados por permiso.

## Decisiones tomadas (con el usuario)
1. **Reversibilidad:** confirmado = bloqueado para editar. Única corrección = **eliminar y recrear**. No hay "des-confirmar".
2. **Permisos:** dos nuevos → `seguimiento_reproductora_engorde.confirmar` y `seguimiento_reproductora_engorde.eliminar`.
   Se **otorgan a los roles que ya tienen el menú** del módulo (route `/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde`)
   para **preservar el borrado actual** (hoy sin permiso) y habilitar Confirmar.
3. **Datos existentes:** **backfill `confirmado=true`** en todas las filas actuales → el cruce histórico ya
   sincronizado no se pierde; el gate aplica solo a lo nuevo.

## Enfoque arquitectónico
- **La regla de negocio del gate vive en la BD** (la función de cruce), donde ya vive toda la lógica de
  sincronización. El backend solo agrega la acción de confirmar y el bloqueo de edición; el front, el botón + estado.
- Refactor ≠ cambio de comportamiento: para datos existentes (todos confirmados) el cruce produce el mismo
  resultado que hoy. El cambio de comportamiento es **intencional y solo para registros nuevos sin confirmar**.

## Cambios de BD / SQL
### Columnas nuevas en `seguimiento_diario_lote_reproductora_aves_engorde`
- `confirmado boolean NOT NULL DEFAULT false`
- `confirmado_at timestamptz NULL`
- `confirmado_por varchar(64) NULL` (UserId del que confirma)

### Migración 1 — `AddConfirmacionSeguimientoReproductoraEngorde` (schema + función)
Orden dentro de `Up()` (idempotente):
1. `ADD COLUMN IF NOT EXISTS` de las 3 columnas (confirmado se agrega **nullable** primero).
2. `CREATE OR REPLACE FUNCTION fn_cruce_reproductora_a_engorde` gateando por `confirmado = true`:
   - JOIN principal del subquery `dia`: `... AND s.confirmado = true`.
   - Subqueries de "aves vivas al inicio del día d" (acumulado de bajas edades `[1,d)`): `... AND p.confirmado = true`.
   - Efecto: `n_con` (COUNT DISTINCT repro_id con registro **confirmado** de edad d) = `v_n_lotes` ⇒ solo
     genera el día cuando **todos** los lotes confirmaron esa edad. 1 lote confirmado ⇒ copia directa. El
     `DELETE` previo del cruce por edad se mantiene ⇒ si se des-confirmara/eliminara, el día cruzado se borra.
   - Resto de la función idéntico (agua, peso ponderado, tipo alimento, fecha destino, metadata). Aritmética preservada.
3. Backfill: `UPDATE ... SET confirmado = true WHERE confirmado IS NULL` (marca lo existente; dispara el
   trigger con la función nueva → regenera el cruce idéntico).
4. `ALTER COLUMN confirmado SET DEFAULT false` + `SET NOT NULL`.
- `Down()`: `CREATE OR REPLACE` con la versión previa (la de `UpdateFnCruceReproductoraEngordeAgua`, sin gate) + `DROP COLUMN` de las 3.

### Migración 2 — `SeedPermisosConfirmarEliminarSeguimientoReproductora` (seed + grant)
- `INSERT permissions` (NOT EXISTS): `seguimiento_reproductora_engorde.confirmar`, `seguimiento_reproductora_engorde.eliminar`.
- `INSERT role_permissions`: a cada `role_id` con el menú del módulo, ambos permisos (NOT EXISTS).
- `Down()`: borra role_permissions + permissions de esas 2 keys.

## Cambios Backend (.NET)
| Archivo | Cambio |
|---|---|
| `Domain/Entities/SeguimientoDiarioLoteReproductoraAvesEngorde.cs` | +`Confirmado`, `ConfirmadoAt`, `ConfirmadoPor`. |
| `Infrastructure/.../SeguimientoDiarioLoteReproductoraAvesEngordeConfiguration.cs` | mapear `confirmado`, `confirmado_at`, `confirmado_por`. |
| `Application/DTOs/SeguimientoLoteLevanteDto.cs` | +`Confirmado=false`, `ConfirmadoAt=null`, `ConfirmadoPor=null` (params opcionales **al final** del record → no rompe otros servicios). |
| `Application/Interfaces/ISeguimientoDiarioLoteReproductoraService.cs` | +`Task<SeguimientoLoteLevanteDto?> ConfirmarAsync(int id);`. |
| `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` | `MapToDto` mapea los 3 campos; `UpdateAsync` lanza `InvalidOperationException` si `ent.Confirmado`; nuevo `ConfirmarAsync` (setea confirmado + at + por, fuerza `EntityState.Modified`, guarda → dispara trigger). `CreateAsync` deja `Confirmado=false` (default). Delete sin cambios (ya restituye). |
| `API/Controllers/SeguimientoDiarioLoteReproductoraController.cs` | inyectar `ICurrentUser`; `POST {id}/confirmar` gateado por `seguimiento_reproductora_engorde.confirmar`; `Delete` gateado por `seguimiento_reproductora_engorde.eliminar`. |

## Cambios Frontend (Angular)
| Archivo | Cambio |
|---|---|
| `features/lote-levante/services/seguimiento-lote-levante.service.ts` (interface compartida) | +`confirmado?`, `confirmadoAt?`, `confirmadoPor?`. |
| `seguimiento-diario-lote-reproductora.service.ts` | +`confirmar(id)` → `POST {baseUrl}/{id}/confirmar`. |
| `...-list.component.ts` | inyectar `UserPermissionService`; getters `canConfirmar`/`canEliminar`; estado del modal de confirmar (`confirmarModalOpen`, `pendingConfirmId`); métodos `confirmar(seg)`, `onConfirmConfirmar()`, `onCancelConfirmar()`; `edit()` bloquea si `seg.confirmado`; recargar tras confirmar. |
| `...-list.component.html` | columna **Estado** (badge Confirmado/Pendiente); botón **Confirmar** (solo si pendiente + `*appHasPermission='...confirmar'`); Editar `[disabled]` si confirmado; Eliminar envuelto en `*appHasPermission='...eliminar'`; modal de confirmación de la acción confirmar. |
| import `HasPermissionDirective` en el componente standalone. |

## Reglas de negocio
- Confirmar es una acción **manual** (el humano valida que la info está correcta); no hay validación automática de completitud.
- Confirmar es **idempotente** (si ya está confirmado, no falla).
- Editar un registro confirmado ⇒ 400 (backend) + botón deshabilitado (front).
- Eliminar confirmado ⇒ permitido (retorna aves/consumo por restitución de inventario + recálculo del cruce).
- Multi-lote: el día cruza solo si **todos** los lotes confirmaron esa edad.

## Casos de prueba
1. **1 lote, registro sin confirmar:** NO aparece cruce en pollo engorde. Al confirmar ⇒ aparece el día. (smoke)
2. **1 lote, editar confirmado:** 400 backend, botón Editar deshabilitado. (smoke + UI)
3. **2 lotes:** día d cruza solo cuando **ambos** confirmaron edad d; si uno se desconfirma vía delete ⇒ el día se borra del cruce. (smoke)
4. **Eliminar confirmado:** inventario restituido (ingreso), fila borrada, cruce del día eliminado, aves recalculadas. (smoke)
5. **Permisos:** sin `...confirmar` ⇒ botón oculto + 403; sin `...eliminar` ⇒ botón oculto + 403. (UI + smoke)
6. **Backfill:** filas existentes quedan `confirmado=true` y el cruce histórico no cambia. (verificación SQL)
7. **xUnit** (si se extrae lógica pura): equivalencia de comportamiento. La lógica del cruce es SQL → validación por smoke local + verificación de la función.

## Validación
- `cd backend && dotnet build` (0 errores) + `dotnet test`.
- `cd frontend && yarn build` (0 errores; único warning aceptado = bundle budget).
- Migración local: aplicar sobre `sanmarinoapplocal:5433`; smoke de confirmar/editar/eliminar con JWT.

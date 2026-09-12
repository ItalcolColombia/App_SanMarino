# Varios seguimientos diarios por día — que el flag de Empresa alcance de verdad

Fecha: 12-sep-2026 · Continúa [`seguimiento_produccion_multiples_registros_dia_plan.md`](seguimiento_produccion_multiples_registros_dia_plan.md)
(feature S0-S7, en `main-produccion` desde el 5-sep).

## 0. Qué pidió el usuario

El ticket de Santa Reyes: en levante y producción, un lote tiene que poder cargar **varios seguimientos
diarios el mismo día**. El comportamiento se prende o se apaga **por empresa, desde el módulo Empresas**
(flag `permite_multiples_seguimientos_diarios`) y se activa solo en las empresas que lo necesitan.

## 1. Auditoría (12-sep-2026, código de `main` + BD local)

El flag existe de punta a punta (columna, DTOs, `FLAGS_EMPRESA`, runtime del front) y está en
producción. Pero **encenderlo no alcanza**, y en producción ni siquiera funciona en Santa Reyes:

| # | Dónde | Qué pasa con el flag ON |
|---|---|---|
| H1 | `ProduccionService.Seguimiento.cs` alta (`ResolverFilaDuplicada`, 2 ramas) | Rechaza el 2.º registro para **toda** empresa: `bf28282` nunca tocó este archivo. |
| H2 | `ProduccionService.Seguimiento.cs` edición (`duplicadoDia`) | No se puede editar ningún registro de un día que tenga dos. |
| H3 | `SeguimientoDiarioService.UpdateAsync` (levante) | Ídem H2 en levante. El alta de levante sí lee el flag. |
| H4 | Índice `ix_seguimiento_diario_produccion_lote_id_fecha_registro` (UNIQUE por **instante**, está en el modelo EF) | El alta ancla la fecha a 12:00Z ⇒ dos registros del mismo día tienen el mismo instante ⇒ 23505 aunque se arregle H1. |
| H5 | Índice `uq_sdlr_tipo_lote_rep_fecha` (UNIQUE por instante, solo en BD) | El modal de levante manda `ymdToIsoAtNoon` ⇒ mismo choque. El smoke S7.3 pasó porque el 2.º registro se insertó por SQL con otra hora. |
| H6 | Índices por día `ux_*_dia_utc` | El predicado tiene **horneados** los `company_id` que tenían el flag al correr la migración del 5-sep (`company_id <> 6`). Encender el flag en otra empresa desde la UI no toca el índice. |
| H7 | Grilla de producción (`fn_seguimiento_diario_produccion` v3 agrupada) | Devuelve UNA fila por día con `MIN(seg_id)`: el 2.º registro no tiene fila propia para Editar/Eliminar/Validar. **Fuera de este bloque** (fase B, ver §5). |

## 2. Decisión (usuario, 12-sep-2026): trigger por fila

Se reemplazan los 4 índices únicos (H4-H6) por **triggers BEFORE INSERT/UPDATE** que exigen «un registro
por lote y por día» **solo si la empresa de la fila tiene el flag apagado**, leyendo `companies` en el
momento. Prender o apagar el flag desde la UI actúa al instante, sin migración por empresa.

- Carreras: `pg_advisory_xact_lock` por (tabla, lote, día) antes de mirar si ya hay otra fila.
- Error: `unique_violation` (23505) con el nombre del índice viejo como `CONSTRAINT`, para que cualquier
  manejo existente de 23505 siga igual.
- UPDATE: el trigger solo mira cuando cambia la clave (lote / día / tipo / reproductora / empresa).
- Apagar el flag conserva los días que ya tienen dos registros (no se borra nada).
- Levante: igual que antes, solo el tipo `levante` queda libre; reproductora y el tipo legacy
  `produccion` siguen con uno por día. Se conserva la exclusión del id 1090 (Demo).
- Los índices por instante pasan a **no únicos** con el mismo nombre (siguen sirviendo a las consultas);
  el de producción cambia en el modelo EF (`IsUnique()` fuera) + snapshot.

## 3. Archivos

**Backend**
- `Application/Calculos/SeguimientoVariosPorDiaCalculos.cs` (nuevo, puro) + `tests/.../SeguimientoVariosPorDiaCalculosTests.cs`.
- `Infrastructure/Services/Funciones/ProduccionService.Seguimiento.cs` — H1/H2 delegan en el cálculo; helper `PermiteMultiplesSeguimientosDiariosAsync`.
- `Infrastructure/Services/SeguimientoDiarioService.cs` — H3.
- `Persistence/Configurations/SeguimientoProduccionConfiguration.cs` + `ZooSanMarinoContextModelSnapshot.cs` — índice (lote, fecha) no único.
- Migración `20260912100000_SeguimientoUnicoPorDiaSigueFlagEmpresa` (+ Designer) — drop de los 4 únicos, índices no únicos, fns + triggers. `Down()` restaura los índices (fail-soft con WARNING si hay duplicados).
- Espejo `backend/sql/fn_trg_seguimiento_unico_por_dia.sql`.

**Frontend:** sin cambios en este bloque (la fila del flag ya está en `FLAGS_EMPRESA` y ningún form bloquea la fecha repetida).

**BD:** solo lo que aplica la migración. Carga masiva sigue fuera de alcance (decisión del plan original).

## 4. Reglas y casos de prueba

Flag OFF ⇒ comportamiento idéntico al actual (mensajes incluidos).

| Caso | OFF | ON |
|---|---|---|
| Alta producción, día vacío | inserta | inserta |
| Alta producción, día con fila de arrastre de huevos | mergea | mergea |
| Alta producción, día con registro del usuario | 400 «Ya existe…» | inserta 2.º |
| Edición producción/levante con otro registro ese día | 400 | guarda |
| Alta levante tipo `levante` 2.º del día | 400 | inserta |
| Reproductora 2.º del día | 400 | 400 |
| INSERT directo en BD (bypass del service) 2.º del día | 23505 | pasa |
| UPDATE en BD de un campo no clave en un día con 2 filas | pasa | pasa |
| Dos INSERT concurrentes mismo lote+día, OFF | uno 23505 | ambos |
| Encender el flag de otra empresa por UPDATE | — | su 2.º registro pasa sin migración |

Validación: `dotnet build` + `dotnet test`; migración por transacción con ROLLBACK (dos pasadas = idempotencia)
ejercitando la tabla de arriba con INSERTs reales sobre Santa Reyes (ON) y Sanmarino/Demo (OFF).

## 5. Fase B (pendiente de decisión, no incluida)

H7: la grilla de producción agrupa el día y oculta el 2.º registro para editar/borrar/validar. Levante
lo resolvió listando fila por registro (`posicionesEnElDia`). Producción necesita lo mismo antes de
activar el flag en una empresa que lo use de verdad.

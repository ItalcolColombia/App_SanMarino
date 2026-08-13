# Plan — Backup DB Studio: orden de funciones por dependencia real

**Motivo:** el backup descargable del 13ago26 (`sanmarino-2026-08-13-produccion.sql`) falla al restaurar
con `ERROR: function fn_seguimiento_diario_engorde(integer) does not exist` (SQLSTATE 42883) en 4
sentencias. Hay que corregirlo a mano en cada descarga. Este plan lo arregla en el generador.

## Diagnóstico

`DbStudioService.Backup.cs` → `WriteRoutinesAsync` emite las funciones **ordenadas por `p.oid`** (orden
de creación). El doc-comment justifica esa elección así:

> si "fn_a" (creada después) llama a "fn_z" (creada antes), el orden alfabético fallaría; el de
> creación no, porque "fn_z" no podría haberse escrito llamando a algo que no existía todavía.

**El razonamiento tiene un agujero: `DROP FUNCTION` + `CREATE`.** Recrear una función le asigna un OID
**nuevo**, más alto que el de sus llamadores, y la manda al final del archivo. Es exactamente lo que pasó
con `fn_seguimiento_diario_engorde`: cambiarle el `RETURNS TABLE` (v15, 08ago26) **obliga** a dropearla y
recrearla, así que quedó en la línea 106161, después de sus 5 llamadores.

Rompen solo los llamadores **`LANGUAGE sql`**, porque el cuerpo de una función SQL se parsea y valida
contra el catálogo **en el `CREATE`**. Los `plpgsql` tienen cuerpo opaco hasta ejecutarse y pasan callados:

| Llamador | Lenguaje | Resultado al restaurar |
|---|---|---|
| `fn_reporte_indicadores_panama` | `sql` | ❌ 42883 — no se crea |
| `fn_informe_semanal_pollo_engorde` | `sql` | ❌ 42883 — no se crea |
| `fn_reporte_diario_costos_engorde` | `sql` | ❌ 42883 — no se crea |
| `fn_cuadre_alimento_engorde` | `sql` | ❌ 42883 — no se crea |
| `fn_congelar_liquidacion_engorde` | `plpgsql` | ✅ pasa |

Impacto real: la BD restaurada queda **sin** el cuadre de alimento engorde, el reporte diario de costos
engorde, el informe semanal y los indicadores Panamá.

### Por qué no alcanza con `pg_depend`

El doc-comment ya lo decía y **queda confirmado empíricamente** contra la BD local restaurada: toda la
base tiene **2** filas de dependencia `pg_proc → pg_proc`. Postgres no registra en `pg_depend` las
llamadas a función dentro del cuerpo de una función SQL clásica (solo lo hace para cuerpos
`BEGIN ATOMIC`, PG14+, que este esquema no usa). No hay orden topológico disponible en el catálogo.

### Por qué no alcanza con "correr el archivo dos veces"

Es lo que sugiere hoy el propio encabezado del backup, y **es peligroso**: los 221 `INSERT` no llevan
`ON CONFLICT` y las tablas `_backup_*` no tienen PK ⇒ la segunda pasada **duplica filas**. Además obliga
a restaurar sin `ON_ERROR_STOP`, con lo que se pierde la capacidad de detectar errores genuinos.

## Enfoque

Orden topológico calculado **leyendo los cuerpos**, que es la única señal disponible. Lógica **pura** en
`Application/Calculos/` (regla del repo: math/lógica sin EF no va en Infrastructure), con tests xUnit.

1. `WriteRoutinesAsync` sigue leyendo `pg_get_functiondef(oid)` ordenado por `oid`, pero ahora también
   trae `p.proname` y bufferea la lista antes de escribir (55 funciones ≈ 1,5 MB: el streaming del
   backup completo lo domina el volumen de datos, no esto).
2. Nueva función pura `OrdenarRutinasPorDependencia`: para cada rutina detecta qué **otras** rutinas del
   mismo lote invoca su cuerpo (`nombre` + `(`, con frontera de palabra a la izquierda, permitiendo el
   calificado `public.nombre(`), arma el grafo y hace **Kahn** con desempate por OID.
3. **Degradación segura:** si queda un ciclo (solo puede venir de una arista falsa, porque un ciclo real
   entre funciones SQL es imposible de crear), esos nodos se emiten al final en orden de OID — o sea, el
   comportamiento de hoy. Nunca se pierde ni se duplica una rutina.
4. Actualizar el encabezado del backup: hoy miente ("volvé a correr este mismo archivo una segunda vez").

### Reglas del matcheo (por qué es seguro)

- Frontera izquierda: el carácter previo no puede ser `[A-Za-z0-9_]`. Un `.` **sí** se acepta, para
  capturar `public.fn_x(`.
- Frontera derecha: tras el nombre solo se permiten espacios y luego `(`. Esto ya descarta los prefijos:
  `fn_cuadre` no matchea dentro de `fn_cuadre_alimento_engorde(` porque le sigue `_`, no `(`.
- Autorreferencia excluida (recursivas).
- Un nombre mencionado en un **comentario** genera una arista de más, no de menos ⇒ a lo sumo adelanta
  una función que ya iba a estar antes; solo sería un problema si cerrara un ciclo, y ahí aplica (3).
- **Overloads:** varias rutinas comparten nombre ⇒ la arista va a todas. Sobre-restringe, nunca sub-restringe.

## Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/DbStudioSqlCalculos.cs` | + `record RoutineDef`, + `OrdenarRutinasPorDependencia`, + `RutinaInvocaA` (helper) |
| `Infrastructure/Services/DbStudio/Funciones/DbStudioService.Backup.cs` | `WriteRoutinesAsync` bufferea + ordena; encabezado corregido |
| `tests/ZooSanMarino.Application.Tests/DbStudioSqlCalculosTests.cs` | + tests del orden |

Sin cambios de BD, sin migración, sin cambios de contrato ni de front.

## Casos de prueba

1. **El caso real:** llamador con OID menor que el callee ⇒ el callee sale primero.
2. Sin dependencias ⇒ **orden de OID intacto** (no reordenar porque sí).
3. Cadena A→B→C declarada al revés ⇒ queda C, B, A.
4. Autorreferencia (recursiva) ⇒ no se cuelga, no se duplica.
5. Ciclo artificial ⇒ no se pierde ninguna rutina; caen al final en orden de OID.
6. **Invariante fuerte:** la salida es siempre una permutación exacta de la entrada (mismo multiset).
7. Prefijos: `fn_cuadre` NO se considera invocada por un cuerpo que dice `fn_cuadre_alimento_engorde(`.
8. Calificado con esquema: `public.fn_x(` sí cuenta como invocación.
9. Mención en comentario sin llamada real ⇒ arista de más tolerada, sin romper el orden.

## Validación

- `dotnet build` + `dotnet test` (0 errores).
- **Prueba de fuego contra datos reales:** regenerar el orden con los 55 cuerpos de la BD local
  restaurada y verificar que cada una de las 4 funciones rotas quede **después** de
  `fn_seguimiento_diario_engorde`, y que el archivo resultante restaure con `ON_ERROR_STOP=1` sobre una
  BD limpia, en **una sola pasada** y con 0 errores.

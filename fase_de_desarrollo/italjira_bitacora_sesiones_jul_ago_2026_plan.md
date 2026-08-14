# Plan — ItalJira: bitácora real de julio y agosto 2026 (horas, solución y bugs)

**Fecha:** 2026-08-13
**Tipo:** migración EF **data-only** (seed) + script generador reproducible
**Objetivo:** que ItalJira muestre, para julio y agosto 2026, **qué se pidió, cuánto costó,
cómo se resolvió y qué bugs aparecieron** — hoy el módulo tiene los títulos del trabajo pero
ni horas, ni descripción de la solución, ni los errores encontrados.

---

## 1. Punto de partida (lo que YA existe)

`20260807160000_SeedHistorialDesarrolloItalJira` sembró **19 historias + 198 tareas** derivadas de
los planes de `fase_de_desarrollo/` (título = H1 del plan, fechas = git, descripción = solo la ruta
del plan). Cubre **todo julio y agosto hasta el 06ago**. Por eso este trabajo **no vuelve a
sembrar** lo mismo: **enriquece** lo existente y **completa** lo que falta.

Decisiones tomadas con el usuario (13ago26):

| Decisión | Elegido |
|---|---|
| Duplicados | **Enriquecer + completar** — UPDATE sobre lo sembrado, INSERT solo de lo que falta |
| Unidad de trabajo | **Sesión de trabajo** (transcripciones de Claude Code, 134 en jul-ago) |
| Horas | **Estimación por juicio del contenido** (no derivada por fórmula) |

## 2. Fuentes (reales, verificables — nada inventado salvo la estimación)

| Dato | Fuente |
|---|---|
| Pedido del usuario | Primer mensaje real de cada sesión (`~/.claude/projects/.../<sid>.jsonl`) |
| Fechas y duración real | Primer/último timestamp de la sesión (huecos > 30 min descontados) |
| Qué se hizo / solución | Commits atribuidos a la sesión (ventana temporal + solape de archivos) |
| Bugs encontrados | Commits `fix(...)` de la ventana — **109** en jul-ago |
| Tarea existente a enriquecer | Plan de `fase_de_desarrollo/` tocado en la sesión ⇒ tarea sembrada |
| Horas estimadas | **Juicio** sobre el alcance real de cada trabajo (rúbrica en §5) |

**Medido:** 134 sesiones jul-ago · 205,6 h reales · 447 commits · 98 tareas existentes alcanzadas ·
39 sesiones sin tarea (20 triviales descartadas: «hola», revisar tracker, sin commits ni archivos).

## 3. Alcance del cambio

### 3.1 Enriquecer (UPDATE) — ~98 tareas ya sembradas
Solo las tareas cuya `descripcion` sigue siendo la original del seed (`Plan: fase_de_desarrollo/...`):
si alguien la editó a mano, **no se toca**. Se les escribe:

- `horas_estimadas` — estimación por juicio.
- `descripcion` estructurada: `Plan:` (se conserva) + **Pedido** (texto real del usuario, recortado)
  + **Solución** (commits en prosa) + **Bugs** (n.º, detalle en subtareas) + **Evidencia**
  (sha cortos, archivos tocados, id de sesión, horas reales de sesión).
- Las fechas **no se tocan**: quedan las del seed anterior (fechas reales de git). La ventana
  exacta de la sesión va en la línea «Evidencia», que es informativa y no altera el roadmap ni
  obliga a un `Down` con estado previo por fila.

### 3.2 Completar (INSERT) — ~39 tareas nuevas
Sesiones jul-ago sin plan (o con plan posterior al seed). Van a la **historia de módulo que ya
existe** (HIS-2026-0001..0020), clasificadas por archivos tocados y scope de los commits.
Código propio `SES-2026-MMDD-N` para no colisionar con los `HIS-2026-NNNN-Tn` del seed.
Tipo: `BUG` si el pedido/commits son de error · `MEJORA` si refactor/upgrade · `DOCUMENTACION`
si solo documenta · `TAREA` en el resto. Estado `LISTO` con fechas reales (las sesiones sin
commit quedan en `ANALISIS`/`DOCUMENTACION`, no en LISTO: no produjeron entrega).

### 3.3 Bugs encontrados (INSERT) — ~109 subtareas `BUG`
Una por commit `fix(...)`, colgando de la tarea de su sesión (`parent_tarea_id`), con
título = asunto del commit y descripción = cuerpo del commit (la causa raíz que se escribió
en su momento). Estado `LISTO`, fecha = fecha del commit.

### 3.4 Horas de la historia
`historias.horas_estimadas` = suma de las estimaciones de sus tareas de esta bitácora + lo que ya
tuviera. Se escribe con `UPDATE` sobre las 19 historias alcanzadas.

## 4. Archivos

| Archivo | Qué |
|---|---|
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260813NNNNNN_SeedBitacoraSesionesJulAgo2026.cs` | Migración documentada + `Down` |
| `...SeedBitacoraSesionesJulAgo2026.Seed.cs` | El SQL generado (partial, por tamaño) |
| `...SeedBitacoraSesionesJulAgo2026.Designer.cs` | Designer **clonado** de la migración anterior |
| `scratchpad/extraer_sesiones.py`, `cruzar.py`, `generar_seed.py` | Generador reproducible |
| `fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json` | **Las horas por juicio, versionadas** (auditable y regenerable) |

⚠️ **Data-only:** `ModelSnapshot` **NO** se toca (no hay cambio de esquema).

## 5. Rúbrica de la estimación (juicio, aplicada leyendo cada trabajo)

No es una fórmula automática: es el criterio con el que se leyó cada ítem, y queda escrito para
que la cifra sea discutible en vez de mágica.

| Alcance real del trabajo | Horas |
|---|---|
| Consulta, ajuste de config, SQL suelto, cambio de un texto | 1 – 2 |
| Fix de un módulo con diagnóstico acotado (front o back) | 2 – 4 |
| Fix con diagnóstico en BD, multipaís o cálculo compartido | 5 – 8 |
| Feature de un módulo (back + front + validación) | 8 – 14 |
| Feature grande: módulo nuevo, migración + tests + smoke en BD real | 16 – 40 |

Las horas de la **sesión real** quedan en la evidencia de la descripción, así se ve la diferencia
entre lo estimado y lo que efectivamente tomó.

## 6. Reglas de seguridad del seed (heredadas del seed anterior)

1. **Identidad por email** (`moiesbbuga@gmail.com`), nunca por guid fijo — ids difieren local↔prod.
2. **Fail-open silencioso:** si el usuario no existe en el entorno ⇒ `RAISE NOTICE` + `RETURN`.
   Un seed no puede tumbar el arranque de la app (lección del SIGSEGV).
3. **El int de auditoría NO es la cédula** (3177120174 no entra en `integer`): se reusa el
   `created_by_user_id` de los tickets del usuario.
4. **Idempotente:** UPDATE con guarda (`horas_estimadas IS NULL AND descripcion LIKE 'Plan: %'`),
   INSERT con `WHERE NOT EXISTS` por `codigo`. Correrla dos veces no cambia nada la segunda vez.
5. **`Down` reversible:** borra solo las subtareas/tareas de códigos `SES-2026-%` y devuelve las
   descripciones enriquecidas a su forma original (`Plan: <ruta>`) + `horas_estimadas = NULL`.

## 7. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | `dotnet build` | 0 errores, sin advertencias nuevas |
| 2 | `dotnet test` | Verde (no hay lógica nueva; nada debe romperse) |
| 3 | Aplicar en BD local | Migración corre sin error |
| 4 | Correrla **dos veces** | 2ª pasada: 0 filas afectadas (idempotencia) |
| 5 | `Down` y volver a aplicar | Vuelve al estado previo y re-siembra igual |
| 6 | Conteos | 98 tareas con horas · ~39 `SES-2026-%` · ~109 subtareas BUG con `parent_tarea_id` |
| 7 | Tarea editada a mano | Simular `descripcion` distinta ⇒ el UPDATE **no la toca** |
| 8 | Entorno sin el usuario | `RAISE NOTICE`, 0 filas, sin excepción |
| 9 | UI ItalJira | Tablero y roadmap cargan; las horas suman en el panel de indicadores |
| 10 | Órdenes del kanban | `orden` sin huecos ni repetidos por columna (regla frágil del módulo) |

## 8. Fuera de alcance (explícito)

- **No** se siembran worklogs (`ticket_tiempos`): la decisión fue estimar por juicio, no imputar
  horas reales como trabajo registrado.
- **No** se crean casos (`tickets`): esto es trabajo del área de desarrollo, no solicitudes de
  usuarios. Los tickets reales de julio-agosto ya existen con su propio flujo.
- **No** se toca junio ni nada anterior al 01jul26.

# Plan — Reporte Técnico alineado al ciclo y a la clasificación de huevo de la empresa

> Nombrado por **comportamiento**, no por tenant (regla §🏢 de `CLAUDE.md`). Hoy la única empresa
> que enciende estos flags es Santa Reyes; el módulo no la menciona por nombre en ninguna línea.
>
> ⚠️ **Vocabulario, para que no se repita el malentendido de la 1ª pasada:** *todas* las empresas
> cargan su **propia** guía genética —Sanmarino 889 filas, Demo 224, Ecuador 15, Santa Reyes 615,
> cada una filtrada por `company_id`—. Lo que cambia es **en qué tabla vive**:
> `guia_genetica_sanmarino_colombia` (esquema completo, ~50 columnas) o `guia_genetica_santa_reyes`
> (esquema simple, 3 métricas). Hablar de «guía propia vs compartida» hizo creer que unas empresas
> no tenían guía, y sobre esa premisa se dejó sin corregir un defecto que las afectaba a todas.

## Contexto

El módulo `/reportes-tecnicos` (menú **«Reporte Técnico Sanmarino»**, componente
`reporte-tecnico-main`, con sus dos vistas **Levante** y **Producción**) **está habilitado para
Santa Reyes** en `company_menus` — verificado en BD, es el único de los tres reportes técnicos que
esa empresa tiene prendido (`/reporte-tecnico-produccion` y `/reporte-tecnico-semanal` están
apagados). O sea: el usuario de Santa Reyes lo abre hoy, y lo que ve está a medias.

Lo que ya funciona (cerrado en X14.7, commit `457be71`): los 5 sitios que cargan la guía preguntan
primero por `guia_genetica_santa_reyes` vía `GuiaGeneticaLookup.ObtenerFilasPropiasAsync` y sólo
caen a `ProduccionAvicolaRaw` si vuelve vacía.

Lo que **no** está conectado, medido contra el código y la BD el 1-sep-2026:

1. **La guía propia sólo llena 3 de las ~17 columnas de comparación.**
   `GuiaGeneticaLookup.ATransitoria` proyecta `prod_porcentaje`, `retiro_ac_h` y `gr_ave_dia_h`;
   el resto de `ProduccionAvicolaRaw` queda `null`. En **Levante** eso deja 2 columnas GUÍA con
   dato (`RetiroHGUIA`, `GrAveDiaGUIAH`) de 17 — peso, uniformidad, mortalidad semanal, consumo
   acumulado y **las 8 de machos** salen vacías. En **Producción**, de los 4 campos que pide
   `ObtenerGuiaParaSemana` (`ProdPorcentaje`, `PesoHuevo`, `HTotalAa`, `Uniformidad`) sólo el
   primero existe.
2. **El eje de la semana en Producción no cruza con el de la guía — EN NINGUNA EMPRESA.**
   Los reportes calculan `semana = ceil((fecha − fecha_inicio_produccion + 1)/7)` → arranca en 1,
   mientras las **dos** tablas de guía se indexan por **semana de vida**:
   `guia_genetica_santa_reyes` va de la 18 a la 140, y `guia_genetica_sanmarino_colombia` de la 1 a
   la 71-97 con su **primera edad con producción en la 25/26** —la semana en que el ave empieza a
   poner—. Medido en P-K345B: la semana 1 de postura se comparaba contra la edad 1 (pollita de un
   día) y la 29 de postura contra la meta de la 29 de vida, que es la 5 de postura.
   *(Alcance ampliado en la 2ª pasada: la 1ª sólo lo corregía para la guía de esquema simple.)*
3. **Las columnas de huevo incubable están muertas y nadie las tapa.**
   `AplicarTotalesHuevoPorItems` escribe **`huevo_inc = 0` a propósito** («postura comercial, no
   incuba») junto con las 11 legacy. Las 4 tablas de la pestaña Producción pintan `huevoInc` y
   `%Incubables` **sin un solo `@if`**: `grep clasificacionHuevoPorItems` sobre
   `features/reportes-tecnicos/` da **0 coincidencias**. El gateo de X18.7/X18.8 se hizo en los
   *otros* reportes; a este —el único que Santa Reyes ve— no llegó.
   Y no hay ninguna columna que muestre el desglose real (`metadata->'huevoItems'`).
4. **Las fases del ciclo de la empresa no las aplica nadie.**
   `SemanasCicloPosturaCalculos.ObtenerEtapa` existe con tests desde el 20-ago-2026 y
   `semanas_ciclo_postura_por_raza` está en `t` para la empresa, pero **ningún servicio la
   consume** (grep: sólo su propia definición, DTOs del flag y doc-comments). El reporte de levante
   filtra duro `edad >= 1 && edad <= 25` y el de producción asume postura desde la 26 — los cortes
   de Sanmarino, no los de esta empresa (8 alistamiento / 16 levante / 4 levante-en-producción /
   74-84 postura ⇒ levante hasta la **24**, postura desde la **29**).

## Decisiones del usuario (tomadas en sesión, 1-sep-2026)

- **Enfoque: adaptar el módulo existente con flags**, no duplicarlo. Es el patrón obligatorio de
  `CLAUDE.md` §🏢 y evita clonar 7 componentes de front + 12 partials de backend + 2 servicios de
  Excel. El menú se renombra a algo neutro.
- **Guía faltante: ocultar las columnas que la guía de la empresa no puede llenar**, en vez de
  dejarlas en blanco. No se amplía `guia_genetica_santa_reyes` en este pase.
- **Levante semanas 1-17: mostrar el real sin comparación, con un aviso en pantalla** de que la
  guía de esa línea arranca en la semana 18. No se corta el reporte ni se bloquea esperando dato
  del cliente.

## Enfoque

Tres flags **ya existentes** deciden todo; ninguno se crea ni se renombra:

| Señal | Origen | Qué decide acá |
|---|---|---|
| `clasificacion_huevo_por_items` | `companies` (bool) | Huevos por ítems en vez de Incubable/%Incub |
| `semanas_ciclo_postura_por_raza` | `companies` (bool) | Cortes de fase y eje de semana de vida |
| En qué **tabla** vive la guía de la empresa | `ObtenerFilasPropiasAsync().Count > 0` | Qué columnas GUÍA se pintan |

**Invariante rector:** con los dos flags de `companies` en OFF, todo lo que este pase AGREGA
(columnas de huevo por ítems, etapa del ciclo) queda idéntico a antes por construcción: cada rama
nueva cuelga de un `if (flag)`.

**La excepción, deliberada y pedida:** el **eje de la guía** SÍ cambia para las cinco empresas,
porque estaba mal para las cinco (regla 1). No es un efecto colateral: es el defecto que este pase
corrige, y su impacto está medido fila a fila.

## Archivos

### Backend

**Nuevos (cálculo puro, `Application/Calculos/`):**
- `SemanaGuiaProduccionCalculos.cs` — qué semana usar para cruzar la guía (relativa a producción vs
  semana de vida) según el origen de la guía.
- `GuiaMetricasDisponiblesCalculos.cs` — dado el conjunto de filas de guía, qué métricas tienen al
  menos un dato. Con guía compartida devuelve «todas», así el front no oculta nada.
- `HuevoItemsResumenCalculos.cs` — resume `huevoItems` en `Primera`/`Pnc`/`Otros`. Espejo exacto de
  `resumir-huevo-items-por-tipo.funcion.ts` (front) — si cambia uno, cambian los dos.

**Modificados:**
- `Services/Funciones/ReporteTecnicoProduccionService.Tabs.cs` — eje de semana, resumen de ítems,
  disponibilidad de guía, etapa del ciclo.
- `Services/Funciones/ReporteTecnicoService.LevanteTabs.cs` — rango de semanas de guía por ciclo de
  empresa, `SemanaGuiaDesde`, disponibilidad de guía.
- `Services/Funciones/ReporteTecnicoService.LevanteCompleto.cs` — mismo tratamiento de guía.
- `Services/ReporteTecnicoProduccionService.cs` (ancla) — helper de resolución de flags.
- `Services/ReporteTecnicoService.cs` (ancla) — ídem para levante.
- `DTOs/ReporteDiarioGalponDto.cs`, `ReporteTecnicoProduccionTabsDto.cs`,
  `ReporteTecnicoLevanteCompletoDto.cs` — campos nuevos, **todos con default** para no romper
  ningún constructor posicional existente.
- `Services/ReporteTecnicoProduccionExcelService.cs` y `ReporteTecnicoExcelService.cs` — mismo
  gateo en las hojas exportadas (el Excel de este módulo se genera en backend, endpoints
  `/ReporteTecnicoProduccion/exportar-excel-tabs` y `/ReporteTecnico/levante/exportar-excel`).

### Frontend
- `features/reportes-tecnicos/pages/reporte-tecnico-main/` — lee los flags, aviso de tramo sin
  guía, pasa los flags a los hijos.
- `features/reportes-tecnicos/components/reporte-general-diario|reporte-general-semanal|reporte-diario-galpon|reporte-semanal-galpon/`
  — columnas de huevo y de guía gateadas (mismo patrón `@if (!ocultaMachosEnPostura)` que ya vive
  al lado, con `[attr.colspan]` recalculado).
- `features/reportes-tecnicos/components/tabla-levante-completa|tabla-levante-semanal-hembras|tabla-levante-semanal-machos/`
  — columnas GUÍA gateadas por disponibilidad.
- `features/reportes-tecnicos/models/` (nuevo) — tipos compartidos extraídos de los componentes.
- `features/reportes-tecnicos/funciones/` (nuevo) — `columnas-guia-visibles.funcion.ts`,
  `resumen-huevo-reporte.funcion.ts` (puras, sin `this` ni DI).

### BD
- Migración EF **idempotente** que renombra el menú `/reportes-tecnicos` de «Reporte Técnico
  Sanmarino» a **«Reporte Técnico»**, localizando por `route` y nunca por id (los ids difieren
  local↔prod). `UPDATE … WHERE label IS DISTINCT FROM` para no ensuciar el histórico.
- **No se crea ninguna columna nueva**: los tres flags ya existen en `companies`.

## Reglas de negocio

1. **Eje de la guía — CORREGIDO PARA TODAS LAS EMPRESAS (1-sep-2026, 2ª pasada).** La fila
   diaria/semanal de producción cruza por **semana de vida** (`ceil((fecha − fecha_encaset + 1)/7)`),
   sin importar en qué tabla viva la guía de la empresa.

   La primera versión de este plan sólo cambiaba el eje para la guía dedicada y dejaba anotado como
   «preexistente, fuera de alcance» el desfase de las demás. **El usuario señaló que cada empresa
   carga su propia guía y pidió validarlo. Se midió y el desfase era real y grave para todas.**

   La prueba de que la columna `edad` es semana de VIDA y no de postura: en
   `guia_genetica_sanmarino_colombia` la edad va de 1 a 71-97, la **primera edad con producción es
   la 25/26** —cuando el ave empieza a poner— y la primera con `peso_h` es la **1**. Por eso
   `ObtenerGuiaGeneticaProduccionAsync` ya filtraba `edad >= 26`.

   Efecto medido en P-K345B (encaset 2025-01-31, inicio de producción 2025-07-19, 169 días ⇒
   semana 25): su semana 1 de postura se comparaba contra la edad 1 —producción y peso vacíos,
   `uniformidad = 70` de levante— y su semana 29 de postura contra la meta de la semana 29 de vida,
   que es la 5 de postura. Todo corrido ~24 semanas. Cobertura de la guía: **126/301 → 294/301**
   filas con % Postura Guía, **133/301 → 294/301** con Peso Huevo Guía.

   Alcance: los DOS reportes (`Tabs.cs` y `Cuadro.cs`, que usaba `EdadInicioSemanas`, contada
   también desde el inicio de producción). Los lotes vivos tienen todos su `fecha_encaset`, con
   desfases de 128 a 363 días.
2. **Columna de guía sin ningún dato ⇒ no se pinta.** La regla se aplica **sólo** cuando la guía es
   propia. Con guía compartida el DTO informa «todas disponibles» y el front pinta lo de siempre,
   incluso si una fila puntual viene incompleta (que es el comportamiento actual y no se toca).
3. **Huevos.** Con `clasificacion_huevo_por_items` ON: la cabecera «Huevos» pasa de `Tot | Inc` a
   `Tot | Primera | Pnc` (más `Otros` sólo si hay ítems de tipo desconocido con cantidad > 0), y
   `%Incubables` se retira. `huevo_tot` / `%Postura` / peso de huevo **no se tocan nunca**: son
   correctos en las dos configuraciones y son el número de cuadre.
4. **Fases.** Con `semanas_ciclo_postura_por_raza` ON, cada fila trae su etapa
   (`Alistamiento`/`Levante`/`LevanteEnProduccion`/`Postura`/`FueraDeCiclo`) resuelta por
   `SemanasCicloPosturaCalculos.ObtenerEtapa(raza, semanaVida)`, que ya existe y tiene tests. Si la
   raza no se reconoce devuelve `null` y **no se muestra etapa** — no se adivina.
5. **Rango de guía en levante.** El filtro fijo `1..25` pasa a ser el fin de levante del ciclo de la
   empresa (24 con el flag ON, 25 con el flag OFF). El aviso de tramo sin guía se dispara con la
   semana mínima real de la guía cargada, no con un 18 hardcodeado.
6. **Nada de `if (empresa == …)` ni `if (pais == …)`** en ninguna línea nueva.

## Casos de prueba (xUnit, `tests/ZooSanMarino.Application.Tests/`)

- `SemanaGuiaProduccionCalculos`: guía compartida ⇒ devuelve la semana relativa tal cual (delta
  cero); guía propia ⇒ semana de vida; `fecha_encaset` nula ⇒ cae a la relativa sin lanzar.
- `GuiaMetricasDisponiblesCalculos`: filas de guía propia (sólo 3 métricas) ⇒ marca disponibles
  exactamente esas 3; filas de guía compartida completas ⇒ marca todas; lista vacía ⇒ ninguna;
  una métrica con dato en 1 de 100 filas ⇒ disponible.
- `HuevoItemsResumenCalculos`: equivalencia caso por caso con
  `resumir-huevo-items-por-tipo.funcion.ts` (Primera/Pnc/desconocido/cantidad ≤ 0/lista vacía/null).
- `SemanasCicloPosturaCalculos` (ya existe): se reusa, no se toca.
- **Gate de no-regresión:** para cada cálculo nuevo, un test con el flag en OFF que verifica salida
  idéntica a la fórmula previa.

## Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test` (3.590 verdes
  hoy; los nuevos suman).
- `cd frontend && yarn build` (0 errores; único warning aceptado el de *bundle budget*
  preexistente) + `yarn test`.
- **Smoke doble, obligatorio** (`CLAUDE.md` §🏢.8):
  - Empresa con los flags **OFF** (Sanmarino con un lote real de producción y otro de levante) ⇒
    **cero cambios visibles**, columna por columna, y Excel idéntico.
  - Empresa con los flags **ON** ⇒ huevos por ítems, columnas de guía sin dato retiradas, aviso de
    tramo sin guía en levante, etapa del ciclo visible.
- Backend local: se levanta **sólo** para el smoke y se apaga al terminar, confirmando puerto libre
  (`CLAUDE.md` §🔌).

## Fuera de alcance (escrito, no olvidado)

- **Ampliar `guia_genetica_santa_reyes`** con peso/uniformidad/consumo acumulado/mortalidad
  semanal: decisión explícita del usuario de no hacerlo en este pase. Cuando el cliente entregue
  esas métricas, las columnas se prenden solas — `GuiaMetricasDisponiblesCalculos` ya las detecta.
- **Cargar las semanas 1-17 de la guía de levante**: depende del cliente.
- **Los otros dos reportes técnicos** (`/reporte-tecnico-produccion`, `/reporte-tecnico-semanal`)
  siguen apagados para esta empresa; su gateo de huevo ya se hizo en X18.7/X18.8 y no se toca acá.
- ~~El desalineamiento del eje de semana~~ → **CORREGIDO** en la 2ª pasada, a pedido del usuario.
  Ver regla 1. La versión inicial de este plan lo daba por «fuera de alcance» apoyándose en una
  premisa equivocada (que sólo una empresa tenía guía propia); la corrección del usuario la
  desarmó.
- **La colisión `25P` vs `25`** en la guía de esquema completo (una fila por raza/año: `25P` es
  prepostura y el parseo tolerante la colapsa en 25) queda resuelta con un desempate determinista
  —gana la grafía numérica pura—, no con una regla de negocio inventada. Antes no importaba porque
  el eje viejo nunca alcanzaba la semana 25; ahora es justo el arranque de la postura. **Conviene
  confirmarlo con el cliente**: las dos filas llenan campos distintos (`25P` trae `peso_huevo`,
  `25` trae `uniformidad`).

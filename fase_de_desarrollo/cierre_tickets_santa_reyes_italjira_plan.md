# Cierre de los tickets de Santa Reyes en ItalJira (31-ago-2026)

Cierra en ItalJira todo el trabajo de Santa Reyes que **ya está construido, probado y desplegado**,
por migración EF data-only. No toca una sola línea de código funcional: solo mueve el estado de los
casos, la historia y sus tareas para que el tablero refleje la realidad.

## 0. Qué había abierto (medido en `sanmarinoapplocal:5433`, no asumido)

| Objeto | Código | Estado hoy | Qué es |
|---|---|---|---|
| Caso | `TK-2026-000172` | **ABIERTO** | Requerimientos de Italapp: 100 h, 10 jornadas, entrega 1-sep-2026 |
| Historia | `HIS-2026-0024` | **BACKLOG** | Implementacion de Italapp para Santa Reyes |
| 13 tareas + 29 subtareas | `HIS-2026-0024-T*` | **BACKLOG** (42) | Los paquetes F0…F12 |
| Caso | `TK-2026-000180` | **ABIERTO** | Las 6 definiciones que faltaban del cliente |
| 6 tareas | `SR-DEF-1..6` | **BLOQUEADA** | Una por definición |

Son **los 2 únicos tickets en ABIERTO de toda la base** y **las 42 únicas tareas en BACKLOG**.

## 1. Validación: qué está realmente hecho (contra código y BD, no contra el tracker)

| Paquete | Evidencia verificada |
|---|---|
| F0.1 Flags por empresa | 8 columnas en `companies`, Santa Reyes (id 6) con sus valores; `flags-empresa.funcion.spec.ts` blinda el cableado |
| F0.2 Catálogo de ítems | `catalogo_items` company 6, `item_type='huevo'`: **28 filas** (10 Primera + 18 Pnc) |
| F1.1 Silos | `FarmSilo` / `GalponSilo` / `LoteSilo` / `SiloCatalogo` + tablas en BD |
| F1.2 Códigos ERP | `ManejaCodigosErpAvicola` en `Company`, `Farm` y las 3 proyecciones de `CompanyDto` |
| F2.1 Guías genéticas | `guia_genetica_santa_reyes`: **615 filas** = 5 razas × 123 semanas |
| F2.2 Guía en indicadores | `GuiaGeneticaLookup` + `RazaGuiaAliasCalculos` + 3ª rama de `vw_guia_genetica_postura` (30-ago, `10a510f`) |
| F3.1/F3.2 Semanas por raza | `SemanasCicloPosturaCalculos` + flag `semanas_ciclo_postura_por_raza` |
| F4.1/F4.2 Consumo hembras | flag `consumo_alimento_solo_hembras` |
| F5.1/F5.2 Machos y sexaje | flag `oculta_machos_en_postura` en front y back |
| F5.3 Machos en ventas | cerrado por decisión del usuario (24-ago): se **retiran**, no se agrega campo |
| F6.1 Tipos de inventario | `TIPOS_ITEM_ALIMENTO_Y_AVES` en `catalogo-alimentos-list` |
| F7.1/F7.2 Huevo sin clasificar | grilla y modal gateados por `clasificacionHuevoPorItems` |
| F7.3 Tipos de huevo del lote | `lote_huevo_items` + `GET /LoteHuevoItem/por-granja/{id}/disponibles` + sección en el alta (30-ago) |
| F7.4 Vigencia primera postura | `HuevoPrimeraPosturaCalculos` + `huevo_primera_postura_hasta_semana` |
| F8.2 Huevo tratado/peso/alimento | `@if (!clasificacionHuevoPorItems)` en modal y tabla |
| F9.1/F9.2 Traslado de aves | machos ocultos + placa/conductor/sellos expuestos |
| F9.2c Comprobante | `ComprobanteTrasladoAvesComponent` (primer comprobante del repo) |
| F10.1 Bodega destino | `mostrarPlantaCatalogoTraslado()` en `modal-traslado-huevos` + listas maestras sembradas |
| F10.2 Tipos de huevo del traslado | `clasificacionHuevoPorItems` en el modal |
| F11.1/F11.2 Pruebas | 10 suites xUnit propias + gate de paridad SQL↔C# |
| F12.1 Despliegue | verificado con el checklist de CLAUDE.md §🚀 (TaskDef 161, imagen = merge de PR #75) |

### Lo único que NO se entregó (decisión del usuario: cerrar dejando constancia escrita)

- **F8.1 / `SR-DEF-3`** — 7 ítems (`ENYEMADO` ×4, `DECOLORADO` ×3) existen en el catálogo **sin
  `codigo` ERP**: el `Items.xlsx` del cliente trae 21 ítems y ninguno Enyemado, mientras el `.docx`
  sí lo pide. Los dos documentos del cliente se contradicen. No se inventan códigos.
- **F8.3 / `SR-DEF-4`** — panel de eficiencia con la nomenclatura nueva: depende de F8.1.
- **F11.3** — pruebas asistidas con el usuario de Santa Reyes sobre datos reales: fuera del repo.

Los tres quedan **escritos en la solución del caso y en la nota de cierre**, con el detalle de qué
falta y por qué, para que cerrarlos no borre el rastro.

## 2. Migración `20260831120000_CerrarPlanItalappSantaReyes` (data-only)

Copia el patrón del seed que los creó (`20260819120000_SeedTicketPlanItalappSantaReyes`):

- **Identidad por dato, nunca por id**: admin por email, empresa por `name LIKE '%santa%reyes%'`,
  historia y casos por `titulo`, tareas por `codigo`/prefijo del título. Los ids difieren local↔prod.
- **Fail-open**: sin el admin o sin la empresa, `RAISE NOTICE` + `RETURN` (con
  `Database__RunMigrations=true` un seed no puede tumbar el arranque de la app).
- **Idempotente**: cada `UPDATE` filtra con `IS DISTINCT FROM`; las notas van con `WHERE NOT EXISTS`
  por su texto. Correrla dos veces no mueve una fila.
- **Fechas deterministas, no `now()`**: el fin real de cada tarea es la fecha en que su paquete
  cerró de verdad (21-ago V52 · 24-ago X18 · 31-ago guía SQL + huevo al alta), para que el
  cronograma del tablero no mienta.
- **Espeja el servicio**: `TicketService.ConfirmarCierreAsync` escribe `estado`,
  `fecha_cierre_solicitante` y `cerrado_por_user_id`; `CambiarEstadoAsync` escribe
  `solucion_descripcion` y `fecha_solucion` + una nota por evento. La migración escribe **lo mismo**,
  incluidas las 2 notas por caso, porque la línea de tiempo se **deriva** de notas + tareas.
- **`Down()`** devuelve los 2 casos a `ABIERTO`, la historia y las 42 tareas a `BACKLOG`, las 6
  `SR-DEF` a `BLOQUEADA`, limpia las fechas reales y borra las 4 notas.

## 3. Casos de prueba

1. `Up()` dos veces en una transacción revertida ⇒ la 2ª pasada reporta **0 filas afectadas**.
2. `Down()` tras `Up()` ⇒ los estados vuelven exactos a los medidos en §0.
3. Post-`Up()`: `SELECT estado FROM tickets WHERE company_id=6` ⇒ **0 en ABIERTO**;
   `ticket_tareas` ⇒ **0 en BACKLOG y 0 en BLOQUEADA** para esos 2 casos.
4. Ninguna otra empresa cambia: `tickets`/`ticket_tareas`/`historias` con `company_id <> 6` intactos.
5. `dotnet build` + `dotnet test` verdes; gate `verificar-sql-llega-por-migracion.js` pasa.

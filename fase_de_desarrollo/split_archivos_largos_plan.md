# Split de los archivos largos detectados en la auditoría (23-ago-2026)

Los 12 archivos que salieron en el reporte de auditoría completa (back+front), confirmados por el
usuario para partir **todos**: 6 backend `.cs` (>1200 líneas) + 6 frontend `.component.ts`
(>1500 líneas).

## Enfoque arquitectónico

**Corte mecánico, no reescritura.** Cada método/función se mueve verbatim — mismo cuerpo, mismo
orden de statements, misma aritmética. Nada de "aprovechar y mejorar la lógica de paso": eso es
otra tarea, y mezclarla acá viola "Refactor ≠ cambio de comportamiento".

**Backend** — patrón `partial class` en `Funciones/` (CLAUDE.md, sección Clean Code):
- El corte se hace con una herramienta propia que balancea llaves respetando strings/comentarios
  (`split_cs.py`, en el scratchpad de la sesión) para no tocar un solo carácter de lógica.
- Cada método se agrupa por responsabilidad real (verificada por sus llamadores, no por cercanía
  física en el archivo — hubo casos donde dos métodos definidos junto a un grupo en realidad solo
  los usaba OTRO grupo).
- El ancla conserva: usings, campos, ctor, la interfaz `: IXxx`, y los helpers que usan 3+ grupos
  distintos (cross-concern).
- Namespace plano (`ZooSanMarino.Infrastructure.Services`) en todos los archivos nuevos, aunque
  vivan en subcarpeta — no rompe DI. Si el ancla usa namespace en bloque (llave, no `namespace X;`),
  los archivos nuevos igual usan file-scoped (así ya lo hacen los partials hermanos existentes) y el
  contenido movido se re-indenta -4 espacios para no arrastrar la indentación anidada.
- Verificación tras CADA archivo: `dotnet build` (0 errores/warnings) + `dotnet test` (mismo número
  de tests que la línea base: **3.135**). Si cambia el número, algo se llevó lógica puesta.

**Frontend** — patrón `funciones/` + `models/` (referencia: `movimientos-pollo-engorde`):
- Funciones puras (sin `this`, sin DI) que hoy son métodos de la clase pero no tocan estado del
  componente más que leer parámetros y devolver un resultado → `funciones/<accion>.funcion.ts`.
- Tipos/interfaces hoy inline en el componente → `models/<concepto>.model.ts`, re-exportados desde
  el componente si algo externo los importaba.
- El componente queda como orquestador delgado: arma datos, llama la función, maneja HTTP/UI.
- El método público que usa el template se conserva (el template lo sigue llamando por `this.`);
  su cuerpo pasa a delegar en la función central.
- Verificación tras CADA archivo: `yarn build` (0 errores/warnings) + `yarn test` (mismo número de
  tests que la línea base: **633**).

## Línea base (medida antes de tocar nada, 23-ago-2026)

| | Resultado |
|---|---|
| `dotnet build` | 0 errores, 0 warnings |
| `dotnet test` | **3.135** pasan |
| `yarn build` | 0 errores, 0 warnings |
| `yarn test` | **633** pasan |

## Los 12 archivos y su split

### Backend

| # | Archivo | Líneas | Split |
|---|---|---|---|
| 1 | `ReporteTecnicoService.cs` | 3.267 | ✅ Diario / Semanal / Sublotes / Alimento / LevanteCompleto / LevanteTabs (6 archivos) |
| 2 | `InventarioGestionService.cs` | 3.061 | Consulta / Ingreso / Traslado / StockMutacion / Consumo / Movimientos (6 archivos nuevos; ya tenía Silos/StockAtomico/ValidacionConsumo) |
| 3 | `ReporteTecnicoProduccionService.cs` | 1.991 | Diario / Semanal / Cuadro / ClasificacionHuevo / Tabs (a definir al llegar, mismo criterio: agrupar por consumidor real) |
| 4 | `ReporteContableService.cs` | 1.786 | CalculoSemanal / MovimientosHuevos / Filtros (calca los `#region` que el archivo ya tenía) |
| 5 | `TicketService.cs` | 1.402 | Creacion / Busqueda / Detalle / Adjuntos / Estado (ya tenía Gestion/Indicadores) |
| 6 | `LoteService.cs` | 1.353 | A definir al llegar (ya tenía AjusteEncasetamiento/Mover) |

### Frontend

| # | Componente | Líneas | Split |
|---|---|---|---|
| 7 | `lote-levante/pages/modal-create-edit` | 2.482 | A definir al llegar |
| 8 | `gestion-inventario/pages/gestion-inventario-page` | 2.164 | A definir al llegar |
| 9 | `lote-produccion/pages/modal-seguimiento-diario` | 2.005 | A definir al llegar |
| 10 | `lote/components/lote-list` | 1.905 | A definir al llegar |
| 11 | `engorde-comun/pages/modal-seguimiento-engorde` | 1.829 | A definir al llegar |
| 12 | `traslados-aves/pages/inventario-dashboard` | 1.691 | A definir al llegar |

Los splits "a definir al llegar" se deciden leyendo cada archivo (TOC de métodos + quién llama a
quién), igual que los primeros dos — no se define una estructura de antemano sin haber visto el
archivo real, porque la agrupación por nombre casi nunca coincide con la agrupación por uso real
(pasó en el archivo #1: dos métodos definidos junto a un grupo, usados solo por otro).

## Casos de prueba

No hay casos de prueba nuevos: es movimiento de código, no comportamiento nuevo. El caso de prueba
ES la no-regresión: mismo build, mismo número de tests, en cada uno de los 12 archivos.

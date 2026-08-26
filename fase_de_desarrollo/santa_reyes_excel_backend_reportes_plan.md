# Plan — Excel backend ciego a `huevoItems` (Contable + Técnico Producción)

## Contexto

Continúa [`santa_reyes_reportes_ciegos_huevo_items_plan.md`](santa_reyes_reportes_ciegos_huevo_items_plan.md)
(X18.7): esa vez se arregló el FRONTEND de 4 pantallas. El Excel que generan
`ReporteContableExcelService`/`ReporteTecnicoProduccionExcelService` (ambos EPPlus, backend, **sin
relación** con el `xlsx` del frontend) quedó explícitamente fuera de alcance. Este plan lo cierra.

Auditoría (doble lectura cruzada por archivo, 4 agentes) confirmó **NO existe ningún gateo de
`OcultaMachosEnPostura` en ningún exportador Excel del backend** — la premisa de que "ya había un
patrón que copiar" era falsa; el barrido de machos fue enteramente frontend. Hay que construir el
mecanismo desde cero, en los 2 archivos.

## Hallazgo nuevo: un 4º defecto real (`DESCARTE`)

`TrasladoHuevosService.CrearTrasladoHuevosAsync:191-203` ya zerea las 11 `Cantidad*` (incluida
`CantidadDesecho`) cuando `usaHuevoItems=true` — **mismo patrón que las 11 columnas legacy de
`seguimiento_diario_produccion`, pero en la tabla `traslado_huevos`**. Verificado contra datos
reales: los 2 traslados de Santa Reyes (`cantidad_desecho=0` ambos). La columna `DESCARTE` del
reporte Contable (pantalla Y Excel) queda tan rota como `HVTO FERTIL`/`HVO COMERCIAL`/`HUEVO
DESECHO` — **no estaba gateada en el front tampoco** (se agrega ahora, gap de la sesión anterior).

## Hallazgo nuevo: 3ª hoja del Excel de Técnico Producción, no mencionada en el alcance original

`ReporteTecnicoProduccionExcelService.GenerarExcelCompleto` genera una hoja **"Clasificación
Huevo"** (`EscribirClasificacionHuevo`) que el ticket original no nombraba. 16 de sus 18 columnas
de datos salen de las 11 legacy — es la hoja MÁS rota de las dos. El front ya oculta la pestaña
homónima ENTERA para Santa Reyes (X17.5/X18.4). Mismo tratamiento acá: no generarla.

## Enfoque — "el flag viaja en el DTO", backend orquesta

Ningún writer EPPlus gana DI nueva (siguen siendo formateadores puros). El flag se resuelve UNA vez
donde el service YA tiene `_ctx`/`_currentUser` (mismo patrón que `DiasAlimentoPrevioEncaset` en
`ReporteContableService.cs:183-187` y `GuiaGeneticaLookup` en `Cuadro.cs:60-61`), y viaja en el DTO
hasta el writer.

### Contable — `ColumnasHuevos` ya es data-driven (patrón de referencia real)

`ReporteContableExcelService.EscribirMovimientosHuevos` arma la hoja iterando **un array estático
de definiciones de columna** (`ColumnasHuevos`, comentario propio: "agregar una columna es agregar
una entrada, no reindexar cuatro bloques") — el MISMO patrón que `filtrar-columnas-machos.funcion.ts`
del frontend, que CLAUDE.md ya señala como "la mejor forma". Filtrar ESE array una vez al principio
del método (spread `Where` sobre una nueva propiedad `OcultaSiClasificaPorItems`) alinea automática
y permanentemente cabecera de grupo, cabecera de columna, filas de dato y fila de totales — cero
riesgo de desalinear.

1. `ReporteMovimientosHuevosDto` — nuevo `bool ClasificacionHuevoPorItems { get; init; }`.
2. `ReporteContableService.MovimientosHuevos.cs` (`ObtenerReporteMovimientosHuevosAsync`, ya tiene
   `_ctx`/`_currentUser` en la partial class) — resolver `_ctx.Companies.Where(c => c.Id ==
   _currentUser.CompanyId).Select(c => c.ClasificacionHuevoPorItems).FirstOrDefaultAsync(ct)` y
   setearlo en el DTO de retorno.
3. `ReporteContableExcelService.cs` — `ColumnasHuevos` gana `bool OcultaSiClasificaPorItems` (true
   en HVTO FÉRTIL, HVO COMERCIAL, HUEVO DESECHO, **DESCARTE**). `EscribirMovimientosHuevos`: `var
   columnas = huevos.ClasificacionHuevoPorItems ? ColumnasHuevos.Where(c =>
   !c.OcultaSiClasificaPorItems).ToArray() : ColumnasHuevos;` y reemplazar toda referencia a
   `ColumnasHuevos` dentro del método por `columnas`.
4. **Frontend** `tabla-movimientos-huevos.component.html` — sumar DESCARTE al `@if
   (!clasificacionHuevoPorItems)` existente (ya cubre HVTO FERTIL tras X18.7); ajustar colspan del
   grupo "Movimientos" (6→5) igual que ya se hizo con "Producción" (4→1).

### Técnico Producción — cabecera por índice fijo, sin array (decisión de diseño tomada)

`EscribirReporteDiario`/`EscribirCuadro` escriben celdas por **índice numérico fijo**
(`ws.Cells[row, 15]`), no por lista recorrida — remover columnas reindexando 40+ celdas a mano en
2 métodos es un refactor grande y de alto riesgo de desalinear (exactamente lo que el commit del
barrido de machos, `f7aee82`, señala como "lo delicado no eran las columnas, eran los COLSPAN").
**Decisión: mantener todos los índices intactos; con el flag ON, dejar la celda de DATOS sin
asignar (aparece vacía) en vez del valor legacy.** El encabezado se conserva (igual que ya hace el
front con "STD ROSS" al lado de una columna oculta). Menor fidelidad visual que el front (que sí
remueve la columna), pero cero riesgo de desalinear un archivo de 43 columnas fijas. Si se quiere
paridad total con el front más adelante, es un refactor aparte (columnas dinámicas) fuera de este
alcance.

5. `ReporteTecnicoProduccionLoteInfoDto` — nuevo `bool ClasificacionHuevoPorItems = false` (record
   posicional, default al final no rompe ningún call site existente).
6. `ReporteTecnicoProduccionService.cs` (ancla, sección "Helpers cross-concern") — nuevo
   `ResolverClasificacionHuevoPorItemsAsync(ct)` con la misma consulta que en Contable.
7. `ReporteTecnicoProduccionService.Diario.cs` — en `GenerarReporteSubloteAsync` y
   `GenerarReporteConsolidadoAsync` (los 2 constructores de `ReporteTecnicoProduccionCompletoDto`),
   resolver el flag y `loteInfo = loteInfo with { ClasificacionHuevoPorItems = flag };` antes del
   `return`. `GenerarReporteCuadroAsync` (Cuadro.cs) **no se toca**: ya reusa
   `reporteCompleto.LoteInfo` tal cual (línea 236) — el flag llega solo.
8. `ReporteTecnicoProduccionExcelService.cs`:
   - `EscribirReporteDiario`: con `reporte.LoteInfo.ClasificacionHuevoPorItems`, no asignar
     `ws.Cells[row,15]`/`[row,16]` (Incubable/Cargado).
   - `EscribirCuadro`: no asignar `ws.Cells[row,23]`/`[row,25]` (Huevos Incub/H.Carga). `%DESCARTE`,
     `%ACUM INCUB` y `LAA` (huevo_inc-dependientes, confirmados) **no se escriben en este Excel en
     absoluto** — nada que gatear ahí. `STD ROSS` (col 24, guía) se mantiene siempre.
9. `ReporteTecnicoProduccionController.cs` (`ExportarExcelCompleto`) — llamar a
   `GenerarReporteClasificacionHuevoComercioAsync` solo si
   `!reporteDiario.LoteInfo.ClasificacionHuevoPorItems`; si no, pasar `null` (el `if` de
   `GenerarExcelCompleto:44` ya lo maneja, cero cambio ahí).

## Invariantes

- Con el flag OFF, cero bytes distintos en ningún Excel (todas las ramas nuevas son `? :` sobre un
  bool que hoy es `false` para todas las demás empresas).
- `POSTURA`/`HUEVOS TOTALES`/`HUEVO TOTAL` (= `huevo_tot`, siempre correcto) nunca se tocan.
- Lo que sale de `traslado_huevos.TotalHuevos` (ENTRADA/VENTA/SALIDA/TRASLADO A PLANTA/V.HUEVO)
  nunca se toca — es la única fuente ajena a `huevo_inc`/las 11 legacy que sigue siendo correcta.

## Tests

xUnit nuevos en `tests/ZooSanMarino.Infrastructure.Tests/` (o carpeta equivalente que ya use
EPPlus para abrir el resultado): `GenerarExcel`/`GenerarExcelCompleto` con flag ON vs OFF, abrir el
`byte[]` resultante con `ExcelPackage` y verificar cabeceras/celdas — mismo criterio que los specs
de frontend de la sesión anterior (contar columnas, no solo texto).

## Validación

`dotnet build` (0 errores) + `dotnet test` + smoke: generar ambos Excel para Santa Reyes
(`SMOKE-SR-001`) contra el backend real, abrir el `.xlsx` resultante (unzip + inspección de la
hoja XML, o script con EPPlus) y confirmar que las columnas rotas están vacías/ausentes y que
`huevo_tot`/traslados siguen exactos.

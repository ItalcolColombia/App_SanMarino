# Seguimiento Levante/Producción — movimientos de alimento por fecha

## Objetivo

Dar en los seguimientos diarios de Levante y Producción la misma trazabilidad visible que ya existe
en Pollo Engorde: para cada fecha mostrar los ingresos de alimento, los traslados de entrada/salida y
la referencia digitada en Gestión de Inventario. Los movimientos deben verse aunque ese día todavía
no exista un registro diario.

## Enfoque arquitectónico

- Reutilizar `lote_registro_historico_unificado` como fuente única. No copiar datos al seguimiento ni
  crear columnas nuevas.
- Consultar en base de datos por empresa activa, ubicación física del lote, rango real de la fase y
  eventos `INV_INGRESO`, `INV_TRASLADO_ENTRADA` e `INV_TRASLADO_SALIDA`.
- Restringir a ítems cuyo `item_inventario.tipo_item` sea `alimento`, excluir movimientos anulados,
  devoluciones por eliminación y movimientos marcados para el próximo ciclo.
- Validar el alcance granular antes de consultar. Ante lote/LPP ajeno o ambiguo, devolver vacío o el
  error existente del módulo (fail-closed).
- Exponer un contrato pequeño, común a ambas pantallas, con fecha, tipo de movimiento, cantidad,
  alimento y referencia. La presentación y agrupación por día serán funciones puras de frontend.
- No alterar saldos, consumos, validaciones, reservas, inventario ni aritmética existente.

## Backend

### Crear

- `Application/DTOs/MovimientoAlimentoSeguimientoDto.cs`: contrato de lectura compartido.
- `Infrastructure/Services/MovimientosAlimentoSeguimientoConsultas.cs`: consulta SQL traducible y
  mapeo compartido para evitar dos fórmulas.

### Modificar

- `ISeguimientoLoteLevanteService` y `SeguimientoLoteLevanteService.Consultas`: lectura por lote de
  Levante con rango de sus seguimientos.
- `SeguimientoLoteLevanteController`: endpoint hermano del listado diario.
- `IProduccionService` y `ProduccionService.Consultas`: lectura por `loteId` o
  `lotePosturaProduccionId`, respetando los dos flujos ya admitidos.
- `ProduccionController`: endpoint hermano de `seguimiento` con los mismos identificadores y filtros
  de fecha.

## Frontend

### Crear

- `shared/models/movimiento-alimento-seguimiento.model.ts`: contrato del endpoint.
- `shared/utils/movimientos-alimento-seguimiento.funcion.ts`: agrupación estable por día,
  deduplicación de referencias y detección de fechas sin seguimiento.
- Pruebas unitarias de la función pura.

### Modificar

- Servicios de Levante y Producción: métodos HTTP tipados.
- Listas contenedoras: cargar/resetear movimientos junto con el lote y refrescarlos tras cambios.
- Tablas principales de Levante y Producción: inputs estables y columnas `Ingreso alimento`,
  `Traslado alimento` y `Referencia`; fila informativa para movimientos de fechas sin seguimiento.
- Specs de ambas tablas: contrato visual, agrupación por fecha y alineación de cabecera/cuerpo.
- Excel de Levante: incluir los tres campos porque ya exporta la grilla completa. Producción no tiene
  export de esta grilla en el componente actual, por lo que no se agrega uno nuevo fuera de alcance.

## Base de datos / SQL

- Sin DDL ni migración: se consulta la tabla espejo existente.
- Sin cambios a funciones SQL ni triggers.

## Reglas de negocio

1. Un movimiento sólo aparece para la empresa activa y la ubicación del lote seleccionado.
2. Sólo se muestran movimientos de alimento no anulados y pertenecientes al rango de la fase.
3. `Ingreso` suma/muestra entradas directas; `Traslado` distingue explícitamente `Entrada` y
   `Salida`; la referencia conserva el texto digitado.
4. Varios movimientos del mismo día se muestran sin perder alimento ni referencia.
5. Con varios seguimientos en una fecha, el resumen de movimientos se pinta una sola vez.
6. Una fecha con movimiento y sin seguimiento sigue visible como fila informativa sin acciones.
7. Los filtros de fecha de Producción se aplican también a los movimientos.

## Casos de prueba

- Un ingreso de alimento aparece en su fecha con cantidad, nombre/código y referencia.
- Traslado entrada y salida del mismo día se distinguen y no se netean visualmente.
- Varias referencias/ítems del día se conservan sin duplicados artificiales.
- Dos registros diarios del mismo día no repiten el movimiento.
- Movimiento sin seguimiento diario genera fila informativa sin botones de edición.
- Ítem no alimento, fila anulada, devolución por eliminación y próximo ciclo no aparecen.
- Lote de otra empresa o fuera del alcance devuelve vacío.
- Levante y Producción mantienen el ancho de tabla en combinaciones de flags existentes.
- `dotnet build`, tests backend pertinentes, `yarn build` y specs Angular quedan verdes.

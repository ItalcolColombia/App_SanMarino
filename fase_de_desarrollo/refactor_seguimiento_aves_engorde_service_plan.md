# Plan — Refactor SeguimientoAvesEngordeService (1884 líneas)

## Enfoque arquitectónico
Mismo patrón canónico que `MovimientoPolloEngorde` / `MovimientoAves`:
- **`partial class`** repartida por responsabilidad en `Services/SeguimientoAvesEngorde/Funciones/`.
- **Namespace plano** `ZooSanMarino.Infrastructure.Services` → DI, interfaz y comportamiento intactos.
- **Aritmética pura** (sin EF / `_ctx` / estado) → `Application/Calculos/SeguimientoAvesEngordeCalculos.cs`.
- Refactor = mover código SIN cambiar comportamiento (misma aritmética, mismos redondeos, mismos contratos).

## Archivos a crear/modificar
- **Anclas** (queda flat): `Services/SeguimientoAvesEngordeService.cs`
  - usings, campos, ctor, interfaz `: ISeguimientoAvesEngordeService`
  - constante `_docMetadataKeys`
  - helpers estáticos compartidos: `MapToDto`, `MapHistoricoUnificado`, `SanitizeContaminatedDocumentMetadata`, `CloneJsonDocument`
  - delegadores thin a Calculos: `FormatYmd`, `FormatKg`, `YmdHistoricoEfectivo`, `TryGetHistDeltaAndOrd`, `CalcularDerivados`, `CalcularSemana`, `ToKg`, `ParseMetadataItemsToKg`, `MergeMetadataWithPatch`, `MetadataYaTieneCamposKardex`, `BuscarCandidatoHistorico`
  - helpers async compartidos: `ResolverPaisIdLoteAsync`, `CalcularHembrasVivasAsync`, `CalcularRangoFechasLoteAsync`
- **`Funciones/SeguimientoAvesEngordeService.Consultas.cs`**: `GetByLoteAsync`, `GetHistoricoUnificadoPorLoteAsync`, `GetLiquidacionResumenAsync`, `QueryHistoricoUnificadoDtosAsync`, `GetByIdAsync`, `FilterAsync`, `GetResultadoAsync`
- **`Funciones/SeguimientoAvesEngordeService.Crud.cs`**: `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `BuildHistoricoConsumoAlimentoAsync`, `DevolverAvesAlInventarioAsync`
- **`Funciones/SeguimientoAvesEngordeService.SaldoAlimento.cs`**: `RecalcularSaldoAlimentoPorLoteAsync` (delega la reducción pura en Calculos)
- **`Funciones/SeguimientoAvesEngordeService.Metadata.cs`**: `BuildStockMetadataPatchAsync`, `BackfillMetadataAsync`
- **`Funciones/SeguimientoAvesEngordeService.CuadrarSaldos.cs`**: `ValidarCuadrarSaldosAsync`, `AplicarCuadrarSaldosAsync`, `ReconciliarMetadataDocumentoAsync`
- **`Application/Calculos/SeguimientoAvesEngordeCalculos.cs`** (nuevo, `static class`): `FormatYmd`, `FormatKg`, `YmdHistoricoEfectivo`, `TsHistorico`, `TsSeguimiento`, `ComputeSaldoAperturaGalponAntesPrimerSeguimiento`, `CalcularSaldoAlimentoPorSeguimiento` (reducción de saldo por seguimiento), `BuscarCandidatoHistorico`, `MetadataYaTieneCamposKardex`, struct `SaldoAlimentoEvent`.

## Reglas de negocio (preservadas 1:1)
- Aritmética de saldo de alimento (apertura + eventos, piso en 0, orden estable) idéntica.
- Gate de inventario por país (Colombia modelo B nivel granja / Ecuador-Panamá modelo B).
- Contratos DTO sin cambios.

## Casos de prueba (tarea QA siguiente)
- `SeguimientoAvesEngordeCalculos` xUnit: equivalencia de `CalcularSaldoAlimentoPorSeguimiento`, `ComputeSaldoApertura...`, `YmdHistoricoEfectivo`, `BuscarCandidatoHistorico`, `MetadataYaTieneCamposKardex`.

## Validación
- `cd backend && dotnet build` (0 errores, sin nuevas advertencias) + `dotnet test`.

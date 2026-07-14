# Plan — Migraciones Masivas: línea ENGORDE (Lotes / Seguimiento / Venta)

> STEP 1 de CLAUDE.md. Extiende el módulo `migraciones-masivas` (hoy solo Postura) con 3 tipos nuevos de la
> **línea de pollo engorde**. Estado en [`../tracker_estado.md`](../tracker_estado.md).
> Decisiones del usuario (2026-07-12): **Seguimiento = replicar todos los efectos vía servicio**;
> **Venta = Completado + descontar contador una vez (trigger escribe VENTA_AVES)**; **implementar los tres juntos**.

## Contexto validado (exploración de código, no supuestos)
- El módulo ya tiene 3 patrones probados: **Estructura** (reusa servicios en TX — `Estructura.cs`), **Histórico** (función plpgsql set-based — `Historicos.cs` + `fn_migracion_seguimiento.sql`), y el frontend **genérico** manejado por el catálogo `/tipos`.
- El **stock de aves de engorde es derivado en lectura**: `HembrasL/MachosL/Mixtas` (de `lote_ave_engorde`) − MortCaja − Σseguimiento(mort/sel/error) − Σmovimientos `Completado`. **No hay contador incremental** como en Postura → seguimiento y venta no recomputan agregados de aves.
- Un **trigger de BD** (`trg_movimiento_pollo_engorde_lote_hist`) escribe `VENTA_AVES` en `lote_registro_historico_unificado` al INSERTAR en `movimiento_pollo_engorde` (idempotente por `ON CONFLICT`). **La migración NO debe escribir ese histórico.**
- `LoteAveEngorde.CreateAsync` resuelve empresa por header (`GetEffectiveCompanyIdAsync`), crea la fila `HistorialLotePolloEngorde` "Inicio" (base del inventario de aves) y valida: granja existe+asignada al usuario, Raza+AñoTablaGenetica obligatorios y existentes en guía (`ProduccionAvicolaRaw` o `GuiaGeneticaEcuadorHeader`), galpón/núcleo coherentes.
- `SeguimientoAvesEngordeService.CreateAsync(SeguimientoLoteLevanteDto)`: valida lote existe+empresa+**no Cerrado**; registra retiro `InventarioAves` (mort+sel+error); recalcula `SaldoAlimentoKg`; **descuenta inventario de alimento SOLO si la fila trae ítems de catálogo** (Colombia modelo-B / Ecuador-Panamá inventario-gestión). Unicidad `(lote_ave_engorde_id, fecha)` por constraint SQL `uq_seg_diario_aves_engorde_lote_fecha` (fecha es TIMESTAMPTZ → normalizar). Días 1-7 pueden tener filas `origen_cruce` autogeneradas que colisionan.
- Venta = `MovimientoPolloEngorde` `TipoMovimiento='Venta'`. Vivo: create `Pendiente` (reserva) → `CompleteAsync` descuenta `HembrasL/MachosL/Mixtas`. `numero_movimiento = MPE-yyyyMMdd-{id:D6}` (post-insert). Sin `InventarioAves` (el stock de engorde vive en las columnas del lote).

## Enfoque por tipo

### 1) `LotesPolloEngorde` — patrón Estructura (reusa servicio en TX)
- Nuevo partial `Infrastructure/Services/Migracion/Funciones/MigracionService.EstructuraEngorde.cs`.
- `ProcesarLotesPolloEngordeAsync`: lee hoja `Datos`; precarga granjas (nombre→id), núcleos (`granja|codigo`), galpones (código), y combos válidos Raza+Año de la guía; valida por fila y arma `CreateLoteAveEngordeDto`; inserta reusando `_loteAveEngordeService.CreateAsync(dto)` dentro de `EjecutarImportacionAsync` (TX, all-or-nothing).
- Columnas plantilla `Datos`: **Lote** (req), **Granja** (req, dropdown), **Núcleo** (opc), **Galpón** (opc), **Raza** (req, dropdown), **Año Tabla** (req), Fecha Encaset, Hembras, Machos, Mixtas, Aves Encasetadas, Peso Inicial H, Peso Inicial M, Técnico, Lote ERP, Edad Inicial. Hoja `Referencias`: granjas, tabla núcleos-por-granja, galpones, combos Raza+Año. Hoja `Instrucciones`.
- Validaciones que rechazan fila (reporte completo): granja inexistente/no asignada, raza+año faltante o inexistente en guía, galpón/núcleo incoherente, lote duplicado en archivo (aviso; no hay unique en BD). `RequiereLote=false`.

### 2) `SeguimientoPolloEngorde` — reusa `CreateAsync` (replicar efectos)
- Nuevo partial `MigracionService.SeguimientoEngorde.cs`.
- Elegibilidad `ElegiblesEngordeAsync`: lotes `LoteAveEngorde` de la empresa, `DeletedAt=null`, `EstadoOperativoLote<>'Cerrado'`, bajo el filtro jerárquico (granja/núcleo/galpón). Devuelve `LoteElegibleDto` (loteId=LoteAveEngordeId).
- `ProcesarSeguimientoEngordeAsync(file, dryRun, companyId, ctx)`: exige `ctx.LoteId` elegible; lee `Datos`; **precarga fechas ya existentes** de `seguimiento_diario_aves_engorde` para ese lote → **omite** filas duplicadas (idempotencia); por fila arma `CreateSeguimientoLoteLevanteRequest` (LoteId=LoteAveEngordeId, FechaRegistro, mort/sel/error, `ConsumoKgHembrasDirecto/MachosDirecto`, TipoAlimento, pesos, uniformidad, Qq* Panamá, Observaciones) → `.ToDto()` → `CreateAsync`.
- **No** se envuelve en TX externa (la ruta Colombia modelo-B abre su propia TX → evita anidamiento). Se procesa fila por fila tras dry-run 0-errores; idempotencia hace re-import seguro (omite existentes).
- Columnas plantilla por lote: Fecha, Mort H, Mort M, Sel H, Sel M, Error Sexaje H, Error Sexaje M, Consumo H (kg), Consumo M (kg), Tipo Alimento, Peso H (g), Peso M (g), Uniformidad H, Uniformidad M, Observaciones. `RequiereLote=true`.
- Nota efectos: sin columna de ítem de catálogo, `CreateAsync` **no descuenta inventario de alimento** (no hay ítems) pero **sí** registra retiro `InventarioAves` y recalcula saldo — es el comportamiento seguro para histórico. Documentado en Instrucciones.
- Guarda: lote `Cerrado` → rechaza (regla viva). Colisión días 1-7 `origen_cruce` → cae en el filtro de fechas existentes (se omite).

### 3) `VentaPolloEngorde` — función plpgsql (Completado + descuento único)
- Nuevo partial `MigracionService.VentaEngorde.cs` + `backend/sql/fn_migracion_venta_engorde.sql` + migración EF que la embebe (`CREATE OR REPLACE`, idempotente; `Down` DROP).
- `fn_migracion_venta_engorde(p_company_id int, p_usuario text, p_rows jsonb) RETURNS int`: loop por fila (idempotente `NOT EXISTS` por `company+lote+fecha+cant_h+cant_m+cant_x+tipo='Venta'`): fetch granja/núcleo/galpón del `lote_ave_engorde` (scoping empresa; si no existe, omite); `INSERT` `movimiento_pollo_engorde` estado `Completado`, `fecha_procesamiento=fecha`, `tipo_movimiento='Venta'`, `numero_movimiento=gen_random_uuid()::text` temporal, `peso_neto=bruto-tara`, `promedio_peso_ave=neto/totalAves`; `RETURNING id`; `UPDATE numero_movimiento='MPE-'||to_char(fecha,'YYYYMMDD')||'-'||lpad(id,6,'0')`; `UPDATE lote_ave_engorde SET hembras_l=greatest(0,hembras_l-cant_h)`, idem machos/mixtas (espeja `CompleteAsync`). El trigger escribe `VENTA_AVES` solo (no lo tocamos).
- `ProcesarVentaEngordeAsync`: exige `ctx.LoteId` elegible (mismo `ElegiblesEngordeAsync`); lee `Datos`; valida (Fecha, cantidades ≥0, al menos una >0; peso opcional bruto≥tara); arma `filasJson`; invoca la fn con `SqlQueryRaw` (patrón `EjecutarHistoricoAsync`).
- Columnas plantilla por lote: Fecha, Cantidad H, Cantidad M, Cantidad Mixtas, Motivo, Peso Bruto (kg), Peso Tara (kg), Edad Aves, Raza, Placa, Observaciones. `RequiereLote=true`.

## Wiring común
- `Application/DTOs/Migracion/TipoMigracion.cs`: +3 valores (`LotesPolloEngorde`, `SeguimientoPolloEngorde`, `VentaPolloEngorde`) y +3 entradas de catálogo (`Fase="4"`, `Disponible=true`, `RequiereLote` según tipo).
- `MigracionService.cs` (ancla): inyectar `ILoteAveEngordeService` y `ISeguimientoAvesEngordeService` (ya registrados en DI; solo agregar al ctor).
- `MigracionService.Operaciones.cs`: extender los 3 `switch` (`GetElegibles`/`GenerarPlantilla`/`Procesar`) con los tipos nuevos.
- Frontend: `models/migracion.model.ts` (+3 al union `TipoMigracionCodigo`) y `selector-tipo-migracion.component.ts` (+3 íconos: 🐔/📋/🧾). Sin cambios de página/panel (genéricos). **Sin menú nuevo** (mismo módulo).

## Cambios de BD/SQL
- `backend/sql/fn_migracion_venta_engorde.sql` + migración EF idempotente que la embebe (convención del repo).
- Sin cambios de schema (todas las tablas existen). No se toca `__EFMigrationsHistory` a mano.

## Cálculo puro / tests
- Reusar `MigracionCalculos` (coerción fechas/números). Si hace falta un helper puro nuevo (p. ej. peso neto/promedio de venta), va a `Application/Calculos/MigracionCalculos.cs` con test en `tests/ZooSanMarino.Application.Tests/MigracionCalculosTests.cs`.
- Smoke por psql de `fn_migracion_venta_engorde` en TX con ROLLBACK (insert + idempotencia + descuento de contador + que el trigger escribió VENTA_AVES).

## Casos de prueba
- **Lotes**: granja inexistente/no asignada, raza+año inexistente en guía, galpón de otra granja, fila OK crea lote + Historial "Inicio". Duplicado de nombre en archivo (aviso).
- **Seguimiento**: fecha inválida/repetida, lote Cerrado (rechaza), fila OK inserta + retiro InventarioAves + saldo recomputado; re-import idempotente (omite fechas existentes, incluidas `origen_cruce`).
- **Venta**: fecha inválida, cantidades todas 0 (rechaza), bruto<tara (rechaza), fila OK inserta Completado + numero MPE + descuenta HembrasL/MachosL/Mixtas una vez + VENTA_AVES presente por trigger; re-import idempotente (0 duplicados, 0 doble-descuento).
- **Roles**: admin elige empresa por header; usuario normal acotado a su empresa.

## Verificación end-to-end
Back `:5002` + front `:4200`, BD `sanmarinoapplocal:5433`. Por tipo: descargar plantilla → llenar → `/validar` (ver errores) → `/importar` → verificar filas + efectos (Historial Inicio / InventarioAves retiro / movimiento_pollo_engorde + VENTA_AVES + contador descontado) → re-import idempotente. `dotnet build` 0/0 + `dotnet test` verdes + `yarn build`. `make down` al terminar.

## Riesgos / notas
- **Empresa activa en servicios reusados**: `LoteAveEngordeService` honra el header; verificar que `SeguimientoAvesEngordeService` opere sobre la empresa efectiva (usa `_current.CompanyId`) — si difiere del header para superadmin, acotar o ajustar. Verificar en implementación.
- **TX anidada** en seguimiento (Colombia): mitigado procesando sin TX externa.
- **Doble conteo venta**: mitigado — solo INSERT (trigger hace VENTA_AVES) + descuento único del contador; nada más recomputa "vendidas".
- Panamá `EsVentaMixta`: v1 mapea H→HembrasL, M→MachosL, X→Mixtas directo (sin lógica mixta especial); documentar como limitación.

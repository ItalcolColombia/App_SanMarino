# Plan — Liquidación / Indicadores Panamá (Pollo Engorde)

**Fecha:** 2026-06-01
**Autor:** Claude Code
**Estado:** En implementación

---

## 1. Objetivo

El módulo **Indicador Ecuador** (feature `indicador-ecuador`) hoy funciona solo para Ecuador
(filtros + `fn_indicadores_pollo_engorde`). Se requiere:

1. **Título dinámico por país**: mostrar "Indicador Panamá" / badge 🇵🇦 cuando la sesión activa
   es Panamá (`CountryFilterService.isPanama()`), y "Indicador Ecuador" 🇪🇨 cuando es Ecuador.
2. **Reporte de liquidación Panamá**: cuando se está en Panamá, mostrar el reporte
   "RESULTADOS DE LIQUIDACIÓN" (layout de la imagen 1) en lugar del reporte Ecuador.
   Alimentado por una nueva función SQL `fn_reporte_indicadores_panama(p_lote_id)`.
3. **Campos nuevos en el modal "Liquidar lote"** (solo Panamá): el usuario digita 6 insumos
   al cerrar/liquidar un lote:
   - Días en Granja
   - Días de Engorde
   - Aves Finales en Granja
   - Aves Beneficiadas
   - Producción Kilo en Pie
   - Metros Cuadrados

Decisiones confirmadas con el usuario:
- **Persistencia:** tabla dedicada nueva `liquidacion_lote_engorde_panama` (1 fila por lote).
- **Indicadores derivados:** los **calcula la función SQL** desde los 6 insumos + agregados de
  `seguimiento_diario_aves_engorde_panama`. El usuario solo digita los 6 insumos.

---

## 2. Contrato de datos (JSON de la plataforma origen que se está copiando)

```jsonc
{
  "liquidacion": {
    "id": 9,
    "idUsuarioRegistro": 16,
    "metrosCuadrados": 2635.00,        // INSUMO usuario
    "avesFinalGranja": 46661.00,       // INSUMO usuario
    "produccionKiloPie": 103746.29,    // INSUMO usuario
    "diasEngorde": 32,                 // INSUMO usuario
    "idLote": 98,
    "diasEnGranja": 37,                // INSUMO usuario
    "avesBeneficiada": 45228,          // INSUMO usuario
    "pesoPromedio": 2.29,              // derivado
    "mortalidadPorc": 2.73,            // derivado
    "seleccionPorc": 0.91,             // derivado
    "porcMortalidadTotal": 3.64,       // derivado
    "supervivencia": 96.36,            // derivado
    "consumoAve": 3.27,                // derivado
    "conversion": 1.43,                // derivado
    "eficienciaAmericana": 0.16,       // derivado
    "eeF": 21846608.37,                // derivado
    "eefDos": 482.22,                  // derivado
    "avesMetrosCua": 17.16,            // derivado
    "kilosMetrosCua": 39.37,           // derivado
    "productividad": 0.11,             // derivado
    "faltanteSobra": 1433.00           // derivado
  },
  "infoProductiva": {
    "consumoAlimentoTotal": 3258.000,  // SUM(qq_hembras+qq_machos+qq_mixtas) del seguimiento PA
    "totalAvesSeleccion": 438.0,       // SUM(sel_h+sel_m)
    "totalAvesMuertas": 1308.0         // SUM(mortalidad_hembras+mortalidad_machos)
  },
  "avesEncasetadas": 47969             // lote_ave_engorde.aves_encasetadas
}
```

### 2.1 Fórmulas de los derivados (VERIFICADAS contra el JSON + screenshot)

Constante: `KG_POR_QQ = 45.36` (1 quintal = 100 lb). `LB_POR_KG = 2.2046`.

```
consumoKgTotal       = consumoAlimentoTotal_qq * 45.36
pesoPromedio         = produccionKiloPie / avesBeneficiada                 → 2.29
mortalidadPorc       = totalAvesMuertas    / avesEncasetadas * 100         → 2.73
seleccionPorc        = totalAvesSeleccion  / avesEncasetadas * 100         → 0.91
porcMortalidadTotal  = mortalidadPorc + seleccionPorc                      → 3.64
supervivencia        = 100 - porcMortalidadTotal                          → 96.36
consumoAve           = consumoKgTotal / avesBeneficiada                    → 3.27
conversion           = consumoKgTotal / produccionKiloPie                  → 1.43
eeF                  = produccionKiloPie * supervivencia / (diasEngorde * conversion) * 100   → 21,846,608
eefDos               = pesoPromedio      * supervivencia / (diasEngorde * conversion) * 100   → 482.22
eficienciaAmericana  = (pesoPromedio * 2.2046) / diasEngorde               → 0.16
productividad        = eficienciaAmericana / conversion                    → 0.11
avesMetrosCua        = avesBeneficiada   / metrosCuadrados                 → 17.16
kilosMetrosCua       = produccionKiloPie / metrosCuadrados                 → 39.37
faltanteSobra        = avesFinalGranja   - avesBeneficiada                 → 1433
```
(Todas las divisiones protegidas con `CASE WHEN divisor > 0`.)

---

## 3. Backend (.NET 9 — Clean Architecture)

### 3.1 Domain
- **Nueva entidad** `LiquidacionLoteEngordePanama` (`ZooSanMarino.Domain/Entities/`):
  - `Id` (PK serial), `LoteAveEngordeId` (FK único), `MetrosCuadrados` (numeric),
    `AvesFinalGranja` (int), `AvesBeneficiada` (int), `ProduccionKiloPie` (numeric),
    `DiasEngorde` (int), `DiasEnGranja` (int),
    `RegistradoPorUserId` (text), `CreatedAt`, `UpdatedAt`.

### 3.2 Infrastructure
- **Configuración EF** `LiquidacionLoteEngordePanamaConfiguration` → tabla
  `liquidacion_lote_engorde_panama`, índice único en `lote_ave_engorde_id`, FK a `lote_ave_engorde`.
- **DbSet** en `ZooSanMarinoContext`.
- **Migración EF** `AddLiquidacionLoteEngordePanama`:
  - `Up()` crea la tabla **idempotente** (`CREATE TABLE IF NOT EXISTS` vía `migrationBuilder.Sql`)
    o `CreateTable` estándar (preferir idempotente por las reglas del proyecto).
  - En el mismo `Up()` crea **`fn_reporte_indicadores_panama(p_lote_id INT)`** vía
    `migrationBuilder.Sql(..., suppressTransaction:true)` (patrón de `AddFnIndicadoresPolloEngorde`).
  - `Down()` hace `DROP FUNCTION` + `DROP TABLE`.
- **`fn_reporte_indicadores_panama`** — `RETURNS TABLE(...)` con TODAS las columnas del JSON
  (insumos + derivados + infoProductiva + aves_encasetadas). Lee:
  - `liquidacion_lote_engorde_panama` (insumos + id + registrado_por)
  - `lote_ave_engorde` (aves_encasetadas)
  - `seguimiento_diario_aves_engorde_panama` (SUM qq_*, sel_*, mortalidad_*)
  - Aplica fórmulas §2.1. `LANGUAGE sql STABLE`. No redondea (front formatea).
- **Servicio** `ReporteIndicadorPanamaService : IReporteIndicadorPanamaService`:
  - `UpsertLiquidacionAsync(loteId, request, userId)` → inserta/actualiza la fila (EF).
  - `GetReporteAsync(loteId)` → ejecuta la fn con SQL crudo (`FromSqlRaw`/Npgsql) y mapea al DTO.

### 3.3 Application
- **DTOs** (`ZooSanMarino.Application/DTOs/`):
  - `ReporteIndicadoresPanamaDto` { LiquidacionPanama Liquidacion; InfoProductivaPanama InfoProductiva; int AvesEncasetadas }
  - `LiquidacionPanamaDto` (todos los campos del JSON `liquidacion`)
  - `InfoProductivaPanamaDto`
  - `GuardarLiquidacionPanamaRequest` { int LoteAveEngordeId; decimal MetrosCuadrados; int AvesFinalGranja; int AvesBeneficiada; decimal ProduccionKiloPie; int DiasEngorde; int DiasEnGranja; string RegistradoPorUserId }
- **Interface** `IReporteIndicadorPanamaService` (`Application/Interfaces/`).

### 3.4 API
- **Controller** `ReporteIndicadorPanamaController` (`[Route("api/[controller]")] [Authorize]`):
  - `POST api/ReporteIndicadorPanama/liquidar` → upsert de los 6 insumos.
  - `GET  api/ReporteIndicadorPanama/{loteId}` → reporte (ejecuta la fn).
- **DI** en `Program.cs`: `AddScoped<IReporteIndicadorPanamaService, ReporteIndicadorPanamaService>()`.

> El cierre del lote sigue por `LoteAveEngorde/{id}/cerrar` (sin cambios). El front, en Panamá,
> primero hace `POST liquidar` (guarda insumos) y luego `cerrar`.

---

## 4. Frontend (Angular 20 standalone)

### 4.1 Servicio
- `indicador-ecuador.service.ts`: agregar interfaces `ReporteIndicadoresPanamaDto`,
  `GuardarLiquidacionPanamaRequest` y métodos:
  - `guardarLiquidacionPanama(req)` → `POST /ReporteIndicadorPanama/liquidar`
  - `getReporteIndicadoresPanama(loteId)` → `GET /ReporteIndicadorPanama/{loteId}`

### 4.2 Título dinámico — `indicador-ecuador-list`
- Inyectar `CountryFilterService` (ya está). Getters: `esPanama`, `tituloModulo`, `paisBadge`.
- HTML: reemplazar `Indicador Ecuador` (línea 12) y badge (línea 22) por bindings.

### 4.3 Reporte Panamá
- **Nuevo componente standalone** `liquidacion-reporte-panama`
  (`indicador-ecuador/components/liquidacion-reporte-panama/`): replica el layout "RESULTADOS DE
  LIQUIDACIÓN" (imagen 1) con buen UX (tabla 2 columnas, branding Italfoods, botón imprimir).
  `@Input() data: ReporteIndicadoresPanamaDto`.
- En `indicador-ecuador-list`: cuando `esPanama` y vista Pollo Engorde, al seleccionar lote +
  Calcular, llamar `getReporteIndicadoresPanama(loteId)` y renderizar
  `<app-liquidacion-reporte-panama>`; ocultar `<app-liquidacion-reporte>` (Ecuador).

### 4.4 Modal "Liquidar lote" — `modal-liquidacion-lote-engorde`
- Inyectar `CountryFilterService`; getter `esPanama`.
- 6 inputs `[(ngModel)]` (solo visibles `*ngIf="esPanama"`): `diasEnGranja`, `diasEngorde`,
  `avesFinalGranja`, `avesBeneficiada`, `produccionKiloPie`, `metrosCuadrados`.
- Validación: requeridos (> 0) cuando Panamá antes de permitir cerrar.
- `cerrarLote()`: si Panamá → `guardarLiquidacionPanama(...)` y luego `cerrarLote` normal.
- Pre-poblar si el lote ya tiene liquidación Panamá (al reabrir/editar).

---

## 5. Pruebas / validación
- Backend: `dotnet build`; levantar BD local; `dotnet ef database update`; probar fn con el
  lote de ejemplo y comparar contra el JSON (debe dar pesoPromedio 2.29, conversion 1.43,
  eeF 21.8M, eefDos 482.22, etc.).
- Frontend: `yarn build` (compila). Validar payload del modal contra el contrato antes de enviar.
- **Apagar servicios** (`make down`) al terminar.

## 6. Riesgos / notas
- `KG_POR_QQ = 45.36`: factor quintal→kg solo para mostrar `consumo_alimento_total` en qq.
  El cálculo real usa kg directos del seguimiento (no depende de este factor).
- **CAMBIO IMPORTANTE (confirmado por el usuario):** la fuente del seguimiento NO es la tabla
  `seguimiento_diario_aves_engorde_panama` (no existe/no es fuente de verdad). Se usa la función
  `fn_seguimiento_diario_engorde(p_lote_id)` — la misma que alimenta el módulo de Seguimiento —
  que devuelve el consumo en **kg** (`consumo_dia_kg`), mortalidad y selección por día. La fn
  Panamá agrega sobre ella. Esto resolvió además el error de tabla inexistente en local.
- **A confirmar con datos reales Panamá:** `fn_seguimiento_diario_engorde` lee de
  `seguimiento_diario_aves_engorde` (tabla base). Si el seguimiento de Panamá se escribe en una
  tabla aparte, validar que la función igualmente lo incluye (el usuario indicó que la función es
  la fuente única, así que debería). Verificar con un lote Panamá real con seguimiento cargado.

## 7. Estado: IMPLEMENTADO (2026-06-01)
- Backend: entidad + EF config + DbSet + DTOs + interface + servicio + migración (tabla idempotente
  + `fn_reporte_indicadores_panama`) + controller + DI. `dotnet build` OK, migración aplicada en
  local, fn validada (todas las fórmulas directas = JSON; `eef`/`eef_dos` ±0.5% por redondeo del qq
  del sample).
- Frontend: servicio (métodos + interfaces), título/badge dinámico por país, componente
  `liquidacion-reporte-panama`, integración en `indicador-ecuador-list` (rama Panamá), modal
  `modal-liquidacion-lote-engorde` con 6 campos Panamá + validación + guardado al cerrar.
  `yarn build` OK.

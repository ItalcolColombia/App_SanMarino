# Plan — Validación y corrección de aves disponibles (lotes pollo engorde "2601")

**Fecha:** 2026-06-10 · **Módulo:** Seguimiento diario pollo engorde (Ecuador) / Disponibilidad de aves

---

## 1. Problema reportado

En lotes con nombre **2601** (ciclo cerrado), las **aves disponibles** (`GET /api/LoteReproductoraAveEngorde/{id}/aves-disponibles`) no cuadran con el **saldo de la tabla diaria** (`GET /api/SeguimientoAvesEngordeEcuador/por-lote/{id}/tabla-diaria`). Caso visto: lote 23 (granja 39, G0038) → disponibles **24 machos**, tabla diaria saldo final **8**, y el usuario reporta que en campo eran "8 de hembras".

## 2. Diagnóstico (BD local `sanmarinoapplocal`, restaurada de prod 2026-06-10)

### 2.1 Contabilidad por género del lote 23

| Concepto | Hembras | Machos | Total |
|---|---|---|---|
| Encasetadas (historial Inicio) | 11 551 | 12 634 | 24 185 |
| Bajas seguimiento (mort+sel+err) | 742 | 875 | 1 617 |
| Ventas/despachos (VENTA_AVES no anuladas) | 10 809 | 11 735 | 22 544 |
| **Saldo contable** | **0** | **24** | **24** |

- Las hembras cierran EXACTAS (11 551 − 742 − 10 809 = 0). El sobrante contable es **100 % machos (24)**.
- Existe una venta tardía: movimiento **#967, 2026-05-13, 8 machos** (reapertura "PARA TRASLADO"), posterior al corte de la tabla diaria (2026-03-18, cuando el alimento llegó a 0).

### 2.2 Causa raíz del "8" en la tabla diaria (bug en `fn_seguimiento_diario_engorde` v6)

Para lotes cerrados la fn calcula `aves_iniciales = bajas + ventas_totales` (cierre en 0 por construcción). Pero `fechas_universo` acota las filas a `fecha_max` (cierre por alimento=0), mientras `ventas_totales` NO tiene tope de fecha. Una VENTA_AVES posterior a `fecha_max` infla `inicial` sin fila que la reste → **saldo residual = ventas fuera de rango** (los "8" del lote 23 son la venta del 13-may que no se muestra, no aves vivas).

### 2.3 Lotes 2601 descuadrados (cerrados con disponibles fantasma > 0)

| Lote | Galpón | disp. H | disp. M | Total fantasma |
|---|---|---|---|---|
| 14 | G0042 | 563 | 154 | 717 |
| 20 | G0035 | 0 | 457 | 457 |
| 33 | G0050 | 0 | 290 | 290 |
| 56 | G0052 | 42 | 9 | 51 |
| 23 | G0038 | 0 | 24 | 24 |
| 28 | G0032 | 8 | 0 | 8 |
| 55 | G0051 | 0 | 4 | 4 |
| 19 | G0036 | 0 | 1 | 1 |

(Los demás 2601 cerrados — 22, 24, 31, 32 — cuadran en 0/0. Los abiertos no se tocan.)

Causa: aves nunca descargadas en ningún registro (encaset > bajas+ventas), típicamente por atribución de género imprecisa al final del ciclo (se registró mortalidad/venta del género que aún tenía saldo en papel). Un lote **Cerrado y liquidado con `aves_sobrante=0` no debe reportar disponibles**.

## 3. Enfoque arquitectónico

### A) Fix `fn_seguimiento_diario_engorde` → **v7** (migración EF + sync `backend/sql/`)
- En `fechas_universo` y `docs_por_fecha`: las **VENTA_AVES del lote NO se acotan por `fecha_min`/`fecha_max`** (solo por `fecha_encaset`). Los eventos de alimento (scope galpón) conservan el tope v5 (evita contaminación del ciclo siguiente).
- Efecto: la venta tardía aparece como fila propia y el saldo del lote cerrado cierra en **0**. Lotes sin ventas fuera de rango → salida idéntica.
- Patrón de despliegue: igual que v6 (`20260531194613`): migración con `CREATE OR REPLACE FUNCTION` idempotente; se aplica sola en deploy (`Database__RunMigrations=true`).

### B) Nueva función de validación + corrección (backend)
- **Servicio nuevo** `CorreccionAvesDisponiblesEngordeService` (Infrastructure, namespace plano `ZooSanMarino.Infrastructure.Services`).
- **`GET /api/LoteAveEngorde/aves-disponibles/validar?loteNombre=2601`** → por cada lote de la company con ese nombre: iniciales H/M, bajas H/M, ventas H/M, ventas posteriores al último seguimiento, disponibles H/M actuales (reutiliza `ILoteReproductoraAveEngordeService.GetAvesDisponiblesAsync` → misma fórmula, cero divergencia), género del sobrante y si requiere corrección.
- **`POST /api/LoteAveEngorde/aves-disponibles/corregir`** body `{ loteNombre, dryRun }` → para cada lote **Cerrado** con disponibles > 0:
  - `ajusteH = hembrasDisponibles`, `ajusteM = machosDisponibles` (fórmula vigente),
  - `hembras_l -= ajusteH`, `machos_l -= ajusteM` (clamp ≥ 0; nunca aumenta saldos),
  - auditoría: fila en `historial_lote_pollo_engorde` con `TipoRegistro='Ajuste'` (valor ya contemplado por la entidad) registrando las aves descontadas por género,
  - `dryRun=true` (default) solo reporta; transacción por corrida; **idempotente** (2ª corrida → 0 ajustes).
- Lotes **Abiertos jamás se tocan**; multi-tenant por company efectiva (patrón `GetEffectiveCompanyIdAsync`).

## 4. Archivos a crear / modificar

| Acción | Archivo |
|---|---|
| Crear | `Application/DTOs/CorreccionAvesDisponiblesEngordeDtos.cs` |
| Crear | `Application/Interfaces/ICorreccionAvesDisponiblesEngordeService.cs` |
| Crear | `Infrastructure/Services/CorreccionAvesDisponiblesEngordeService.cs` |
| Modificar | `API/Controllers/LoteAveEngordeController.cs` (2 endpoints) |
| Modificar | `API/Program.cs` (DI) |
| Modificar | `backend/sql/fn_seguimiento_diario_engorde.sql` (v7) |
| Crear | migración EF `FixFnSeguimientoEngordeVentasPostCierre` (v7) |

## 5. Reglas de negocio

1. La corrección aplica **solo** a lotes con `estado_operativo_lote='Cerrado'` y nombre exacto indicado (por ahora el caller la usará con `2601`).
2. El ajuste se calcula **por género independiente** con la fórmula vigente de disponibilidad (incluye mortCaja propio y de reproductoras, asignadas/7 días).
3. Nunca crea aves ni aumenta saldos; solo descuenta el fantasma. Deja rastro auditable.
4. El "género correcto" del sobrante se determina por contabilidad: género cuyo `iniciales − bajas − ventas > 0` (lote 23 → machos).

## 6. Casos de prueba (BD local + API local)

1. `dotnet build` sin errores ni advertencias nuevas.
2. fn v7 — lote 23: última fila = 2026-05-13 con `despacho_machos=8` y `saldo_aves=0`; lote 24 (sin ventas post-cierre): salida idéntica a v6.
3. `GET validar?loteNombre=2601` → exactamente los 8 lotes de §2.3 con `requiereCorreccion=true` y ajustes (563/154, 457 M, 290 M, 42/9, 24 M, 8 H, 4 M, 1 M).
4. `POST corregir dryRun=true` → reporta y NO modifica BD.
5. `POST corregir dryRun=false` → `hembras_l/machos_l` ajustados; `GET aves-disponibles` lote 23 → 0/0/0; tabla diaria lote 23 → saldo final 0; filas de auditoría creadas.
6. Idempotencia: segunda corrida → 0 lotes corregidos. Lotes abiertos y otros nombres intactos.

## 7. Fuera de alcance

- Reatribución de género en registros históricos de mortalidad/ventas (cambiaría seguimientos firmados; solo se documenta el hallazgo).
- Cambios de frontend.
- Aplicación en prod: la migración v7 viaja con el deploy; la corrección de datos se ejecuta llamando el endpoint en prod **previa confirmación del usuario**.

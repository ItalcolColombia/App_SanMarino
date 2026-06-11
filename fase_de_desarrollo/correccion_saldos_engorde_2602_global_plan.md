# Plan — Corrección global de saldos de aves en lotes pollo engorde (caso "2602" / lote 73)

**Fecha:** 2026-06-11 · **Antecedente:** [correccion_aves_disponibles_engorde_2601_plan.md](correccion_aves_disponibles_engorde_2601_plan.md) (lotes 2601 cerrados, ya corregidos)

---

## 1. Problema reportado

Lote **73** ("2602", granja 40, G0040, **Abierto**): `aves-disponibles` muestra **17.257 vivas** (5.211 H + 12.046 M) mientras la tabla diaria muestra saldo **1.357**.

## 2. Diagnóstico (BD local, restaurada de prod)

### 2.1 Lote 73 — causa: ventas con factura en estado `Pendiente` nunca confirmadas

| Ventas del lote 73 | Estado | H | M | Efecto en maestro |
|---|---|---|---|---|
| 3 mov. (28–30 may, docs 162/164/165) | Completado | 4.656 | 0 | ✅ descontaron (`hembras_l` 10.215 → 5.559) |
| **9 mov. (02–04 jun, facturas)** | **Pendiente** | **5.224** | **10.676** | ❌ no descuentan (por diseño: reserva) |

- Los 9 pendientes **sí** están en `lote_registro_historico_unificado` (se escriben al crear) → la tabla diaria los muestra como despachos reales (saldo 1.357 ✓ la granja físicamente los despachó: la mortalidad posterior corre sobre ~1.400 aves).
- `GetAvesDisponiblesAsync` **no resta pendientes** (a diferencia de `ResumenDisponibilidad`, que los trata como reserva) → muestra 17.257 fantasma.
- Contabilidad por género al confirmar: H = 10.215 − 348 bajas − 9.880 ventas = **−13** (clamp 0; sobreventa de género) · M = 12.483 − 437 − 10.676 = **1.370** ≈ tabla 1.357 ✓.

### 2.2 Alcance global (scan de los 75+ lotes engorde, company 3)

| Caso | Lotes | Detalle |
|---|---|---|
| **A. Pendientes sin confirmar (vencidos)** | **72** y **73** (2602, abiertos) | 72: 14 mov. (5.225 H + 10.893 M, 01–02 jun) · 73: 9 mov. (5.224 H + 10.676 M, 02–04 jun). Únicos 23 pendientes del sistema. |
| **B. Maestro no descontado por ventas Completadas** | **5** (2602, abierto) | 29 de sus 30 ventas (abr) nunca descontaron `hembras_l/machos_l`; solo la última (#915, 72 H, creada 11-may) descontó → maestro inflado en 23.630 aves (disponibles 23.791 vs tabla 161). Bug histórico de escritura, corregido en el código ~may-2026. |
| **C. Fantasma en cerrados** (caso 2601) | ninguno nuevo | Los 8 lotes 2601 ya corregidos; el "drift" que muestran = su ajuste auditado ✓ |
| Falsos positivos | 7, 30 | `historial Inicio` desactualizado (suma ≠ `aves_encasetadas`); el maestro CUADRA por conservación total → no tocar. |

## 3. Enfoque

### A) Fix de código — `GetAvesDisponiblesAsync` resta la reserva pendiente
`LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync`: restar por género las ventas `Pendiente` (no borradas, tipo Venta/Despacho/Retiro, origen = lote), igual que `ResumenDisponibilidad` (líneas 485-486). Se agregan al DTO `HembrasReservadasPendiente`/`MachosReservadasPendiente` (aditivo, no rompe contrato). → El lote 73 mostrará 0 H + 1.370 M aun antes de confirmar.

### B) Corrección de datos — extender `CorreccionAvesDisponiblesEngordeService` (v2)
`loteNombre` pasa a **opcional** (null ⇒ todos los lotes engorde de la company). `POST corregir` aplica por lote, en orden, dentro de una transacción:
1. **Confirmar pendientes vencidos** (`fecha_movimiento < hoy`): vía `IMovimientoPolloEngordeService.CompleteAsync` (misma lógica del botón de la app: estado→Completado, `fecha_procesamiento`, descuento del maestro con clamp ≥ 0; **no** duplica histórico — verificado con las 3 confirmaciones manuales del 10-jun). Pendientes de hoy/futuros se respetan como reserva.
2. **Re-sync del maestro no descontado**: recalcular esperado = `ini_historial − ventasCompletadas − ajustes_auditados` por género **solo si** el historial Inicio es confiable (`ini_h+ini_m+ini_x == aves_encasetadas`). Si no es confiable → algoritmo determinista: `sobrante_total = (hl+ml+mx) − (encaset − ventasCompTotal − ajustesTotal)`; recorrer ventas completadas de la más vieja a la más nueva acumulando H/M hasta igualar **exactamente** `sobrante_total` (la cohorte vieja que no descontó); si no cuadra exacto → **no tocar**, marcar `RevisionManual`. Nunca aumenta saldos. Auditoría `historial_lote_pollo_engorde` `TipoRegistro='Ajuste'`.
3. **Fantasma en cerrados** (lógica 2601 existente): disponibles → 0 con auditoría.

Validación cruzada esperada tras corregir: lote 5 → maestro 1.101 H / 739 M, disponibles 161 ≈ tabla 161 ✓ · lote 73 → maestro 335 H / 1.807 M, disponibles 1.370 ≈ tabla 1.357 ✓ · lote 72 → maestro = ini − ventas ✓.

### C) Migración EF de datos para PROD (ajuste post-validación, pedido del usuario)
La corrección se valida primero en local vía endpoint (dryRun → real) y luego se **empaqueta como migración EF** `20260611172121_CorreccionSaldosAvesEngorde2601y2602` para que prod quede alineada sola en el deploy (las correcciones hechas por endpoint solo existían en local). Contenido (SQL legible en `backend/sql/correccion_saldos_aves_engorde_2601_2602.sql`):
0. CHECK `ck_hlpe_tipo_registro` admite `'AjusteResync'`.
1. Confirma por ID las 23 ventas Pendientes de 72/73 (guard `estado='Pendiente'` → respeta confirmaciones manuales).
2. Re-sync lote 5 (−10.738 H / −12.892 M) con marcador `'AjusteResync'`.
3. Fantasma 2601 (8 lotes) con marcador `'Ajuste'` y guard `estado='Cerrado'`.
**Idempotente** (verificado con simulación de estado-prod + re-aplicación + rollback): en BD ya corregida es no-op.

### Refinamiento de auditoría (corrige bug de idempotencia detectado en pruebas)
Dos tipos de fila en `historial_lote_pollo_engorde`:
- `'Ajuste'` (fantasma): descuento de aves nunca descargadas → **SÍ** participa en la conservación (esperado = iniciales − ventas − ajustes fantasma).
- `'AjusteResync'`: sustituye el descuento que las ventas Completadas no hicieron → **NO** participa en la conservación (restarlo re-generaba el drift y duplicaba el ajuste, como pasó con el lote 5 en la 2ª corrida local; reparado con `backend/sql/tmp_repair_lote5_ajuste_resync.sql`).

## 4. Archivos a modificar

| Acción | Archivo |
|---|---|
| Modificar | `Application/DTOs/AvesDisponiblesDto.cs` (+2 campos reserva) |
| Modificar | `Infrastructure/Services/LoteReproductoraAveEngordeService.cs` (restar pendientes) |
| Modificar | `Application/DTOs/CorreccionAvesDisponiblesEngordeDtos.cs` (campos v2: pendientes, drift, confiabilidad, tipoDescuadre, acciones) |
| Modificar | `Application/Interfaces/ICorreccionAvesDisponiblesEngordeService.cs` (loteNombre opcional) |
| Modificar | `Infrastructure/Services/CorreccionAvesDisponiblesEngordeService.cs` (v2: 3 correcciones) |
| Modificar | `API/Controllers/LoteAveEngordeController.cs` (loteNombre opcional) |

## 5. Reglas de negocio

1. Solo se confirman pendientes **vencidos** (fecha pasada) — son despachos físicamente ejecutados (constan en el histórico/tabla diaria); los futuros siguen siendo reserva.
2. El re-sync **nunca aumenta** saldos del maestro; si la evidencia no cierra exacta → `RevisionManual`, sin tocar datos.
3. Cerrados con disponibles > 0 → 0 (regla 2601).
4. Todo ajuste de maestro deja fila `Ajuste` auditada; las confirmaciones quedan trazadas en el propio movimiento.
5. Multi-tenant: company efectiva del usuario; idempotente (2ª corrida = 0 acciones).

## 6. Casos de prueba

1. `dotnet build` 0 errores + `dotnet test` verde.
2. `GET aves-disponibles/73` (tras fix A, antes de corregir): 0 H + 1.370 M, reserva pendiente 5.224/10.676 visible.
3. `GET validar` (sin loteNombre): 72/73 → `PendientesSinConfirmar`; 5 → `MaestroNoDescontado` (ajuste 10.738 H / 12.892 M); 7/30 → sin acción (historial no confiable pero conservación OK); resto limpio.
4. `POST corregir dryRun=true` → reporta 23 confirmaciones + resync lote 5; BD intacta.
5. `POST corregir dryRun=false` → 23 movimientos Completado (con `fecha_procesamiento`); maestros: 73→335/1.807, 72→ini−ventas, 5→1.101/739; disponibles ≈ saldo tabla en los 3; filas `Ajuste` para lote 5.
6. Idempotencia y lotes sanos intactos (snapshot hl/ml antes/después).

## 7. Fuera de alcance
- Reatribución de género (sobreventa H de 13 aves del lote 73 queda documentada; el lote sigue abierto).
- Prod: deploy del código + ejecutar `POST corregir` con dryRun→OK explícito→real (igual que 2601).

# Memoria de Desarrollo — Tracker Activo

**Fase Actual:** Feature 14 — Traslado de Aves en Producción (paridad con Levante)
**Inicio:** 2026-05-25
**Archivo de plan:** `fase_de_desarrollo/14_traslado_aves_produccion_plan.md`

> Feature anterior completada: Traslado de Aves Mejorado (13) — `fase_de_desarrollo/13_traslado_aves_mejorado_plan.md`

---

## 🗄️ Base de datos

- [ ] **DB-1** `backend/sql/050_add_traslado_acumulados_lote_postura_produccion.sql` — 4 columnas en LPP, default 0
- [ ] **DB-2** Ejecutar 050 en local
- [ ] **DB-3** `backend/sql/051_add_traslado_columns_produccion_seguimiento.sql` — 4 columnas split H/M, es_traslado + flags, updated_by, índice, constraint
- [ ] **DB-4** Ejecutar 051 en local
- [ ] **DB-5** Migración EF `AddTrasladoAcumuladosLPP` (idempotente, cubre 050+051)

## 🧩 Dominio

- [ ] **DOM-1** `LotePosturaProduccion.cs`: agregar `TrasladoIngreso/SalidaHembras/Machos`
- [ ] **DOM-2** `SeguimientoProduccion.cs`: agregar splits H/M dedicados + flags traslado + updated_by/updated_at

## 🔧 EF Configurations

- [ ] **EF-1** `LotePosturaProduccionConfiguration.cs`: mapear 4 nuevas columnas con default 0
- [ ] **EF-2** `SeguimientoProduccionConfiguration.cs`: mapear nuevas columnas + índice parcial + auditoría

## 📦 DTOs

- [ ] **DTO-1** `LotePosturaProduccionDto`: añadir 4 acumulados de traslado
- [ ] **DTO-2** `SeguimientoProduccionDto`: añadir splits H/M + flags + auditoría
- [ ] **DTO-3** `CreateSeguimientoProduccionDto` y `UpdateSeguimientoProduccionDto`: revisar si requieren cambios mínimos

## 🚚 TrasladoAvesDesdeSegService — rama Producción

- [ ] **TRA-1** Reemplazar UPSERT antiguo (que usaba `MortalidadH/M` como split) por escritura en columnas dedicadas `traslado_*_h/m`
- [ ] **TRA-2** UPSERT inteligente: si ya hay SD para misma fecha+lote, acumula traslado sin sobrescribir mortalidad manual
- [ ] **TRA-3** LPP origen: `TrasladoSalidaHembras +=`, `AvesHActual -=` (idem machos)
- [ ] **TRA-4** LPP destino: `TrasladoIngresoHembras +=`, `AvesHActual +=` (idem machos)
- [ ] **TRA-5** Setear `EsTraslado=true`, `TrasladoDireccion`, contraparte, `created_by_user_id`

## 🆕 SeguimientoProduccionService.CreateAsync — MERGE inteligente

- [ ] **CRT-1** Detectar fila existente para `(LoteId, Fecha)`
- [ ] **CRT-2** Si solo tiene traslado → MERGE (añadir campos manuales sin tocar columnas traslado_*)
- [ ] **CRT-3** Si tiene manual → bloquear con mensaje específico
- [ ] **CRT-4** Descuento centralizado de aves: `AplicarDescuentoLppAsync(lpp_id, mort_h, mort_m, sel_h, sel_m, ...)` en LPP

## 🗑️ SeguimientoProduccionService.DeleteAsync con reversión

- [ ] **DEL-1** Detectar traslado por columnas dedicadas (`traslado_*` > 0)
- [ ] **DEL-2** Si traslado: revertir LPP origen + destino + contraparte (borrar si solo-traslado, limpiar si tiene manual)
- [ ] **DEL-3** Si manual: devolver mortalidad/sel a LPP del lote
- [ ] **DEL-4** Transacción atómica

## 📊 Resumen mortalidad Producción

- [ ] **RES-1** Verificar si existe endpoint análogo a Levante (`GetMortalidadResumenAsync` en `LoteService`) para fase Producción
- [ ] **RES-2** Si existe: añadir suma de mortalidad de TODAS las filas (sin filtrar por es_traslado) + traslado_ingreso/salida acumulados
- [ ] **RES-3** Si no existe: crear método nuevo

## 🛣️ API endpoints

- [ ] **API-1** `SeguimientoProduccionController`: verificar rutas existentes (Create/Update/Delete)
- [ ] **API-2** Build backend 0 errores

## 🎨 Frontend - Modal de traslado

- [ ] **FE-MOD-1** Ya soporta Producción↔Producción (Feature 13). Validar comportamiento.

## 🟡 Frontend - Tabla seguimiento producción

- [ ] **FE-TAB-1** `SeguimientoProduccionDto` (frontend): añadir nuevos campos
- [ ] **FE-TAB-2** Tabla en `lote-produccion-list`: 4 columnas dedicadas (Ing. H/M en verde, Sal. H/M en ámbar)
- [ ] **FE-TAB-3** Fila amarilla cuando hay traslado
- [ ] **FE-TAB-4** Saldo aves vivas calculado incluyendo traslados

## 🃏 Frontend - Información del lote producción

- [ ] **FE-CRD-1** 4 mini-cards de traslado (in H, in M, out H, out M)
- [ ] **FE-CRD-2** Reusar mismo patrón visual de Levante

## 🚨 Frontend - Confirmación de delete

- [ ] **FE-CONF-1** Importar `ConfirmationModalComponent` en `lote-produccion-list`
- [ ] **FE-CONF-2** Reescribir delete con mensaje detallado (traslado / manual)
- [ ] **FE-CONF-3** `armarMensajeDeleteConTrasladoProd()` con info de contraparte

## 📥 Frontend - Excel descarga

- [ ] **FE-XLS-1** Cabecera con info detallada del lote produccion (Granja, Fase Producción, Fechas, Aves vivas, etc.)
- [ ] **FE-XLS-2** 4 columnas traslado por género
- [ ] **FE-XLS-3** Columnas de auditoría (Registrado por, Actualizado por, fechas)

## 🧪 Build & Test

- [ ] **BLD-1** Build backend 0 errores
- [ ] **BLD-2** Build frontend 0 errores
- [ ] **TEST-1** Crear traslado Producción → Producción: saldo origen baja, destino sube
- [ ] **TEST-2** Seguimiento manual sobre fecha traslado producción: MERGE correctamente
- [ ] **TEST-3** Eliminar traslado producción: revierte ambos
- [ ] **TEST-4** Resumen-mortalidad producción refleja mortalidades+traslados

---

## 🏷️ Refinamiento (UX) — Prefijo de fase en acumulados de traslado

> Necesidad: en el modal de detalle del lote y en las cards de información se
> debe mostrar claramente en QUÉ FASE (Levante o Producción) ocurrió cada
> traslado. Por consistencia, las columnas de la BD también llevan el prefijo.

### Base de datos
- [x] **REN-DB-1** SQL `052_rename_traslado_columns_per_fase.sql` ejecutado en local
- [x] **REN-DB-2** Migración EF `RenameTrasladoColumnsPerFase` (idempotente con `IF EXISTS`)
- [x] **REN-DB-3** `lote_postura_levante.traslado_*` → `levante_traslado_*` (4 columnas)
- [x] **REN-DB-4** `lote_postura_produccion.traslado_*` → `produccion_traslado_*` (4 columnas)

### Backend
- [x] **REN-DOM-1** `LotePosturaLevante`: 4 props renombradas con prefijo `Levante`
- [x] **REN-DOM-2** `LotePosturaProduccion`: 4 props renombradas con prefijo `Produccion`
- [x] **REN-EF-1** EF configurations: nombres de columna actualizados
- [x] **REN-DTO-1** `LotePosturaLevanteDetailDto`: 4 campos con prefijo `Levante`
- [x] **REN-DTO-2** `LoteMortalidadResumenDto`: 8 campos (4 Levante + 4 Producción)
- [x] **REN-DTO-3** `InformacionLoteDto` (producción): 8 campos extra (4 Levante + 4 Producción)
- [x] **REN-SVC-1** `LoteService.GetMortalidadResumenAsync`: lee LPL + LPP, devuelve los 8 totales
- [x] **REN-SVC-2** `ProduccionService.GetInformacionLoteAsync`: lee LPL (vía LotePosturaLevanteId) + LPP, devuelve ambas fases
- [x] **REN-SVC-3** `TrasladoAvesDesdeSegService`: usa `LevanteTraslado*` (rama Levante) y `ProduccionTraslado*` (rama Producción)
- [x] **REN-SVC-4** `SeguimientoDiarioService.RevertirTrasladoLevanteAsync`: usa `LevanteTraslado*`
- [x] **REN-SVC-5** `SeguimientoProduccionService.RevertirTrasladoProduccionAsync`: usa `ProduccionTraslado*`
- [x] **REN-SVC-6** `LotePosturaLevanteService.ProjectToDetail`: mapea `LevanteTraslado*`
- [x] **REN-BLD** Build backend 0 errores + arranca HTTP 200

### Frontend
- [x] **REN-FE-DTO-1** `LotePosturaLevanteDto` (lote service): 4 campos con prefijo `levante`
- [x] **REN-FE-DTO-2** `LoteMortalidadResumenDto` (lote service): 8 campos (4 Levante + 4 Producción)
- [x] **REN-FE-DTO-3** `InformacionLoteDto` (produccion service): 8 campos extra
- [x] **REN-FE-DET** Modal detalle del lote base (`lote-list.component.html`): nueva sección con 2 sub-bloques (Fase Levante + Fase Producción) + saldo neto total
- [x] **REN-FE-LEV-DET** Modal detalle Levante (`selectedLoteLevante`): título y labels con etiqueta "Levante"
- [x] **REN-FE-LEV-KPI** `tabs-principal` (Levante): cards de info usan `levanteTraslado*`
- [x] **REN-FE-LEV-XLS** Excel descarga de Levante usa `levanteTraslado*` para cabecera
- [x] **REN-FE-PROD-KPI** `tabs-principal` (Producción): 4 cards diferenciadas — 🐣↘/↗ Levante + 🥚↘/↗ Producción
- [x] **REN-FE-MODAL** Modal de traslado: pill de origen suma ambas fases (Levante+Producción) como total disponible histórico
- [x] **REN-BLD-FE** Build frontend 0 errores

## Estado: COMPLETO ✅

Feature 14 — Traslado de Aves en Producción

### Resumen aplicado
- **SQL 050 + 051** ejecutados en local; **migración EF `AddTrasladoAcumuladosLPPandSeguimiento`** idempotente (cubre ambos).
- **`LotePosturaProduccion`** ahora tiene `TrasladoIngreso/SalidaHembras/Machos`.
- **`ProduccionSeguimiento`** ahora tiene los 4 splits H/M dedicados + `EsTraslado` + `TrasladoDireccion/LoteContraparteId/GranjaContraparteId` + `SelH/SelM/ErrorSexajeH/M` + `UpdatedByUserId`.
- **`TrasladoAvesDesdeSegService` (rama Producción)** reescrita para usar columnas dedicadas (no más `MortalidadH/M` como split). UPSERT inteligente acumula traslados en la misma fecha.
- **`SeguimientoProduccionService`**:
  - `CreateAsync` con MERGE inteligente sobre fila sólo-traslado existente.
  - `UpdateAsync` aplica delta de descuento sobre LPP.
  - `DeleteAsync` con reversión completa (LPP origen + destino + contraparte en transacción).
  - Helper `AplicarDescuentoLppAsync` para mantener `AvesHActual/MActual` consistente con mortalidad/sel/err.
  - Helper `RevertirTrasladoProduccionAsync` con tratamiento de contraparte vacía vs con datos manuales.
- **Frontend `produccion.service.ts`**: DTO `SeguimientoItemDto` y `InformacionLoteDto` con los nuevos campos.
- **Tabla seguimiento producción** en `tabs-principal` (lote-produccion): 4 columnas dedicadas (↘ Ing.H/M, ↗ Sal.H/M) verde/ámbar, tag SALIDA/INGRESO al inicio, fila amarilla.
- **Mini-cards en "Información del lote"** con acumulados de traslado in/out (solo visibles si hay datos).
- **Modal de confirmación de delete** en `lote-produccion-list` con mensaje detallado (contraparte, dirección, cantidades, efecto en ambos lotes).
- **Backend** 0 errores, arranca HTTP 200.
- **Frontend** 0 errores.
 
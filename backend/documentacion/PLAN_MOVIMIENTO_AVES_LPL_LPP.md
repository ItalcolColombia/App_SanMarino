# Plan: Módulo de Movimiento de Aves – LPL/LPP

## Resumen de requisitos

1. **Filtros en una sola petición** (estilo filter-data): Granja → Núcleo → Galpón → Lote.
2. **Lotes desde `lote_postura_levante` y `lote_postura_produccion`** (no solo Lote legacy).
3. **Disponibilidad de aves**: usar `aves_h_actual`, `aves_m_actual` del lote origen (LPL o LPP).
4. **Descuentos al procesar**: restar en `aves_h_actual` y `aves_m_actual` del LPL o LPP origen.
5. **Retorno al cancelar**: sumar de nuevo en `aves_h_actual` y `aves_m_actual`.
6. **No modificar seguimiento_diario** con registros artificiales de descuento; usar solo los contadores en LPL/LPP.

---

## Reglas de los contadores de aves (LPL y LPP)

| Tabla | Campos | Actualizado por | Descripción |
|-------|--------|-----------------|-------------|
| **lote_postura_levante** | `aves_h_actual`, `aves_m_actual` | Trigger/SeguimientoDiarioService (mortalidad, selección, etc.) + **movimientos** | Saldo disponible de hembras y machos. Se resta al procesar movimiento, se suma al cancelar. |
| **lote_postura_produccion** | `aves_h_actual`, `aves_m_actual` | SeguimientoDiarioService (mortalidad, selección, error sexaje) + **movimientos** | Saldo disponible en producción. Se resta al procesar, se suma al cancelar. |
| **seguimiento_diario** | registros diarios | Solo seguimiento real (producción diaria) | No se usa para registrar descuentos por movimiento; solo refleja lo ocurrido en campo. |

### Reglas para movimientos de aves

- **Al procesar movimiento** (traslado/venta): restar hembras y machos en `aves_h_actual` y `aves_m_actual` del lote origen (LPL o LPP).
- **Al cancelar movimiento**: sumar de nuevo en `aves_h_actual` y `aves_m_actual` del lote origen.
- **No modificar** `seguimiento_diario` con registros de descuento por movimiento.

---

## Situación actual

| Componente | Actual | Cambio necesario |
|------------|--------|------------------|
| **Filtros** | Cascada: `farmSvc.getAll()` → `nucleoSvc.getByGranja()` → `loteSvc.getAll()` + `produccionSvc.obtenerLotesProduccion()` | Endpoint `filter-data` único con lotes LPL + LPP |
| **Lotes** | `lote` (legacy) + lotes producción | `lote_postura_levante` y `lote_postura_produccion` |
| **Disponibilidad** | `DisponibilidadLoteService`: Lote + seguimiento_diario + movimientos | Usar `aves_h_actual`, `aves_m_actual` de LPL/LPP cuando aplique |
| **Procesar movimiento** | `AplicarDescuentoEnLevanteDiariaAvesAsync` / `AplicarDescuentoEnProduccionDiariaAvesAsync`: modifica seguimiento_diario | Descontar solo en LPL/LPP (`aves_h_actual`, `aves_m_actual`) |
| **MovimientoAves** | `LoteOrigenId`, `LoteDestinoId` (int) | Añadir `LotePosturaLevanteId`, `LotePosturaProduccionId` (opcionales) para flujo LPL/LPP |
| **Cancelar movimiento** | Devuelve aves vía seguimiento | Sumar en `aves_h_actual`, `aves_m_actual` del LPL/LPP origen |

---

## Cambios propuestos

### 1. Backend: filter-data para Movimiento de Aves

- **Endpoint**: `GET /api/MovimientoAves/filter-data` o `GET /api/Traslados/filter-data-aves`.
- **Respuesta**: granjas, núcleos, galpones y lotes que pueden ser origen de movimientos:
  - **LPL** (`lote_postura_levante`): lotes en Levante (antes semana 26), con `aves_h_actual`, `aves_m_actual`.
  - **LPP** (`lote_postura_produccion`): lotes en Producción (semana 26+), con `aves_h_actual`, `aves_m_actual`.
- Cada lote debe exponer un identificador unívoco (LPL o LPP) para disponibilidad y descuentos.

### 2. Backend: entidad MovimientoAves – soporte LPL/LPP

- Añadir `LotePosturaLevanteId` (int?) y `LotePosturaProduccionId` (int?).
- Regla: se usa uno de LoteOrigenId (legacy), LotePosturaLevanteId o LotePosturaProduccionId para el origen.
- Migración SQL para agregar columnas.

### 3. Backend: DisponibilidadLoteService – flujo LPL/LPP

- **Nuevo método**: `ObtenerDisponibilidadAvesLPLAsync(int lotePosturaLevanteId)`.
- **Nuevo método**: `ObtenerDisponibilidadAvesLPPAsync(int lotePosturaProduccionId)`.
- Disponibles = `aves_h_actual`, `aves_m_actual` del LPL o LPP.
- Mantener `ObtenerDisponibilidadLoteAsync(string loteId)` para flujo legacy.

### 4. Backend: MovimientoAvesService – descuento en LPL/LPP

- **Procesar movimiento**:
  - Si `LotePosturaLevanteId` está presente: restar en `lote_postura_levante.aves_h_actual` y `aves_m_actual`.
  - Si `LotePosturaProduccionId` está presente: restar en `lote_postura_produccion.aves_h_actual` y `aves_m_actual`.
  - No modificar `seguimiento_diario` con registros de descuento.
- **Cancelar movimiento**:
  - Sumar de nuevo en `aves_h_actual` y `aves_m_actual` del LPL o LPP origen.
- Mantener lógica legacy (LoteOrigenId + seguimiento) hasta migración completa.

### 5. Backend: endpoint de disponibilidad por LPL/LPP

- **Ejemplos**:
  - `GET /api/Traslados/lote-levante/{lotePosturaLevanteId}/disponibilidad-aves`
  - `GET /api/Traslados/lote-produccion/{lotePosturaProduccionId}/disponibilidad-aves`
- Respuesta: hembras y machos disponibles según `aves_h_actual` y `aves_m_actual`.

### 6. Frontend: filtros unificados

- Usar `filter-data` del módulo de movimiento de aves.
- Componente de filtros tipo `FiltroSelectComponent` con cascada Granja → Núcleo → Galpón → Lote (LPL/LPP).
- Una sola llamada para cargar granjas, núcleos, galpones y lotes.

### 7. Frontend: modal de movimiento

- Mostrar disponibilidad clara: hembras y machos disponibles.
- Validar cantidades frente a `aves_h_actual` y `aves_m_actual`.
- Enviar `lotePosturaLevanteId` o `lotePosturaProduccionId` según el lote seleccionado.

### 8. Flujo de movimiento de aves

```
1. Usuario selecciona lote origen (LPL o LPP) desde filter-data.
2. Se carga disponibilidad (aves_h_actual, aves_m_actual).
3. Usuario ingresa cantidades (hembras, machos) y destino.
4. Al crear movimiento:
   - Se guarda en movimiento_aves (con lote_postura_levante_id o lote_postura_produccion_id si aplica).
   - Se procesa: se resta en aves_h_actual y aves_m_actual del LPL o LPP origen.
5. No se toca seguimiento_diario.
6. Al cancelar movimiento: se suma de nuevo en aves_h_actual y aves_m_actual del origen.
```

---

## Orden sugerido de implementación

1. Migración: añadir `lote_postura_levante_id` y `lote_postura_produccion_id` a `movimiento_aves`.
2. Crear endpoint filter-data para movimiento de aves (lotes LPL + LPP).
3. Endpoints de disponibilidad por LPL y por LPP.
4. Ajustar `MovimientoAvesService`: descontar/sumar en LPL/LPP en vez de seguir modificando seguimiento_diario cuando se use LPL/LPP.
5. Actualizar DTOs (`CreateMovimientoAvesDto`, etc.) para incluir IDs de LPL/LPP.
6. Frontend: filtros unificados y envío de `lotePosturaLevanteId` o `lotePosturaProduccionId`.
7. Validar flujo completo (crear, procesar, cancelar movimientos).

---

## Notas

- Los lotes en Levante (LPL) y en Producción (LPP) pueden ser origen de movimientos.
- El destino puede seguir usando `LoteDestinoId`, `GranjaDestinoId`, etc.; el cambio principal es el origen.
- Para compatibilidad, mantener flujo con `LoteOrigenId` mientras existan lotes legacy.
- Si un lote está en LPL y pasa a LPP (semana 26), el movimiento debe referenciar el registro correcto (LPL o LPP) según el momento del movimiento.

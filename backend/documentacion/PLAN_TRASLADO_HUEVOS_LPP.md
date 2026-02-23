# Plan: Módulo Traslado de Huevos – LPP y Espejo

## Resumen de requisitos

1. **Filtros en una sola petición** (estilo filter-data): Granja → Núcleo → Galpón → Lote.
2. **Lotes desde `lote_postura_produccion`** (no desde Lote legacy).
3. **Sección inferior**: huevos disponibles del lote según seguimiento.
4. **Total de huevos** disponibles.
5. **Tabla de histórico** de huevos por categoría.
6. **Descuentos/movimientos**: restar en `espejo_huevo_produccion` (`huevo_*_dinamico`), **no** en `seguimiento_diario`.
7. **Guardar movimientos** correctamente y validar flujo.

---

## Reglas de la tabla `espejo_huevo_produccion`

| Campo | Actualizado por | Descripción |
|-------|-----------------|-------------|
| **`huevo_*_historico`** | Solo trigger de `seguimiento_diario` | Acumulado de producción (solo suma desde seguimiento). **No se modifica** con movimientos. |
| **`huevo_*_dinamico`** | Trigger de `seguimiento_diario` + **traslados/movimientos** | Saldo disponible: suma producción, resta salidas. **Es el que se mueve** (descuenta o retorna). |
| **`historico_semanal`** (JSONB) | Solo trigger de `seguimiento_diario` | Histórico por semana de producción. **No se toca** con movimientos; es solo consulta/visualización. |

### Reglas para traslados/movimientos de huevos

- **Al descontar** (procesar traslado): restar solo en `huevo_*_dinamico`.
- **Al devolver** (cancelar o eliminar traslado): sumar solo en `huevo_*_dinamico`.
- **No modificar nunca**: `huevo_*_historico` ni `historico_semanal`.

---

## Situación actual

| Componente | Actual | Cambio necesario |
|------------|--------|------------------|
| **Filtros** | `loteService.getAll()` + `farmService.getAll()` (2+ llamadas) | Endpoint `filter-data` único (granjas, núcleos, galpones, lotes LPP) |
| **Lotes** | Tabla `lote` (legacy) | `lote_postura_produccion` |
| **Disponibilidad** | `DisponibilidadLoteService`: suma seguimiento_diario, resta traslados | Usar `espejo_huevo_produccion.huevo_*_dinamico` cuando LPP |
| **Descuento traslado** | `AplicarDescuentoEnProduccionDiariaAsync`: modifica `seguimiento_diario` | Restar en `espejo_huevo_produccion` (huevo_*_dinamico) |
| **TrasladoHuevos** | `LoteId` (string) | Añadir `LotePosturaProduccionId` (int?) |

---

## Cambios propuestos

### 1. Backend: filter-data para Traslado Huevos

- **Endpoint**: `GET /api/Traslados/filter-data` o `GET /api/TrasladoHuevos/filter-data`.
- **Respuesta**: granjas, núcleos, galpones y lotes de `lote_postura_produccion` (con `estado_cierre = 'Abierta'` o similar).
- **Implementación**: servicio tipo `LoteProduccionFilterDataService` o reutilizar/recomponer desde el existente para el módulo de traslados.

### 2. Backend: entidad EspejoHuevoProduccion

- Mapear tabla `espejo_huevo_produccion` en EF Core.
- DbSet en `ZooSanMarinoContext`.
- Usar para disponibilidad y descuentos cuando se trabaje con LPP.

### 3. Backend: DisponibilidadLoteService – flujo LPP

- **Nuevo método** (o extensión): `ObtenerDisponibilidadLoteLPPAsync(int lotePosturaProduccionId)`.
- Origen de datos: `espejo_huevo_produccion` → campos `huevo_*_dinamico`.
- Disponibles = `huevo_tot_dinamico`, `huevo_limpio_dinamico`, etc.
- Mantener `ObtenerDisponibilidadLoteAsync(string loteId)` para legacy si sigue usándose.

### 4. Backend: TrasladoHuevos – LPP y espejo

- **TrasladoHuevos**: añadir `LotePosturaProduccionId` (int?).
- **CrearTrasladoHuevosDto**: aceptar `LotePosturaProduccionId` (int?) además de `LoteId`.
- **Procesar traslado**: si existe `LotePosturaProduccionId`:
  - Validar disponibilidad usando `espejo_huevo_produccion`.
  - Restar cantidades en `espejo_huevo_produccion.huevo_*_dinamico` (SQL UPDATE directo o EF si la entidad está mapeada).
- **Cancelar traslado**: sumar de nuevo las cantidades en `espejo_huevo_produccion`.
- **No modificar** `seguimiento_diario` cuando el traslado es por LPP.

### 5. Backend: endpoint de disponibilidad por LPP

- **Endpoint**: `GET /api/Traslados/lote-lpp/{lotePosturaProduccionId}/disponibilidad`.
- Retorna `HuevosDisponiblesDto` desde `espejo_huevo_produccion.huevo_*_dinamico`.
- Opcional: incluir `historico_semanal` para la tabla de histórico por semana.

### 6. Frontend: filtros unificados

- Usar `filter-data` en lugar de `getAll` de lotes y granjas.
- Componente de filtros (similar a `FiltroSelectComponent` de producción) con cascada Granja → Núcleo → Galpón → Lote (LPP).
- Una sola llamada inicial para cargar catálogos y lotes.

### 7. Frontend: sección inferior

- **Total huevos**: suma de `huevo_*_dinamico` o total devuelto por disponibilidad.
- **Disponibilidad por categoría**: Limpio, Tratado, Sucio, etc. con saldos actuales.
- **Tabla de histórico**:
  - Origen: `historico_semanal` (JSONB) de `espejo_huevo_produccion`.
  - Columnas: semana, total, incubables, limpio, tratado, etc.
  - Endpoint específico o incluir en la respuesta de disponibilidad.

### 8. Flujo de movimiento de huevos

```
1. Usuario selecciona lote LPP (filtro único).
2. Se carga disponibilidad desde espejo_huevo_produccion (huevo_*_dinamico).
3. Usuario ingresa cantidades por categoría (validación ≤ disponible).
4. Al crear traslado:
   - Se guarda en traslado_huevos (con lote_postura_produccion_id).
   - Se procesa: se resta en espejo_huevo_produccion (huevo_*_dinamico).
5. No se toca seguimiento_diario.
6. Al cancelar traslado: se suma de nuevo en espejo_huevo_produccion.
```

---

## Orden sugerido de implementación

1. Mapear `espejo_huevo_produccion` en EF (entidad + DbSet).
2. Añadir `lote_postura_produccion_id` a `traslado_huevos` (migración).
3. Crear endpoint filter-data para traslado huevos (lotes LPP).
4. Endpoint disponibilidad por LPP (desde espejo).
5. Ajustar `TrasladoHuevosService`: descuento/reversión en espejo en vez de `seguimiento_diario` cuando hay LPP.
6. Frontend: filtros unificados + disponibilidad desde espejo + tabla histórico.
7. Validar flujo completo (crear, procesar, cancelar traslados).

---

## Notas

- El trigger en `seguimiento_diario` solo actúa cuando hay `lote_postura_produccion_id`; los lotes legacy no actualizan el espejo.
- Para flujo legacy (LoteId sin LPP), se puede mantener el comportamiento actual (descuento en seguimiento_diario) hasta que todo migre a LPP.
- `historico_semanal` en espejo ya contiene datos por semana; se puede exponer tal cual para la tabla de histórico en la UI.
- **Importante**: Los movimientos (traslados/ventas) solo afectan `huevo_*_dinamico`. Los campos `huevo_*_historico` y `historico_semanal` no deben tocarse al procesar o cancelar traslados.

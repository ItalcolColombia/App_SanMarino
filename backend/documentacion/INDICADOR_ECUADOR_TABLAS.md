# Indicador Ecuador – Tablas y validación Pollo Engorde

## 1. A qué tablas apunta hoy el módulo Indicador Ecuador

El módulo **Indicador Ecuador** (endpoints `IndicadorEcuador/calcular`, `consolidado`, `lotes-cerrados`, `liquidacion-periodo`) utiliza **solo** el modelo de lotes **Levante/Producción** (tabla `lotes`), no el de pollo engorde.

| Origen del dato | Tabla / entidad | Uso |
|-----------------|-----------------|-----|
| Listado de lotes a calcular | `lotes` | Filtros por granja, núcleo, galpón, lote, fechas encaset, tipo (Levante/Producción/Reproductora). |
| Aves encasetadas / actuales | `lotes` | AvesEncasetadas, HembrasL, MachosL, Mixtas. |
| Aves sacrificadas / despacho | `movimiento_aves` | LoteOrigenId (FK a `lotes`), TipoMovimiento Venta/Despacho. |
| Mortalidad y selección | `seguimiento_lote_levante`, `seguimiento_produccion`, `lote_seguimientos` | Por LoteId (levante/producción int; reproductora string). |
| Consumo total alimento | Mismas tablas de seguimiento | ConsumoKg* por seguimiento. |
| Kg carne y edad promedio | `movimiento_aves` | PesoBruto, PesoTara, EdadAves en movimientos Venta/Despacho. |
| Fecha cierre lote | `movimiento_aves` | Fecha del último despacho/venta. |
| Metros cuadrados | `galpones` | Ancho × Largo del galpón del lote. |

**Conclusión:** El indicador actual **no** usa `lote_ave_engorde`, `lote_reproductora_ave_engorde`, `movimiento_pollo_engorde`, `seguimiento_diario_aves_engorde` ni `seguimiento_diario_lote_reproductora_aves_engorde`.

---

## 2. Modelo Pollo Engorde (Ecuador)

Para **pollo engorde** las tablas son:

| Tabla | Entidad | Relación |
|-------|---------|----------|
| `lote_ave_engorde` | LoteAveEngorde | **Lote padre** (pollo engorde directo). PK: `lote_ave_engorde_id`. |
| `lote_reproductora_ave_engorde` | LoteReproductoraAveEngorde | **Lotes reproductores** creados desde un lote padre. FK `lote_ave_engorde_id` → `lote_ave_engorde`. Un lote padre puede tener varios reproductores. |
| `movimiento_pollo_engorde` | MovimientoPolloEngorde | Despachos/ventas: origen `lote_ave_engorde_origen_id` **o** `lote_reproductora_ave_engorde_origen_id`. Incluye peso (PesoBruto, PesoTara), EdadAves, etc. |
| `seguimiento_diario_aves_engorde` | SeguimientoDiarioAvesEngorde | Mortalidad, selección, consumo por **LoteAveEngordeId**. |
| `seguimiento_diario_lote_reproductora_aves_engorde` | SeguimientoDiarioLoteReproductoraAvesEngorde | Mortalidad, selección, consumo por **LoteReproductoraAveEngordeId**. |
| `historial_lote_pollo_engorde` | HistorialLotePolloEngorde | Historial de aves por lote (inicio, entradas, salidas) para calcular aves actuales. |

Para que el **Indicador Ecuador** sirva también a **pollo engorde** hay que:

- Calcular los **mismos indicadores** (aves encasetadas, sacrificadas, mortalidad %, consumo, kg carne, conversión, etc.) pero tomando datos de:
  - **Lote padre:** `LoteAveEngorde`, `MovimientoPolloEngorde` (origen LoteAveEngorde), `SeguimientoDiarioAvesEngorde`.
  - **Cada lote reproductor:** `LoteReproductoraAveEngorde`, `MovimientoPolloEngorde` (origen LoteReproductoraAveEngorde), `SeguimientoDiarioLoteReproductoraAvesEngorde`.

---

## 3. Diseño de la vista por Lote padre y Lotes reproductores

- **Un tab “Lote padre”:** muestra el indicador calculado para el **LoteAveEngorde** seleccionado (mismos filtros: fechas, solo cerrados, etc.).
- **Tabs dinámicos:** un tab por cada **LoteReproductoraAveEngorde** asociado a ese lote padre, con el **mismo resumen de indicadores** y los **mismos filtros** (fecha desde/hasta, solo lotes cerrados, etc.).

La respuesta del backend para esta vista se modela como:

- `IndicadorPolloEngordePorLotePadreDto`:
  - `IndicadorLotePadre`: `IndicadorEcuadorDto` (lote padre).
  - `LotesReproductores`: lista de `{ Id, NombreLote, Indicador: IndicadorEcuadorDto }` (un indicador por reproductor).

Endpoint propuesto: **POST** `IndicadorEcuador/indicadores-pollo-engorde-por-lote-padre` con cuerpo que incluya `LoteAveEngordeId` y los mismos filtros que `IndicadorEcuadorRequest` (fechas, solo cerrados, etc.).

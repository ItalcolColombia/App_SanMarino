# Validación: Liquidación Técnica de Cierre de Mes de Granja – Conglomerado

Este documento verifica que el **módulo Indicador Ecuador** incluye todos los datos y fórmulas de la **LIQUIDACIÓN TÉCNICA DE CIERRE DE MES DE GRANJA : CONGLOMERADO DE TODAS LAS GRANJAS**.

---

## 1. Indicadores requeridos vs implementación

| Requerimiento | Fórmula / definición | Backend (DTO / Servicio) | Frontend (Consolidado) |
|---------------|----------------------|---------------------------|------------------------|
| **Aves encasetadas** | Suma de aves encasetadas que llegaron a los galpones con lotes activos (pueden ser de diferentes meses). | `IndicadorEcuadorDto.AvesEncasetadas`; consolidado: suma por lote. `IndicadorEcuadorConsolidadoDto.TotalAvesEncasetadas`. | ✅ Total Aves Encasetadas (card) y en tabla detalle. |
| **Aves Sacrificadas** | Sumatoria desde que inició cada lote (galpón): sumatoria de aves despachos (unidades). | `AvesSacrificadas` = movimientos Venta/Despacho/Retiro. Consolidado: `TotalAvesSacrificadas`. | ✅ Total Aves Sacrificadas. |
| **Mortalidad** | Mortalidad + selección. | `Mortalidad` = mortalidad + selección (seguimiento diario). Consolidado: `TotalMortalidad`. | ✅ Total Mortalidad (unidades) + Mortalidad (%). |
| **Mortalidad (%)** | (Mortalidad / Aves encasetadas) × 100. | `MortalidadPorcentaje`. Consolidado: `PromedioMortalidadPorcentaje` = TotalMortalidad/TotalAvesEncasetadas×100. | ✅ Mortalidad (%). |
| **Supervivencia %** | (Aves encasetadas − (Mortalidad + selección)) / Aves encasetadas × 100. | `SupervivenciaPorcentaje`. Consolidado: (TotalAvesEncasetadas − TotalMortalidad)/TotalAvesEncasetadas×100. | ✅ Supervivencia %. |
| **Consumo total alimento (kg)** | Sumatoria de alimentos dados desde que inició cada lote. | `ConsumoTotalAlimentoKg`. Consolidado: suma por lote. | ✅ Consumo total alimento (kg). |
| **Consumo ave (g)** | Consumo total alimento / aves sacrificadas (× 1000 para g). | `ConsumoAveGramos` = ConsumoTotal/avesSacrificadas×1000. Consolidado: TotalConsumo/TotalAvesSacrificadas×1000. | ✅ Consumo ave (g). |
| **Kg Carne de Pollos** | Movimientos de despacho (saque del galpón/lote). | `KgCarnePollos` = suma (PesoBruto−PesoTara) en despachos. Consolidado: suma por lote. | ✅ Kg Carne de Pollos. |
| **Peso promedio (kg)** | Kg Carne / aves sacrificadas. | `PesoPromedioKilos`. Consolidado: TotalKgCarne/TotalAvesSacrificadas. | ✅ Peso promedio (kg). |
| **Conversión** | Consumo total alimento (kg) / Kg Carne de pollo (kg). | `Conversion` = ConsumoTotal/kgCarne. Consolidado: TotalConsumo/TotalKgCarne. | ✅ Conversión. |
| **Conv. Ajustada a 2700 g** | Conversión + (2,7 − Peso promedio kg) / 4,5. Variables 2,7 y 4,5. | `ConversionAjustada2700`; `PesoAjusteVariable` (default 2,7), `DivisorAjusteVariable` (default 4,5). Fórmula: `conversion + (pesoAjuste - pesoPromedio) / divisorAjuste`. | ✅ Conv. Ajustada (2,7 / 4,5). Filtros Pollo Engorde permiten cambiar variables. |
| **Edad** | Promedio de los saques de pollo de los galpones (vivos). | `EdadPromedio` = promedio de EdadAves en movimientos despacho. Consolidado: promedio de EdadPromedio por lote. | ✅ Edad (días, promedio saques). |
| **Metros cuadrados** | Granja / galpón. | `MetrosCuadrados` = Ancho×Largo del galpón. Consolidado: suma por lote. | ✅ Metros cuadrados. |
| **Aves / m²** | Aves sacrificadas / metros cuadrados. | `AvesPorMetroCuadrado`. Consolidado: TotalAvesSacrificadas/TotalMetrosCuadrados. | ✅ Aves / m². |
| **Kg / m²** | Kg carne pollo / metros cuadrados. | `KgPorMetroCuadrado`. Consolidado: TotalKgCarne/TotalMetrosCuadrados. | ✅ Kg / m². |
| **Eficiencia Americana** | (Peso promedio kg / Conversión) × 100. | `EficienciaAmericana`. Consolidado: (PromedioPeso/PromedioConversion)×100. | ✅ Eficiencia Americana. |
| **Eficiencia Europea** | ((Peso promedio kg × % Supervivencia) / (Edad × Conversión)) × 100. | `EficienciaEuropea`. Consolidado: (PromedioPeso×Supervivencia%)/(PromedioEdad×Conversion)×100. | ✅ Eficiencia Europea. |
| **I. Productividad** | ((Peso promedio kg / Conversión) / Conversión) × 100. | `IndiceProductividad`. Consolidado: (PromedioPeso/Conversion)/Conversion×100. | ✅ I. Productividad. |
| **Ganancia / día** | (Peso promedio kg / Edad) × 1000. | `GananciaDia`. Consolidado: (PromedioPeso/PromedioEdad)×1000. | ✅ Ganancia / día. |

---

## 2. Liquidación de granjas

| Regla | Implementación |
|-------|----------------|
| **Solo se liquida cuando quedan cero aves en la granja (lote).** | Filtro `SoloLotesCerrados = true`: solo se incluyen lotes con aves actuales = 0. Cálculo: aves actuales = encasetadas − mortalidad − selección − aves sacrificadas (y para Pollo Engorde también − aves trasladadas a reproductores). |
| **Se tienen en cuenta las granjas que finalizaron lote en el período.** | Liquidación por período: `CalcularLiquidacionPeriodoAsync(fechaInicio, fechaFin, tipoPeriodo)`. Solo lotes cuya **fecha de cierre** (último despacho) está en [fechaInicio, fechaFin]. |
| **Semanal (viernes):** lotes cerrados (aves = 0) cuya fecha de cierre cae en la semana. | Endpoint `liquidacion-periodo` con `TipoPeriodo = "Semanal"`. El usuario elige rango (ej. semana al viernes). |
| **Mensual:** primeros días de la semana. Resumen de granjas. | Endpoint `liquidacion-periodo` con `TipoPeriodo = "Mensual"`. El usuario elige rango (ej. 1–30 sept). |

---

## 3. Vistas en el módulo

- **Vista General (Levante/Producción):** usa tablas `lotes`, `movimiento_aves`, seguimientos levante/producción. Incluye consolidado con todos los indicadores anteriores.
- **Vista Pollo Engorde:** usa `lote_ave_engorde`, `lote_reproductora_ave_engorde`, `movimiento_pollo_engorde`, seguimientos diarios aves engorde. Mismos indicadores y fórmulas; variables 2,7 y 4,5 configurables en filtros.
- **Liquidación por período:** pestaña/opción que llama a `IndicadorEcuador/liquidacion-periodo`; muestra solo lotes cerrados con fecha de cierre en el rango.

---

## 4. Resumen

- **Todos los indicadores** de la liquidación técnica conglomerado están en el DTO, en el servicio (por lote y consolidado) y en la tabla de resumen consolidado del frontend.
- **Liquidación solo con 0 aves** y **liquidación por período (semanal/mensual)** están implementadas en backend y expuestas en el módulo Indicador Ecuador.
- Referencia de fórmulas y alcance Pollo Engorde: [LIQUIDACION_TECNICA_POLLO_ENGORDE.md](./LIQUIDACION_TECNICA_POLLO_ENGORDE.md).

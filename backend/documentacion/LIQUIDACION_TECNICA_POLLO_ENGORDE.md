# Liquidación Técnica – Pollo Engorde (Ecuador)

**Documento específico para Lote Pollo Engorde.**  
Esta liquidación técnica es **distinta** a la que se utiliza para los lotes de Levante/Producción. Aquí solo aplican **Lote Ave Engorde** (lote padre) y **Lote Reproductora Ave Engorde**, con tablas `lote_ave_engorde`, `lote_reproductora_ave_engorde`, `movimiento_pollo_engorde` y seguimientos diarios de aves engorde.

---

## Alcance

- **Lote Pollo Engorde (padre):** tabla `lote_ave_engorde`.
- **Lote Reproductora Pollo Engorde:** tabla `lote_reproductora_ave_engorde` (cada uno asociado a un lote padre).
- **Ventas / despachos:** movimientos en `movimiento_pollo_engorde` (Venta, Despacho, Retiro) con origen en lote padre o lote reproductora.

---

## 1. Indicadores por lote (Lote padre o Lote reproductora)

**Importante:** En esta liquidación, **Sacrificio** es únicamente la pérdida en granja registrada en el **seguimiento diario** (mortalidad + selección). Las aves que salen por **venta o despacho** (movimientos) no se consideran “sacrificio”; van en **Aves vendidas / despacho**.

| Indicador | Definición / Fórmula | Origen de datos (Pollo Engorde) |
|-----------|----------------------|----------------------------------|
| **Aves encasetadas** | Suma de aves encasetadas que llegaron a los galpones con lotes activos (pueden ser de diferentes meses). | Lote padre: `AvesEncasetadas` o `HembrasL + MachosL + Mixtas`. Reproductor: `AvesInicioHembras + AvesInicioMachos + Mixtas` (o M + H + Mixtas). |
| **Aves vendidas / despacho** | Sumatoria de aves que salieron del lote por **venta, despacho o retiro** (movimientos). No es sacrificio. | Suma de cantidades en movimientos **Venta/Despacho/Retiro** de `movimiento_pollo_engorde` (origen = lote padre o lote reproductora). |
| **Mortalidad (Sacrificio en granja)** | Mortalidad + selección registradas en el seguimiento diario. **Este es el sacrificio** de la liquidación (pérdida en granja). | `seguimiento_diario_aves_engorde` (lote padre) o `seguimiento_diario_lote_reproductora_aves_engorde` (reproductora): MortalidadHembras + MortalidadMachos + SelH + SelM. |
| **Mortalidad (%)** | (Mortalidad / Aves encasetadas) × 100. | Calculado (Mortalidad = mortalidad + selección del seguimiento diario). |
| **Supervivencia %** | (Aves encasetadas − (Mortalidad + selección)) / Aves encasetadas × 100. | Calculado. |
| **Consumo total alimento (kg)** | Sumatoria de alimento dado desde que inició el lote. | Seguimientos diarios del lote (ConsumoKgHembras + ConsumoKgMachos). |
| **Consumo ave (g)** | (Consumo total alimento / Aves vendidas/despacho) × 1000. Si no hay ventas, 0. | Calculado. |
| **Kg Carne de Pollos** | Movimientos de despacho (saque del galpón/lote). | Suma de Peso neto (PesoBruto − PesoTara) en movimientos Venta/Despacho/Retiro del lote en `movimiento_pollo_engorde`. |
| **Peso promedio (kg)** | Kg Carne / Aves vendidas/despacho. | Calculado. |
| **Conversión** | Consumo total alimento (kg) / Kg Carne (kg). | Calculado. |
| **Conv. Ajustada a 2700 g** | Conversión + (2,7 − Peso promedio kg) / 4,5. | 2,7 y 4,5 son variables configurables (PesoAjusteVariable, DivisorAjusteVariable). |
| **Edad (días)** | Promedio de los saques de pollo del lote. | Promedio de **EdadAves** en movimientos de despacho del lote. |
| **Metros cuadrados** | Área del galpón del lote. | Galpón: Ancho × Largo. |
| **Aves / m²** | Aves vendidas/despacho / Metros cuadrados. | Calculado. |
| **Kg / m²** | Kg Carne / Metros cuadrados. | Calculado. |
| **Eficiencia Americana** | (Peso promedio kg / Conversión) × 100. | Calculado. |
| **Eficiencia Europea** | ((Peso promedio kg × % Supervivencia) / (Edad × Conversión)) × 100. | Calculado. |
| **I. Productividad** | ((Peso promedio kg / Conversión) / Conversión) × 100. | Calculado. |
| **Ganancia / día** | (Peso promedio kg / Edad) × 1000. | Calculado. |

---

## 2. Liquidación de granjas (Pollo Engorde)

- **Solo se liquida cuando quedan cero aves en el lote.**  
  Se consideran en la liquidación únicamente los **lotes que finalizaron** (aves actuales = 0).

- **Cierre de lote:** el lote está cerrado cuando **Aves = 0** (aves encasetadas − mortalidad − selección − aves vendidas/despacho = 0).

- **Por período:** solo entran los lotes cuya **fecha de cierre** (fecha del último despacho/venta) está dentro del rango del período.

### Frecuencias

| Frecuencia | Descripción |
|------------|-------------|
| **Semanal** | Liquidación todos los **viernes** con los lotes cerrados (aves = 0) cuya fecha de cierre cae en la semana. |
| **Mensual** | Se envía los **primeros días de la semana**. Resumen de granjas con lotes cerrados en el mes. |

---

## 3. Tablas utilizadas (solo Pollo Engorde)

| Tabla | Uso |
|-------|-----|
| `lote_ave_engorde` | Lote padre: aves encasetadas, galpón, granja. |
| `lote_reproductora_ave_engorde` | Lotes reproductores: aves inicio, galpón vía lote padre. |
| `movimiento_pollo_engorde` | Ventas/despachos: unidades, kg carne (PesoBruto − PesoTara), EdadAves. |
| `seguimiento_diario_aves_engorde` | **Sacrificio en granja** (mortalidad + selección), consumo por lote padre. |
| `seguimiento_diario_lote_reproductora_aves_engorde` | **Sacrificio en granja** (mortalidad + selección), consumo por lote reproductora. |
| `galpones` | Metros cuadrados (Ancho × Largo). |

**Nota:** La liquidación técnica para lotes de **Levante/Producción** usa otras tablas (`lotes`, `movimiento_aves`, `seguimiento_lote_levante`, etc.) y se documenta por separado.

---

## 4. Implementación API (Ecuador)

Para el indicador de **liquidación técnica por lote aves de engorde** (pantalla "Liquidación técnica" dentro del módulo *Seguimiento diario pollo de engorde*) se implementó una API específica que usa `lote_ave_engorde` y `seguimiento_diario_aves_engorde`:

- **Base URL:** `api/LiquidacionTecnicaEcuador`
- **Parámetro:** `loteAveEngordeId` (ID del lote en `lote_ave_engorde`)

Detalle de implementación, endpoints y uso desde el frontend: ver **DESARROLLO_MODULO_AVES_ENGORDE_ECUADOR.md** (sección 3).

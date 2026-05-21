# Feature 11 — Reporte de Liquidación Técnica (Estilo Excel)

**Fecha:** 2026-05-21  
**Módulo:** Indicador Ecuador / Pollo Engorde

## Objetivo
Generar una ficha técnica imprimible por lote, fiel al diseño de "LIQUIDACION HOJA EXCEL", con soporte masivo (una hoja por lote) y descarga/impresión vía `window.print()`.

## Archivos a crear / modificar

### Backend
| Archivo | Cambio |
|---|---|
| `DTOs/IndicadorEcuadorDto.cs` | Agregar `DateTime? FechaAlistamiento` al record |
| `Services/IndicadorEcuadorService.cs` | Pasar `lote.FechaAlistamiento` en `CalcularIndicadorLoteAveEngordeAsync` |

### Frontend — Service
| Archivo | Cambio |
|---|---|
| `indicador-ecuador.service.ts` | Agregar `fechaAlistamiento?: string \| null` a `IndicadorEcuadorDto`; agregar `tipoFiltroLotes?` a `LiquidacionPolloEngordeReporteRequest` |

### Frontend — Nuevo componente
```
indicador-ecuador/components/liquidacion-reporte/
  liquidacion-reporte.component.ts
  liquidacion-reporte.component.html
  liquidacion-reporte.component.scss
```

### Frontend — Página existente
| Archivo | Cambio |
|---|---|
| `indicador-ecuador-list.component.ts` | Importar nuevo componente; agregar `showReporte`; pasar `tipoFiltroLotes` en 3 llamadas |
| `indicador-ecuador-list.component.html` | Botón "Ver Reporte" + `<app-liquidacion-reporte>` |

## Estructura de la ficha por lote

```
╔══════════════════════════════════════════╗
║     LIQUIDACION HOJA EXCEL               ║  ← fondo azul, texto blanco
╠══════════════════════════════════════════╣
║ ECU - ITALCOL S.A.                        ║
║ GRANJA          │ {granjaNombre}           ║
║ LOTE            │ {galponNombre} / {lote}  ║
║─────────────────┼──────────────────────── ║
║ FECHA ALIST.    │ {fechaAlistamiento}      ║  ← label rojo
║ FECHA ENCASET.  │ {fechaInicioLote}        ║  ← label rojo
║ FECHA LIQUID.   │ {fechaCierreLote}        ║  ← label rojo
╠══════════════════════════════════════════╣
║            ANALISIS TECNICO              ║  ← fondo azul, texto blanco
╠═══════════════════════╦═════════╦════════╣
║ Aves encasetadas      ║ 44301   ║        ║
║ Aves sacrificadas     ║ 42532   ║        ║
║ Mortalidad (unidades) ║ 1772    ║        ║
║ Mortalidad (%)        ║  4.00   ║        ║
║ Merma (unidades)      ║  0      ║        ║
║ Merma (%)             ║  0.00   ║        ║
║ Ajuste en Aves        ║  calc   ║        ║
║ Porcentaje de ajuste  ║  0.00   ║        ║
║ Supervivencia (%)     ║ 96.00   ║        ║
║ Consumo total (Kg)    ║206340   ║        ║
║ Producción kilo pie   ║135090   ║        ║
║ Merma Planta (Kg)     ║  0.00   ║        ║  placeholder
║ Merma Proceso (Kg)    ║  0.00   ║        ║  placeholder
║ Total kg despachados  ║135080   ║        ║
║ Consumo ave (Kg)      ║  4.85   ║        ║
║ Peso promedio (Kg)    ║  3.18   ║        ║
║ Conversión            ║  1.53   ║        ║
║ Conv. Ajustada        ║  1.30   ║        ║
║ Eficiencia Americana  ║207.95   ║        ║
║ Eficiencia Europea    ║  XXX    ║        ║
║ Días de engorde       ║  51     ║        ║
║ Productividad (IEEP)  ║136.14   ║        ║
║ Edad Ponderada        ║ 43.41   ║        ║
║ Ganancia / Día (g)    ║ 81.00   ║        ║
╚═══════════════════════╩═════════╩════════╝
```

## Lógica de cálculo para nuevos campos
- **Ajuste en Aves** = `avesEncasetadas - mortalidad - avesSacrificadas` (residual)
- **Días de engorde** = días entre `fechaInicioLote` y `fechaCierreLote`
- **Consumo ave (Kg)** = `consumoAveGramos / 1000`
- **Mermas** = placeholder 0.00
- **Total kg despachados** = `kgCarnePollos` (igual que Producción, mermas en 0)

## CSS @media print
- `@page { size: A4; margin: 1cm; }`
- `.reporte-lote { page-break-after: always; }`
- Ocultar nav, filtros, botones al imprimir

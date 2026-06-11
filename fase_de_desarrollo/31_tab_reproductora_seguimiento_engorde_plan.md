# Plan — Tab «R. Reproductora» en Seguimiento Diario Pollo Engorde

## Objetivo
Agregar un nuevo tab en el módulo de seguimiento diario pollo engorde (`tabs-principal-engorde`), al lado de **📊 Seguimiento**, llamado **🐣 R. Reproductora**, que reproduzca la sección «LOTES DE POLLITOS PRIMERA SEMANA» del Excel de granja:

1. **Resumen de lotes reproductora** (cabecera): LOTE (H-34/M-34/H-32/M-32) · CANTIDAD · PESO LLEGADA · Cantidad×Peso · Peso 7 días · Cantidad×7 días · VPI + fila de totales (56,355 · 2,222,976.08).
2. **Bloques primera semana por sexo** (un bloque H y uno M por cada lote reproductora): días 1–7 con Consumo tabla (gr) · QQ tabla · QQ real · Grs./ave · Peso · Ganancia · Conv. · Muertos Norm./Sel. · Saldos · %Norm · %Sel + fila Promedio; encabezado con Fecha encaset, # Pollitos y Peso llegada.

## Validación previa (cruce ya verificado — 2026-06-10)
El export de la app (`Seguimiento_engorde_Lote 31`) cuadra **exacto** contra los bloques del Excel original: mort H/M y sel H/M por edad ✓, consumos kg↔qq (45.36) ✓, agua (toma lote 34, no suma) ✓, pesos 56–164 g ✓, saldo final 56,067 ✓. Diferencias vs la hoja principal del Excel = las 2 discrepancias internas del Excel ya documentadas (plan 30). ⚠️ Pendiente: fecha mostrada corre 1 día antes (edad 1 = 28/05 en la app vs 29/05 en el Excel) — revisar manejo timezone (timestamptz UTC-midnight mostrado en hora local). Verificar dato en BD antes de corregir visualización.

## Arquitectura (CLEAN CODE obligatorio — patrón movimientos-pollo-engorde)

### Sin cambios de backend
Todo existe: lotes reproductora (`LoteReproductoraAveEngordeService.getAll(loteAveEngordeId)`), seguimientos 7 días (`SeguimientoDiarioLoteReproductoraController.GetByLoteReproductora`), guía genética (servicio guia-genetica-ecuador por raza/año del lote). El tab es 100 % frontend, solo lectura.

### Frontend — `features/aves-engorde/`
```
models/
└── reproductora-primera-semana.model.ts   # NUEVO: tipos compartidos
    · ResumenReproductoraFila { lote, codigo, cantidad, pesoLlegada, cantidadXPeso,
                                peso7Dias, cantidadX7Dias, vpi }
    · BloquePrimeraSemana { titulo ('H-34'), sexo, fechaEncaset, pollitos, pesoLlegada,
                            filas: FilaDiaBloque[], promedio }
    · FilaDiaBloque { dia, consumoTablaGr, qqTabla, qqReal, grsAve, pesoG, gananciaG,
                      conv, muertosNorm, muertosSel, saldo, pctNorm, pctSel }
funciones/
├── construir-bloques-reproductora.funcion.ts   # NUEVO — PURA
│     (lotesReproductora, seguimientosPorLote, guiaTabla?) → BloquePrimeraSemana[]
│     · separa cada lote reproductora en bloque H y bloque M
│     · saldo día d = avesInicio − Σ(mort+sel+err) días ≤ d (por sexo)
│     · qqReal = consumoKg / 45.36 · grsAve = consumoKg×1000 / saldo inicio día
│     · ganancia = peso_d − peso_(d−1) (día 1: peso − pesoLlegada)
│     · conv = grsAve / ganancia del día (criterio Excel; día 1 ⚠ confirmar fórmula)
│     · qqTabla = grTabla × aves vivas / 1000 / 45.36 (si hay guía cargada)
└── calcular-resumen-vpi.funcion.ts             # NUEVO — PURA
      (bloques) → { filas: ResumenReproductoraFila[], totales }
      · cantidadXPeso = cantidad × pesoLlegada
      · peso7Dias = pesoProm del registro edad 7 (hoy 0/null → 0, como el Excel)
      · cantidadX7Dias = cantidad viva día 7 × peso7Dias
      · VPI fila = peso7Dias / pesoLlegada · VPI total = Σ(cantX7)/Σ(cantXPeso)
        ⚠ fórmula derivada del Excel (col BD/AZ) — CONFIRMAR con negocio
components/
└── tab-reproductora-engorde/                   # NUEVO — orquestador delgado
    ├── tab-reproductora-engorde.component.ts   # @Input() loteAveEngordeId, raza, anoTabla
    ├── tab-reproductora-engorde.component.html # card resumen + card por bloque (estilo Excel)
    └── tab-reproductora-engorde.component.scss # tablas verdes paleta Italfoods, overflow-x
```

### Integración en `tabs-principal-engorde`
- `activeTab: 'general' | 'reproductora' | 'indicadores' | 'grafica'` (+ `onTabChange`).
- Botón `🐣 R. Reproductora` inmediatamente después de `📊 Seguimiento` (tabs-principal-engorde.component.html:14).
- `<app-tab-reproductora-engorde [loteAveEngordeId]="..." ...>` en panel nuevo; carga lazy al activar el tab (no pegarle a la API si nunca se abre).
- Empty state: lote sin lotes reproductora → mensaje «Este lote no tiene lotes reproductora asociados».

## Reglas de negocio / mapeo de columnas Excel → app
| Columna Excel | Fuente app |
|---|---|
| # Pollitos / CANTIDAD | `avesInicioHembras` / `avesInicioMachos` |
| PESO LLEGADA | `pesoInicialH` / `pesoInicialM` (g) |
| QQ REAL | `consumoKgHembras` / `consumoKgMachos` ÷ 45.36 |
| Muertos Norm. | `mortalidadHembras` / `mortalidadMachos` |
| Muertos Sel. | `selH` / `selM` |
| Peso | `pesoPromHembras` / `pesoPromMachos` (g) |
| Saldos / %Norm / %Sel / Grs. / Ganancia / Conv. | calculados (funciones puras) |
| Consumo tabla / QQ TABLA | guía genética raza+año del lote (si está cargada; si no, columna oculta) |
| Fecha | `fechaEncasetamiento` del lote reproductora |
| Incubadora / %Ombligo / Lesiones «Mortalidad 5 día» | **FASE 2** — no existen campos hoy (módulo lesiones/calidad pollito) |

## Casos de prueba (con el Lote 31 ya cargado)
1. `cd frontend && yarn build` sin errores.
2. Resumen: 4 filas (H-34 14,595 · M-34 14,096 · H-32 13,995 · M-32 13,669), total 56,355, Cantidad×Peso 2,222,976.08, VPI 0 (sin pesaje día 7).
3. Bloque H-34 día 1: QQ real 3.00 · Grs. 9.33≈9 · saldo 14,585 · %Norm 0.07 % · ganancia 17.37.
4. Bloque M-32 día 5: QQ 8.00 · muertos 13 · sel 0 · saldo 13,607.
5. Filas Promedio: H-34 → 39 qq, 56 norm, 10 sel (idem Excel).
6. Lote sin reproductoras → empty state sin errores de consola.
7. Tab Seguimiento/Indicadores/Gráficas siguen funcionando igual (sin regresión de CD).

## Fuera de alcance (fase 2)
- Lesiones «Mortalidad 5 día #/%» por bloque (módulo necropsia/lesiones).
- Calidad de pollito (incubadora, %ombligo, %pico, %pata, %tarso).
- Export Excel del tab.
- Fix del corrimiento de fecha (–1 día) en la visualización del cruce: se trata como issue aparte tras verificar el dato en BD.

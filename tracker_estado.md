# Estado — Decimales en pesos/uniformidad del modal «Nuevo Lote de Engorde»

Plan: [29_decimales_pesos_uniformidad_lote_engorde_plan.md](./fase_de_desarrollo/29_decimales_pesos_uniformidad_lote_engorde_plan.md)

## Diagnóstico
- [x] Localizar el campo: directiva `ThousandSeparatorDirective` (`Math.round` + `maximumFractionDigits: 0`)
- [x] Confirmar que la directiva es **compartida** entre `lote` (quiere enteros) y `lote-engorde`
- [x] Cruzar con el contrato backend: `pesoInicialH/M`, `pesoMixto`, `unifH/M` son `double?`; el resto `int?`

## Implementación
- [x] `ThousandSeparatorDirective`: `@Input() decimals = 0` opt-in, retrocompatible (import `Input`)
- [x] `lote-engorde` HTML: `[decimals]="2"` en `pesoMixto`, `pesoInicialH`, `pesoInicialM`, `unifH`, `unifM`
- [x] `cd frontend && yarn build` sin errores

## Notas
- Comportamiento por defecto (`decimals=0`) **idéntico** al actual → módulo `lote` intacto.
- Conteos de aves (`hembrasL`, `machosL`, `mixtas`, `mortCajaH/M`, `avesEncasetadas`) siguen enteros.
- Precisión = 2 decimales (ajustable por campo vía `[decimals]`).

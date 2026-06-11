# funciones/ — aves-engorde

Convención del repo (CLAUDE.md): **una función grande / de botón por archivo**, PURA
(sin `this`, sin DI, sin services/toasts/estado). El componente orquestador junta los
datos, llama la función y maneja HTTP/UI. Tipos compartidos → `../models/`.

| Archivo | Qué hace |
|---|---|
| `construir-bloques-reproductora.funcion.ts` | Arma los bloques «primera semana» (un bloque Hembras y uno Machos por lote reproductora) desde los seguimientos diarios: edad 1–7, saldos, qq reales (45.36 kg/qq), grs/ave, ganancia, conversión, %Norm/%Sel y fila Total. |
| `calcular-resumen-vpi.funcion.ts` | Resumen superior del tab: Cantidad, Peso llegada, Cantidad×Peso, Peso 7 días, Cantidad×7 días y VPI (peso 7 días ÷ peso llegada; total = Σ cant×7d ÷ Σ cant×peso). Fórmulas confirmadas con negocio 2026-06-10. |

Notas de reutilización:
- Las usa `components/tab-reproductora-engorde/`, compartido por los módulos
  **aves-engorde** (Ecuador) y **aves-engorde-panama** — no duplicar lógica en el fork Panamá.
- Los tipos de entrada son **estructurales** (`*Like` en `models/reproductora-primera-semana.model.ts`):
  aceptan los DTOs reales sin acoplarse a los services.
- Guía genética (Consumo tabla / QQ tabla): campos ya mapeados en el modelo
  (`consumoTablaGr`, `qqTabla`) pero en `null` y columnas ocultas — fase 2.
- Conv. del día 1 divide por **peso de llegada** (los demás días por la ganancia) —
  criterio del Excel confirmado por negocio.

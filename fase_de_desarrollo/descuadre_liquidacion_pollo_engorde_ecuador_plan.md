# Plan — Corrección descuadre Liquidación Pollo Engorde Ecuador (lote 2601 / granja 38)

Diagnóstico completo: [backend/documentacion/ANALISIS_DESCUADRE_LIQUIDACION_POLLO_ENGORDE_ECUADOR.md](../backend/documentacion/ANALISIS_DESCUADRE_LIQUIDACION_POLLO_ENGORDE_ECUADOR.md)

## Contexto / reglas de negocio
- La **merma se digita UNA vez por corrida** y queda en un solo lote (lote 19); aplica a toda la corrida 2601. **No se duplica.**
- El **TOTAL** del reporte lo calcula el **front** (`liquidacionTotales()`), no el backend.
- El Excel del cliente es la referencia correcta. Diferencia clave marcada: "Producción kilo en pie" 251.052 vs sistema 245.129 = **5.923 kg**.

## Causas (probadas por simulación)
- **C1 (DATO):** mov `id 102` (lote 20, `MPE-20260401-000102`, 3.192 aves) sin ningún peso ⇒ 0 kg = los 5.923 faltantes. En cascada descuadra peso promedio, conversión, eficiencia americana, productividad.
- **C2 (CÓDIGO front):** el total "kilos despachados a cliente" se acumula solo dentro del `if (merma != null)`; como la merma única va en el lote 19, el lote 20 queda excluido del total.

## Enfoque arquitectónico
- **A (C2)** — Frontend Angular, archivo único:
  `frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts` → método `liquidacionTotales()`.
  - El total a cliente debe ser de **corrida**: `prodKg - mermaKg` (prodKg ya suma todos los lotes; mermaKg = merma única).
  - Quitar el acumulador por-lote `totCliente` (queda muerto).
  - `totalKilosDespachadosCliente: hayMerma ? (prodKg - mermaKg) : null`.
  - Sin cambios en backend ni en `fn_indicadores_pollo_engorde` (la fn ya entrega `produccion_kilo_en_pie` por lote sin depender de la merma).
- **B (C1)** — Dato en la **copia LOCAL** (`localhost:5433/sanmarinoapplocal`), NO prod:
  `UPDATE movimiento_pollo_engorde SET peso_bruto/peso_tara/peso_neto` del `id=102` para que el neto sea 5.923 kg (provisional, a confirmar contra tiquete físico de placa MAA-2902).
  - Validar con `fn_indicadores_pollo_engorde(20,…)` y recomputar el total de la corrida vs Excel.

## Cambios de BD/SQL
- Solo **B**, y **solo en local** (no prod, no migración): un `UPDATE` puntual al movimiento 102. Prod queda intacto hasta confirmar el tiquete real.

## Casos de prueba / validación
1. `yarn build` del front sin errores tras el cambio A.
2. Simulación A (con datos actuales): total a cliente = 245.129 − 462,66 = **244.666**.
3. Tras B (mov 102 = 5.923 neto): `fn_indicadores_pollo_engorde(20)` → kg_carne lote 20 = 117.363,83; peso prom ≈ 2,79.
4. Total corrida tras A+B = **251.052** producción / **250.590** a cliente / peso 2,77 / conversión 1,81 / ef. americana 153,22 / productividad 84,75 → **cuadra con Excel**.

## Fuera de alcance (sólo se documenta, no se cambia ahora)
- Denominador de Merma % (encasetadas vs sacrificadas), Días de engorde y Edad ponderada (diferencias de definición).
- Endurecer filtro `estado NOT IN ('Cancelado','Anulado')` en la fn (riesgo latente).
- Aplicar el dato real en **prod** (espera confirmación del tiquete físico).

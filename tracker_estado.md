# Estado — Tab «R. Reproductora» en Seguimiento Diario Pollo Engorde

Plan: [31_tab_reproductora_seguimiento_engorde_plan.md](./fase_de_desarrollo/31_tab_reproductora_seguimiento_engorde_plan.md)
(Antecedente: [30_carga_prueba_lote31_engorde_reproductoras_plan.md](./fase_de_desarrollo/30_carga_prueba_lote31_engorde_reproductoras_plan.md))

## Validación del cruce (Excel original vs export de la app) — COMPLETA ✅
- [x] Mortalidad H/M, selección H/M, consumos kg↔qq (45.36), agua, pesos, saldo final 56,067: exactos vs bloques del Excel
- [x] Diferencias vs hoja principal del Excel = discrepancias internas del propio Excel (14 vs 15 qq día 1 · sel 16 vs 15 día 4)
- [ ] ⚠️ Fecha corrida −1 día en la app (edad 1 = 28/05 vs Excel 29/05) → issue aparte: verificar dato en BD (timezone)

## Decisiones de negocio confirmadas (2026-06-10)
- [x] VPI = peso 7 días ÷ peso llegada · VPI total = Σ(cant×7días) ÷ Σ(cant×peso) ✔ usuario
- [x] Conv. día 1 divide por **peso de llegada**; días 2+ por la ganancia ✔ usuario
- [x] Guía genética (Consumo tabla / QQ tabla): **mapeada pero oculta** (`mostrarGuiaGenetica=false`, campos en el modelo) ✔ usuario

## Implementación — COMPLETA ✅
- [x] `aves-engorde/models/reproductora-primera-semana.model.ts` (tipos estructurales + constante 45.36)
- [x] `aves-engorde/funciones/construir-bloques-reproductora.funcion.ts` (pura: bloques H/M, edad por fecha UTC, saldos, qq, grs, conv, %)
- [x] `aves-engorde/funciones/calcular-resumen-vpi.funcion.ts` (pura: resumen + VPI)
- [x] `aves-engorde/funciones/README.md` (convención + nota de reutilización Ecuador/Panamá)
- [x] `aves-engorde/components/tab-reproductora-engorde/` (orquestador delgado: ts + html + scss, estados carga/error/vacío)
- [x] Integrado en `aves-engorde/pages/tabs-principal-engorde` (botón 🐣 R. Reproductora + panel, junto a Seguimiento)
- [x] Integrado en `aves-engorde-panama/pages/tabs-principal-engorde` (mismo componente compartido)
- [x] `yarn build` sin errores (solo warning preexistente de bundle budget)

## Validación manual pendiente (usuario, con Lote 31 cargado)
- [ ] Resumen: H-34 14,595 · M-34 14,096 · H-32 13,995 · M-32 13,669 · total 56,355 · Cant×Peso 2,222,976.08 · VPI 0
- [ ] Bloque H-34 día 1: QQ 3.00 · muertos 10 · saldo 14,585 · %Norm 0.07 % · Conv. 0.24
- [ ] Bloque M-32 día 5: QQ 8.00 · muertos 13 · sel 0 · saldo 13,607
- [ ] Fila Total H-34: 39 qq · 56 norm · 10 sel · saldo final 14,529
- [ ] Día 7 (sin datos reales): filas con «—» y saldo arrastrado
- [ ] Tabs Seguimiento/Indicadores/Gráficas sin regresión

## Fase 2 (pendiente de priorizar)
- [ ] Conectar guía genética (poner `mostrarGuiaGenetica=true` + llenar `consumoTablaGr`/`qqTabla`)
- [ ] Lesiones «Mortalidad 5 día» por bloque · calidad de pollito (%ombligo, incubadora)
- [ ] Export Excel del tab
- [ ] Fix corrimiento −1 día en visualización de fechas del cruce

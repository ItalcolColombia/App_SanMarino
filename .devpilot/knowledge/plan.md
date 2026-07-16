# Plan propuesto: Matriz Verenice rev 6-jul-26 — Postura Colombia (validación + corrección)

**Objetivo:** corregir todos los módulos de la línea POSTURA Colombia (seguimiento diario levante, seguimiento diario producción, movimiento de aves, movimiento de huevos, reportes, indicadores/gráficas Sanmarino levante+producción) según la matriz de requerimientos de la líder funcional Verenice (rev 6-jul-26, 10 REQ + hoja FÓRMULAS ~140 parámetros), con validación previa de qué ya está resuelto en main.

**Plan detallado (fuente de verdad):** `fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md` · **Tracker:** `tracker_estado.md`

## Estrategia
Validación hecha (2026-07-16) con investigación de código multi-agente (evidencia file:line) + verificación de datos en BD local (copia de prod). Resultado: 7 ítems ya RESUELTOS en main (fixes de julio, desplegados — los screenshots de Verenice eran del 12-jun, pre-fix), 18 con FALLA vigente, 17 PARCIALES y 4 alcances nuevos. Orden de ataque: Fase 0 data-fix (lotes 116/117 con encaset futuro, filas de traslado mal fechadas, año de guía K345 2023→2026 — desbloquea la mayoría de síntomas) → Fase 1 hotfixes front (columnas corridas thead/tbody en Indicadores, % consumo, TIPO ITEM, labels) → Fase 2 fn_indicadores_levante_postura (consumo H/M, acumulados sobre aves iniciales, defensas) → Fase 3 semana/edad/etapa + validaciones anti-corrupción (anti-futuro, consumo vs saldo por sexo, filter-data producción con fechaEncaset, semana 25, etapa 26-33/34-50/>50) → Fase 4 traslado (default de fecha) → Fase 5 reporte semanal por sexo + barrido conversión alimenticia + selector H/M → Fase 6 transversales (vista Power BI a migración EF, enforcement edición, Lote General A+B+C, %Retiro, autosave borrador, carga masiva lotes postura) → Fase 7 fórmulas nuevas (Kcal/Proteína, % clasificación huevo vs guía, IP/Masa/PesoM-H, embriodiagnosis — bloqueadas por insumos de la líder).

## Causas raíz descubiertas (conocimiento clave)
1. Indicadores levante: `<tbody>` desalineado del `<thead>` en tabla-lista-indicadores (mortalidad pintada antes que peso/uniformidad) → 9 columnas corridas; el backend calcula bien.
2. Lotes duplicados A374A/B (ids 116/117) creados a mano con fecha_encaset un año en el futuro y sin aves iniciales; `LoteService.CreateAsync` no valida futuro ni duplicado → semana=1 congelada, saldo=0, %pérdidas=100%, indicadores colapsados.
3. Filas de traslado en seguimiento_diario_levante fechadas con "hoy" UTC (default de los callers del modal) → semanas fantasma 34/35.
4. K345A/B siguen con año de guía 2023 (el script de alineación jamás corrió en prod).
5. Producción: endpoint filter-data devuelve DTO sin fechaEncaset → edad 0/semana 26 fija/etapa errada; la tarjeta usa otro endpoint correcto (74 semanas).

## Validación y gates
Cada fase: `yarn build` / `dotnet build` + `dotnet test` (xUnit para toda fórmula pura nueva). Data-fix en PROD solo con OK explícito + backup. Cambios de fn SQL vía migración EF idempotente (CREATE OR REPLACE), nunca editando migraciones aplicadas.

# 📊 Tracker de Estado — Validación y corrección masiva del saldo de alimento (engorde, lotes "2602")

**Plan de referencia:** [fase_de_desarrollo/18_validacion_correccion_saldo_alimento_engorde_2602.md](fase_de_desarrollo/18_validacion_correccion_saldo_alimento_engorde_2602.md)

**Alcance:** 34 lotes con `lote_nombre` terminando en `2602`. **Entorno:** solo local primero.

---

## Fase 1 — Diagnóstico (COMPLETADA)
- [x] Conectar a BD local `sanmarinoapplocal` y leer el lote 75 (`/api/LoteAveEngorde/75`)
- [x] Verificar la fórmula de saldo de la fn `fn_seguimiento_diario_engorde` (fuente de verdad)
- [x] Comparar saldo persistido vs calculado en los 34 lotes → **divergencia 0** (la fn no tiene bug)
- [x] Caso lote 75: confirmar que 29→30 cuadra aritméticamente; raíz = clamp del 28 (−1765) + consumo anómalo del 30 (360)
- [x] Validación masiva: 15/34 lotes con ledger negativo
- [x] Clasificar negativos: 9 **transitorios** (timing) vs 6 **déficit real** (falta ingreso / consumo sobrecargado)
- [x] Escribir plan (`fase_de_desarrollo/18_*`) y resetear este tracker

## Fase 2 — Decisión del usuario (COMPLETADA)
- [x] Clase A (transitorios): **M1** (piso 0 con reseteo de base)
- [x] Ámbito: **cambiar la función global** + recálculo masivo persistido
- [x] Hallazgo: frontend y backend C# YA usaban M1; la fn SQL era la única en M0 → M1 alinea todo
- [ ] Clase B (déficit real): "validar mejor antes" → forensics hechos; **falta decisión por lote** (ver §abajo)

## Fase 3 — Implementación (COMPLETADA en local)
- [x] Respaldo `_backup_correccion_saldo_2602_2026_05_31` (1666 filas) + `_migracion_saldo_alimento_m1_2026_05_31`
- [x] Modificar `backend/sql/fn_seguimiento_diario_engorde.sql` → **v6 (M1)**, forma cerrada de Lindley
- [x] Migración EF idempotente `20260531194613_FixFnSeguimientoEngordeM1SaldoAlimento` (CREATE OR REPLACE + recálculo masivo)
- [x] Aplicada a local vía `dotnet ef database update` (registrada en `__EFMigrationsHistory`)
- [x] Recalcular `saldo_alimento_kg` persistido (todo engorde)

## Fase 4 — Validación (COMPLETADA en local)
- [x] Lote 75: 28→0, **29→6920, 30→16680** (cuadra día a día y coincide con el frontend)
- [x] 34 lotes 2602: 0 negativos
- [x] **Prueba de concordancia C# vs fn**: réplica exacta del algoritmo `RecalcularSaldoAlimentoPorLoteAsync`
      en PL/pgSQL. Detectó 2 gaps reales corregidos en la fn:
      1. (bug de la propia réplica) `NULL LIKE` descartaba ingresos con `referencia` NULL — corregido en la prueba.
      2. **Apertura**: la fn sumaba plano; frontend/C# pisan en 0 por paso (Lindley) → fn v6 ahora pisa la apertura.
- [x] **Concordancia TRIPLE en TODO engorde (75 lotes, 3495 segs): persistido = fn = C# → 0 divergencias, 0 negativos**
- [x] Función temporal de prueba `_test_saldo_csharp` eliminada; migración recompila sin errores
- [ ] `make down` (no aplica: no se levantó backend/docker; validación 100% vía SQL)

## Fase 6 — NUEVO: Reconciliación stock inventario ↔ saldo seguimiento (plan `19_*`)
- [x] Diagnóstico: existen 2 sistemas paralelos de alimento que NO concuerdan
      - Inventario real (`inventario_gestion_stock`, por tipo) vs saldo seguimiento (fn)
      - Ingresos fantasma (`cuadrar_saldos_engorde` + `manual_backfill`) no están en stock real
      - `INV_CONSUMO` ≈ 2-2.5× el consumo del seguimiento (incluso date-scoped, sin solape de ciclo)
      - Stock es galpón-acumulado entre ciclos secuenciales (2601→2602→2603); saldo es por ciclo
      - Ej. lote 75/G0042: stock 39 435 kg vs saldo 16 680 kg
- [x] **Decisiones del usuario**: fuente de verdad = **SEGUIMIENTO**; consumo 2× = investigar; arrancar read-only
- [x] **Causa del consumo 2× RESUELTA**: los movimientos `Consumo` del inventario se generan desde el
      seguimiento (`reference="Seguimiento aves engorde #<id>"`) y el total por galpón suma TODOS los
      ciclos (2601+2602+2603). Atribuidos por lote vía el `#`, muchos cuadran; otros tienen consumo del
      seguimiento que NUNCA se posteó al inventario (ej. lote 75: SM0178 Super Pollo Engorde, 24 520 kg
      reportados, 0 posteados → stock SM0178 sobreestimado a 38 740).
- [x] **Fase A — reporte read-only construido** (vistas en `backend/sql/vw_validacion_alimento_engorde.sql`):
      `vw_validacion_alimento_engorde_por_lote` y `vw_validacion_alimento_engorde_por_tipo`.
      Resumen 75 lotes engorde: 30 con consumo sin postear, 42 con ingresos antes de encaset, 53 saldo≠stock.
- [x] Fase B — reporte revisado; causa raíz = consumo del seguimiento sin postear al inventario
- [x] **Fase C — CUADRE EJECUTADO Y VALIDADO EN LOCAL** (`backend/sql/cuadre_inventario_vs_seguimiento_2602.sql`
      + migración `20260601032401_CuadreInventarioVsSeguimiento2602`, idempotente, respaldo
      `_backup_cuadre_inv_stock_2026_05_31`, Down() reversible):
      - Posteó **131 movimientos Consumo (286 200 kg)** que el seguimiento reportó pero el inventario omitió,
        atribuidos por tipo_alimento; decrementó stock (piso 0).
      - Resultado 34 lotes 2602: **20 cuadran exacto** (stock=saldo) · 4 con residuo menor (<5 000 kg:
        ingresos fantasma + condonación M1 + split multi-tipo) · 10 con residuo mayor (galpón YA ocupado
        por el ciclo 2603 → su stock no es del 2602; comparar contra el lote 2603).
      - Lote 75: SM0178 38 740→18 360; total 18 360 vs saldo 16 680.
- [x] **Bug del split multi-tipo CORREGIDO**: el consumo de días con dos tipos ahora va completo al tipo
      con más stock (antes 50/50 → perdía kg en piso 0). Lote 75: inventario 18 360→**14 915** (físico real).
- [x] **Lote 75 RESUELTO (cuadra exacto 14 915 = 14 915)**: fix de fecha del ingreso doc 9445 (10 320 kg)
      del 29→28-may (llegó antes, registrado tarde) → desaparece el déficit del 28, M1 deja de inflar y
      el saldo baja de 16 680 a 14 915 = inventario. Respaldo `_backup_fix_fecha_ingreso_2026_05_31`.
      El 695 kg SM0176 = leftover de una EliminacionStock de 18 150 del 23-abr (ciclo 2601, no del 75).
      ⚠️ Este fix se aplicó DIRECTO en local; NO está en migración → para prod hay que migrarlo o repetirlo.
- [x] **Revisión completa de residuos (panorama final):**
      - **Déficits transitorios NO uniformes**: lote 75 limpio (1 ingreso 1 día tarde) PERO G0035 (−16 490)
        es estructural (consumo ~8-9 t/día, ingresos en bloque cada 2-3 días) → re-fechar a ciegas es RIESGOSO.
        La migración general de fechas solo es segura para el subconjunto limpio (déficit chico, 1 ingreso lo resuelve).
      - **10 de residuo grande**: 6 tienen ciclo ACTIVO = 2603 (mismo bug de consumo sin postear, fuera de scope)
        → para cuadrarlos hay que **extender el cuadre al ciclo activo 2603**. El 2601 (cerrado) sigue excluido.
      - **Stock < saldo (G0041, G0055, G0036)**: problema INVERSO, al inventario le falta stock
        (ingresos fantasma en seguimiento no en stock real, o EliminacionStock que borró stock real).
- [x] **CUADRE DEFINITIVO (modelo expected M0) — EJECUTADO Y VALIDADO EN LOCAL.**
      Decisiones del usuario: fuente de verdad = seguimiento; expected = ingresos − consumo por lote;
      no arrastrar sobrante de ciclo cerrado; extender al ciclo activo (2603); sumar fantasma; usar fechas como están.
      - Migración `20260601032401_CuadreInventarioVsSeguimiento2602` (reescrita): para cada galpón con lote 2602,
        fija stock por tipo = GREATEST(0, ingresos_ciclo(item, >= encaset, incl fantasma) − consumo(item)).
        Lote ACTIVO (2602 abierto o 2603 que relevó); ciclos cerrados excluidos (cycle-scoping respeta
        EliminacionStock previa y no arrastra sobrante). Atribución multi-tipo DETERMINISTA (por ingresos) → idempotente.
        Respaldo `_backup_cuadre_expected_2026_06_01` + AjusteStock auditable + Down() reversible.
      - Script: `backend/sql/cuadre_inventario_expected_m0_2602.sql`.
      - **Resultado: 34/34 galpones activos cuadran stock=expected. Idempotente (re-run sin cambios).**
        Lote 75: SM0178 14 220 + SM0176 695 = 14 915. G0050 (2603) computado con su propio ciclo.
- [ ] NOTA display: el inventario = M0 ("cuánto debe tener"). La PANTALLA de seguimiento muestra M1 (floored);
      difieren por la condonación transitoria (lote 75: pantalla 16 680 vs físico 14 915). Decidir si la
      pantalla debe mostrar M0 (separado; no afecta el inventario ya cuadrado).
- [ ] PROD: 3 migraciones correrán al desplegar (M1 fn + cuadre saldo + cuadre inventario). Revisar antes.
- [ ] **Prod**: la migración correrá en el próximo deploy (idempotente). Revisar antes de desplegar.

## Fase 5 — PENDIENTE de decisión (déficit real + prod)
- [ ] **6 lotes déficit real** (16, 8, 62, 61, 74, 71): M1 los muestra cuadrados (piso 0) pero el dato crudo
      sigue con consumo > alimento ingresado (afecta conversión/FCR, no el saldo). Decidir por lote:
      - Lote 61 (−2880): hay `INV_INGRESO 2880` anulado (doc 56114, 06-abr) que coincide exacto → ¿revertir anulación?
      - Lote 8 (−5650): 0 ingresos, encaset 31-ene pero seguimientos solo mar 18-24 → lote fragmentario, revisar carga
      - Lotes 16/62/74/71: anulados NO cuadran con el déficit → consumo inflado o ingreso físico nunca cargado (necesita Excel)
- [ ] **Anomalía consumo lote 75 día 30 = 360 kg** (vs ~3400) → posible dígito faltante (¿3600?). Confirmar con operación.
- [ ] **Deploy a prod**: la migración está lista pero NO desplegada. Requiere confirmación explícita (CLAUDE.md).

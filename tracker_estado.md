# Tracker — Matriz Verenice rev 6-jul-26 · Postura Colombia

**Plan:** [fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md](fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md)

## Validación (hecha)
- [x] Leer Excel 6jul26 completo (12 hojas + 17 screenshots)
- [x] Investigación de código en main por REQ (evidencia file:line)
- [x] Verificación de datos en BD local (lotes 116/117 encaset futuro, filas traslado, K345 año 2023)
- [x] Matriz de estado por ítem en el plan
- [ ] Validación en vivo con credenciales de Verenice (pendiente)

## Implementación (delegada a 9 "departamentos" en paralelo — HECHO salvo build front)
### Backend — build Infrastructure/API 0 errores · tests 416/416 verdes
- [x] REQ-002b: Regional vía master_list_options (regional='' además de NULL) — LotePosturaLevanteService
- [x] REQ-002e: consumo diario/tabla H y M en fn_indicadores_levante_postura + DTO
- [x] REQ-002f: acumulados = bajas_acum/aves_iniciales; excluye filas traslado (mata semana 25 fantasma)
- [x] REQ-002-B36: defensas fn (fallback base aves, encaset futuro → 0 filas, DROP TEMP TABLE IF EXISTS)
- [x] REQ-011a/009c: LoteService rechaza encaset futuro + nombre duplicado (company+granja)
- [x] REQ-011b: consumo vs saldo por sexo (soft-warning log) + fix CalcularHembrasVivasAsync (traslados)
- [x] REQ-011d: LiquidacionTecnica.CalcularSemana delega en SeguimientoEngordeCalculos (sin negativos)
- [x] REQ-006: enforcement backend lote Cerrado en Create/Update/Delete (levante y producción)
- [x] REQ-012d: filter-data producción con FechaEncaset/aves/estadoCierre (DTO real)
- [x] REQ-012a: fecha inicio producción efectiva = MIN(fecha) seguimientos
- [x] REQ-012b: semana 25 habilitada (fn 25.. + legacy 175 días)
- [x] REQ-012c: MovimientoAvesCalculos.EtapaProduccion borde <26 → etapa 1
- [x] REQ-004: %Retiro real H/M en fn_indicadores_produccion_postura + DTO
- [x] REQ-005: vista vw_guia_genetica_por_lote_postura idempotente lista
- [x] Migración EF `20260717000000_PosturaVereniceFnsIndicadoresYVista` (DROP+CREATE fns + vista) — compila
- [x] Tests: MovimientoAvesCalculosTests actualizados + ProduccionCalculosTests %retiro nuevos

### Frontend — build en curso (integración)
- [x] REQ-002m/n: reorden td tbody (peso+unif antes de mortalidad) — 23 cols alineadas
- [x] REQ-002a: 4 columnas → chips encima + colspan 28→23
- [x] REQ-002b: chip Regional lee valor resuelto
- [x] REQ-001a: "Bajas total"→"Mortalidad Total", "% bajas/ini"→"% Mort. Total/ini" (tabla+export+hints)
- [x] REQ-007c: consumo acumulado H y M separado (+2 columnas + export)
- [x] REQ-007d: %Retiro semanal (saldo 0 → null, sin 100% falso)
- [x] REQ-008a: gr/ave/día H/M en Reporte semana (quita Consumo total agrupado)
- [x] REQ-008b: quita % consumo H/M
- [x] REQ-008c: excluye filas traslado-only (mata semanas fantasma 34/35)
- [x] REQ-011d: fecha<encaset → "—" + banner (tabs-principal + tabla-lista-registro)
- [x] REQ-010f/002h: barrido conversión alimenticia (gráficas + indicadores diarios + FCR + liquidación 4→3 params)
- [x] REQ-010b: selector H/M/Ambos (gancho visual; backend por sexo pendiente)
- [x] REQ-012c: etapa calculada en vivo 26-33/34-50/>50 (select readonly) + 3 fórmulas alineadas
- [x] REQ-012d: getFechaBaseEdad ← informacionLote.fechaEncaset
- [x] REQ-012e: quita TIPO ITEM H/M (tabla+export) + oculta select si Colombia
- [x] REQ-004 (front): columnas %Retiro Real vs Guía en indicadores producción + export
- [x] REQ-000c: empty-state con causa (encaset futuro / sin guía)
- [x] REQ-011a/009c (front): [max]=hoy en fechaEncaset + validador futuro + hint duplicado
- [x] REQ-009a: default traslado = último registro origen (fecha LOCAL, no UTC) — levante y producción
- [ ] `yarn build` 0 errores ← EN CURSO

## Fase 0 — Data-fix (PREPARADO, NO aplicado) — `backend/sql/fix_datos_postura_verenice_jul26.sql`
- [x] Script idempotente escrito (encaset 116/117, K345 año, backfill fecha inicio prod, auditoría)
- [ ] ⛔ Aplicar: requiere confirmación Verenice (fecha real movimiento físico + OK prod + backup)
- [x] Gotcha registrado: trigger trg_lotes_sync_lote_postura_levante sobrescribe aves con NULL → script incluye respaldo/restauración

## DIFERIDO — requiere respuestas de Verenice (correo enviado)
- [ ] REQ-001b (nombres "Informe RA 2025") · REQ-001c (maestro ERP) · REQ-007i (bodegas) · REQ-003 (formato Lote General) · REQ-000a (carga masiva lotes postura + Fase 3) · REQ-000b (autosave draft — additive, no bloqueado pero no incluido esta tanda)
- [ ] Fórmulas nuevas: N1 Kcal/Prot · N2 % huevo vs guía · N3 IP/Masa/PesoM-H · N4 embriodiagnosis

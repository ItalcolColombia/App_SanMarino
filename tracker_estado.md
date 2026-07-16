# Tracker — Matriz Verenice rev 6-jul-26 · Postura Colombia

**Plan:** [fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md](fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md)

## Validación (hecha)
- [x] Leer Excel 6jul26 completo (12 hojas + 17 screenshots embebidos)
- [x] Investigación de código en main por REQ (workflow 8 agentes, evidencia file:line)
- [x] Verificación de datos en BD local (lotes 116/117 encaset futuro, filas traslado fantasma, K345 año 2023, regional resoluble vía master_list_options)
- [x] Matriz de estado por ítem (RESUELTO/PARCIAL/FALLA/NUEVO/BLOQUEADO) en el plan
- [ ] Validación en vivo con credenciales de Verenice (pendiente: credenciales no recibidas)

## Fase 0 — Data-fix 🔥 (`backend/sql/fix_datos_postura_verenice_jul26.sql`)
- [ ] Script idempotente: encaset 116/117 ← lote origen + sync lote_postura_levante
- [ ] Aves iniciales 116/117 (o re-fechar ingreso traslado) — ⛔ confirmar fecha real movimiento físico
- [ ] Re-fechar/eliminar filas traslado-only mal fechadas (114: 2026-06-08/11 + espejo 116)
- [ ] K345A/B año guía 2023→2026 + auditoría lotes activos company 1 + sync tablas postura
- [ ] Backfill fecha_inicio_produccion = MIN(fecha) seguimientos
- [ ] Aplicar en LOCAL + verificar (fn 116 devuelve semanas reales; sin semanas 34/35)
- [ ] PROD: presentar plan + esperar OK explícito

## Fase 1 — Hotfixes front (S)
- [ ] REQ-002m/n: reordenar td tbody (peso+unif antes de mortalidad) en tabla-lista-indicadores
- [ ] REQ-002a: quitar 4 columnas por fila → chips encima (con Regional); fix colspan empty-state
- [ ] REQ-008b: eliminar % consumo H/M (tabla + export)
- [ ] REQ-012e: quitar TIPO ITEM H/M (tabla + export) + ocultar select si Colombia
- [ ] REQ-001a: renombrar "Bajas total"/"% bajas/ini" en Reporte semana + export + hints
- [ ] `yarn build` 0 errores

## Fase 2 — Indicadores levante (fn + DTO + UI)
- [ ] REQ-002b: Regional = l.Regional ?? MasterListOptions (LotePosturaLevanteService)
- [ ] REQ-002e: consumo H/M real+guía en fn_indicadores_levante_postura + DTO + columnas
- [ ] REQ-002f: acumulados = bajas_acum/aves_iniciales; excluir filas solo-traslado; resumen desde totales
- [ ] REQ-002-B36: defensas fn (fallback base, sin clamp negativo, DROP TEMP TABLE IF EXISTS)
- [ ] Migración EF idempotente CREATE OR REPLACE + spec en backend/sql/ + probar local

## Fase 3 — Semana/edad/etapa + validaciones
- [ ] REQ-011a/009c: rechazar encaset futuro (back Create/Update) + [max]=hoy + warning duplicado
- [ ] REQ-011d: fecha<encaset ⇒ null + "—" + banner; unificar cálculo semana (fix LiquidacionTecnica negativos)
- [ ] REQ-011b: validar consumo/mort vs saldo por sexo a la fecha + fix CalcularHembrasVivasAsync (traslados)
- [ ] REQ-007d: cablear %Retiro semanal (saldo 0 → null, nunca 100%)
- [ ] REQ-012d: getFechaBaseEdad ← informacionLote.fechaEncaset + filter-data producción con DTO real
- [ ] REQ-012c: etapa calculada en vivo 26-33/34-50/>50 (select readonly) + alinear MovimientoAvesCalculos
- [ ] REQ-012a: fecha inicio producción efectiva MIN(fecha) + warning modal cierre
- [ ] REQ-012b: habilitar semana 25 (fn 25.. + clamps front + legacy 175 días) vía migración
- [ ] `dotnet build` + `dotnet test` verdes

## Fase 4 — Traslado
- [ ] REQ-009a: default fecha = último registro origen (o required); hoy LOCAL no UTC
- [ ] REQ-008c: excluir filas traslado-only del Reporte semana

## Fase 5 — Reporte semana + gráficas
- [ ] REQ-008a: quitar Consumo total; gr/ave/día H/M (saldo por sexo); memoizar reporteSemanaFilas
- [ ] REQ-010f/002h: barrido conversión alimenticia (gráficas + indicadores diarios + FCR + liquidación 4→3 params)
- [ ] REQ-010b: sexo H/M/Ambos en fn/endpoint + selector en gráficas e indicadores
- [ ] Etiquetas "Consumo (g/ave/día)"

## Fase 6 — Transversales
- [ ] REQ-005: migración EF vw_guia_genetica_por_lote_postura + f_safe_numeric
- [ ] REQ-006: guard producción + enforcement backend lote cerrado
- [ ] REQ-003: opción "Lote General (A+B+C)" en tabs (consume consolidados existentes)
- [ ] REQ-004: %Retiro real en fn producción + UI + Excel
- [ ] REQ-000b: DraftStorageService + autosave modales levante/producción
- [ ] REQ-000a: TipoMigracion.LotesPostura + Fase 3 migraciones (Ventas/MovAves/MovHuevos)
- [ ] REQ-000c: empty-state con causa (encaset inválido / sin guía)

## Fase 7 — Fórmulas nuevas (⛔ insumos de la líder)
- [ ] N1 Kcal/Prot H/M sem+acum vs guía (falta tabla nutricional)
- [ ] N2 % clasificación huevo vs guía (falta confirmar guía)
- [ ] N3 IP/MasaHuevo/PesoM-H/GrHuevoT-Inc(+MES) (IP sin fórmula)
- [ ] N4 Módulo embriodiagnosis (falta levantamiento)

## Bloqueantes con Verenice (sección 4 del plan)
- [ ] Fecha real movimiento físico A374A/B · política año guía · flujo REQ-006 · definición "bodega" REQ-007i · doc Informe RA 2025 · maestro ERP · tabla nutricional · % huevo en guía · fórmula IP · formato embriodiagnosis

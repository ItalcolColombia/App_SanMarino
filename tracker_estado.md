# Tracker — Validación vistas Pollo Engorde Ecuador

Plan: [validacion_vistas_engorde_ecuador_plan.md](./fase_de_desarrollo/validacion_vistas_engorde_ecuador_plan.md)

## Estado: 🟢 Vistas reescritas y validadas en LOCAL (0 diff vs funciones). Prod NO desplegado (espera OK).

### Diagnóstico (completado)
- [x] Verificar tablas reales en BD local (`seguimiento_diario_aves_engorde` = única con datos; `_ecuador`/`_panama` no existen)
- [x] Extraer columnas de las 3 vistas desplegadas
- [x] Diff `vw_seguimiento_pollo_engorde` vs `fn_seguimiento_diario_engorde`
- [x] Diff `vw_liquidacion_ecuador_pollo_engorde` vs `fn_indicadores_pollo_engorde`
- [x] Revisar `vw_indicadores_diarios_engorde` (ya alineada con repo)

### Decisiones del usuario
- Alcance: **las 3 vistas**. Seguimiento: **incluir días movimiento-only**.
- Diseño: **reimplementación set-based (sin LATERAL)**. `seguimiento_id` NULL + columna `tipo_fila`.

### Implementación
- [x] Rebuild `vw_liquidacion_ecuador_pollo_engorde` (+ bloque merma/ajuste/producción + fix R3.1 kg_carne) — **validado 0 diff vs fn**
- [x] Rebuild `vw_seguimiento_pollo_engorde` (espejo set-based de fn_seguimiento_diario_engorde + tipo_fila + mediciones/agua/ciclo/historico/despacho peso individual) — **0/0 diff vs fn (3684 filas)**
- [x] Rebuild `vw_indicadores_diarios_engorde` alineado al cómputo del front (4 correcciones) + columna `pais_id` — validado por spot-check (sin oráculo SQL; el cómputo vive en el front)
- [x] Backfill `guia_genetica_ecuador_header.pais_id` (0 → país de sus lotes: header1→2, header2→3)
- [x] Migración EF idempotente `20260624165752_RebuildVistasEngordeEcuador` (DROP+CREATE **3 vistas** + backfill) + OWNER/GRANT guardados por rol — **Down+Up validados en local**
- [x] Validación local: seguimiento 0/0, liquidación 0 vs fn; indicadores cobertura guía 3596/3657; `dotnet build` 0 errores

### Correcciones del indicador diario (alineado a IndicadoresDiariosEngordeComputeService + GetDatosAsync)
1. Guía por company_id + **pais_id** (sin estado='active'); LATERAL LIMIT 1.
2. Consumo mixto = `consumo_kg_hembras` (no hembras+machos).
3. Ganancia diaria vs **último peso medido > 0** (no LAG del día anterior).
4. Aves vivas / mort_acum_pct restan **despachos de metadata** (sistema antiguo) + mort+sel+errSexaje.

### Pendiente
- [ ] OK del usuario para desplegar a prod (deploy aplica la migración + backfill solos). Verificación post-deploy.
- [ ] (Recomendado) Verificar el indicador en el front/Power BI contra la vista para un lote, ya que esta vista se alineó por lectura de código (sin oráculo SQL automático).

### Archivos
- `backend/sql/vw_liquidacion_ecuador_pollo_engorde.sql`, `vw_seguimiento_pollo_engorde.sql`, `vw_indicadores_diarios_engorde.sql`, `backfill_pais_id_guia_genetica_ecuador_header.sql`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260624165752_RebuildVistasEngordeEcuador.cs`

### Cambios de comportamiento a reportar (corrección hacia la función)
- Liquidación: `kg_carne_pollos` y derivados ahora usan `COALESCE(peso_neto, bruto-tara)` (fix R3.1).
- Seguimiento: `saldo_aves_vivas` ahora resta ventas; alimento/documento por scope galpón; filas movimiento-only.

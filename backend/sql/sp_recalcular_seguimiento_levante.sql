-- ═══════════════════════════════════════════════════════════════════════════════════════
-- sp_recalcular_seguimiento_levante — grilla diaria + saldo de LEVANTE (produccion_resultado_levante)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Primer espejo de esta fn en backend/sql/ (no tenia uno hasta ahora).
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   el CTE 'base' leia seguimiento_diario_levante CRUDA. Con el flag
--   companies.permite_multiples_seguimientos_diarios ON, 2+ registros el mismo dia harian
--   que lag(peso_prom_h) OVER (ORDER BY fecha_registro) -- unas lineas mas abajo, en ac_base --
--   comparara dos registros del MISMO dia como si fueran dias consecutivos, dando un
--   gr_ave_dia_h/m sin sentido. A diferencia de las 3 fns semanales (donde SUM asociativa
--   hacia bastar un fix de conteo), ESTA fn necesitaba agrupar por dia ANTES de la ventana.
-- Fuente ahora: fn_seguimiento_diario_levante(l_lote_id) en vez de la tabla cruda -- una
-- fila por dia siempre (agrupada si el flag esta ON, un espejo 1:1 si esta OFF).
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
declare
  v_fecha_encaset date;
  v_h_ini int;
  v_m_ini int;
  v_mort_caja_h int;
  v_mort_caja_m int;
  v_codigo_guia text;
  v_raza text;
  v_ano_gen int;
begin

  select fecha_encaset,
         coalesce(hembras_l, 0),
         coalesce(machos_l, 0),
         coalesce(mort_caja_h, 0),
         coalesce(mort_caja_m, 0),
         codigo_guia_genetica,
         raza,
         ano_tabla_genetica
    into v_fecha_encaset, v_h_ini, v_m_ini, v_mort_caja_h, v_mort_caja_m, v_codigo_guia, v_raza, v_ano_gen
  from lotes
  where lote_id = l_lote_id::integer;   -- lotes.lote_id es integer; l_lote_id es text

  if not found then
    raise exception 'Lote % no existe', l_lote_id;
  end if;

  delete from produccion_resultado_levante where lote_id = l_lote_id;

  insert into produccion_resultado_levante (
    lote_id, fecha, edad_semana,
    hembra_viva, mort_h, sel_h_out, err_h, cons_kg_h, peso_h, unif_h, cv_h,
    mort_h_pct, sel_h_pct, err_h_pct, ms_eh_h,
    ac_mort_h, ac_sel_h, ac_err_h, ac_cons_kg_h, cons_ac_gr_h, gr_ave_dia_h,
    dif_cons_h_pct, dif_peso_h_pct, retiro_h_pct, retiro_h_ac_pct,
    macho_vivo, mort_m, sel_m_out, err_m, cons_kg_m, peso_m, unif_m, cv_m,
    mort_m_pct, sel_m_pct, err_m_pct, ms_em_m,
    ac_mort_m, ac_sel_m, ac_err_m, ac_cons_kg_m, cons_ac_gr_m, gr_ave_dia_m,
    dif_cons_m_pct, dif_peso_m_pct, retiro_m_pct, retiro_m_ac_pct,
    rel_m_h_pct,
    peso_h_guia, unif_h_guia, cons_ac_gr_h_guia, gr_ave_dia_h_guia, mort_h_pct_guia,
    peso_m_guia, unif_m_guia, cons_ac_gr_m_guia, gr_ave_dia_m_guia, mort_m_pct_guia,
    alimento_h_guia, alimento_m_guia
  )
  with base as (
    -- fn_seguimiento_diario_levante: agrupa por dia cuando el flag de la empresa lo pide (una
    -- fila por dia SIEMPRE). Antes esto leia la tabla cruda directo -- con 2 registros el mismo
    -- dia, lag(peso_prom_h) unas lineas mas abajo compararia dos registros del MISMO dia como si
    -- fueran dias consecutivos. fecha_ts preserva el timestamptz original (byte a byte igual al
    -- `s.fecha` de antes cuando no hay agrupacion).
    select
           s.fecha_ts as fecha_registro,
           s.mortalidad_hembras, s.mortalidad_machos,
           s.sel_h, s.sel_m,
           s.error_sexaje_hembras, s.error_sexaje_machos,
           s.consumo_kg_hembras, s.consumo_kg_machos,
           s.peso_prom_hembras as peso_prom_h, s.peso_prom_machos as peso_prom_m,
           s.uniformidad_hembras as uniformidad_h, s.uniformidad_machos as uniformidad_m,
           s.cv_hembras as cv_h, s.cv_machos as cv_m,
           case when v_fecha_encaset is null then null
                else (1 + floor(extract(epoch from (s.fecha_ts - v_fecha_encaset)) / 86400.0 / 7.0)::int)
           end as edad_sem,
           (coalesce(s.mortalidad_hembras,0) + coalesce(s.sel_h,0) + coalesce(s.error_sexaje_hembras,0)
             + coalesce(s.traslado_salida_hembras,0) - coalesce(s.traslado_ingreso_hembras,0)) as out_h,
           (coalesce(s.mortalidad_machos,0)  + coalesce(s.sel_m,0) + coalesce(s.error_sexaje_machos,0)
             + coalesce(s.traslado_salida_machos,0) - coalesce(s.traslado_ingreso_machos,0)) as out_m
    from fn_seguimiento_diario_levante(l_lote_id) s
  ),
  ac_base as (
    select b.*,
           sum(b.out_h) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_h_prev,
           sum(b.out_m) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_m_prev,
           sum(coalesce(b.mortalidad_hembras,0)) over (order by b.fecha_registro) as ac_mort_h,
           sum(coalesce(b.sel_h,0))             over (order by b.fecha_registro) as ac_sel_h,
           sum(coalesce(b.error_sexaje_hembras,0)) over (order by b.fecha_registro) as ac_err_h,
           sum(coalesce(b.consumo_kg_hembras,0))   over (order by b.fecha_registro) as ac_cons_kg_h,
           sum(coalesce(b.mortalidad_machos,0)) over (order by b.fecha_registro) as ac_mort_m,
           sum(coalesce(b.sel_m,0))             over (order by b.fecha_registro) as ac_sel_m,
           sum(coalesce(b.error_sexaje_machos,0)) over (order by b.fecha_registro) as ac_err_m,
           sum(coalesce(b.consumo_kg_machos,0))   over (order by b.fecha_registro) as ac_cons_kg_m,
           lag(b.peso_prom_h) over (order by b.fecha_registro) as peso_h_prev,
           lag(b.peso_prom_m) over (order by b.fecha_registro) as peso_m_prev
    from base b
  ),
  pobl as (
    select a.*,
           greatest(0, (coalesce(v_h_ini,0) - coalesce(v_mort_caja_h,0) - coalesce(a.ac_out_h_prev,0)))::int as hembra_viva,
           greatest(0, (coalesce(v_m_ini,0) - coalesce(v_mort_caja_m,0) - coalesce(a.ac_out_m_prev,0)))::int as macho_vivo
    from ac_base a
  ),
  gh as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='H'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  ),
  gm as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='M'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  )
  select
    l_lote_id as lote_id,
    p.fecha_registro as fecha,
    p.edad_sem as edad_semana,
    p.hembra_viva,
    coalesce(p.mortalidad_hembras,0) as mort_h,
    coalesce(p.sel_h,0)              as sel_h_out,
    coalesce(p.error_sexaje_hembras,0) as err_h,
    p.consumo_kg_hembras             as cons_kg_h,
    p.peso_prom_h                    as peso_h,
    p.uniformidad_h                  as unif_h,
    p.cv_h                           as cv_h,
    case when p.hembra_viva>0 then dpr(p.mortalidad_hembras * 100.0 / p.hembra_viva, 3) end as mort_h_pct,
    case when p.hembra_viva>0 then dpr(p.sel_h * 100.0 / p.hembra_viva, 3) end              as sel_h_pct,
    case when p.hembra_viva>0 then dpr(p.error_sexaje_hembras * 100.0 / p.hembra_viva, 3) end as err_h_pct,
    (coalesce(p.mortalidad_hembras,0)+coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0))   as ms_eh_h,
    p.ac_mort_h, p.ac_sel_h, p.ac_err_h,
    p.ac_cons_kg_h,
    case when p.hembra_viva>0 then dpr( (p.ac_cons_kg_h*1000.0)/p.hembra_viva, 3) end as cons_ac_gr_h,
    case when p.peso_prom_h is null or p.peso_h_prev is null then null
         else dpr(p.peso_prom_h - p.peso_h_prev, 2)
    end as gr_ave_dia_h,
    case when gh.cons_ac_gr_obj is null or p.hembra_viva<=0 then null
         else dpr( (((p.ac_cons_kg_h*1000.0)/p.hembra_viva) - gh.cons_ac_gr_obj) * 100.0 / gh.cons_ac_gr_obj, 3)
    end as dif_cons_h_pct,
    case when gh.peso_obj is null or p.peso_prom_h is null then null
         else dpr( (p.peso_prom_h - gh.peso_obj) * 100.0 / gh.peso_obj, 3)
    end as dif_peso_h_pct,
    case when p.hembra_viva>0 then dpr( (coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0)) * 100.0 / p.hembra_viva, 3) end as retiro_h_pct,
    case when (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h)>0
         then dpr( (p.ac_sel_h + p.ac_err_h) * 100.0 / (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h), 3)
    end as retiro_h_ac_pct,
    p.macho_vivo,
    coalesce(p.mortalidad_machos,0) as mort_m,
    coalesce(p.sel_m,0)             as sel_m_out,
    coalesce(p.error_sexaje_machos,0) as err_m,
    p.consumo_kg_machos             as cons_kg_m,
    p.peso_prom_m                   as peso_m,
    p.uniformidad_m                 as unif_m,
    p.cv_m                          as cv_m,
    case when p.macho_vivo>0 then dpr(p.mortalidad_machos * 100.0 / p.macho_vivo, 3) end as mort_m_pct,
    case when p.macho_vivo>0 then dpr(p.sel_m * 100.0 / p.macho_vivo, 3) end             as sel_m_pct,
    case when p.macho_vivo>0 then dpr(p.error_sexaje_machos * 100.0 / p.macho_vivo, 3) end as err_m_pct,
    (coalesce(p.mortalidad_machos,0)+coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0))   as ms_em_m,
    p.ac_mort_m, p.ac_sel_m, p.ac_err_m,
    p.ac_cons_kg_m,
    case when p.macho_vivo>0 then dpr( (p.ac_cons_kg_m*1000.0)/p.macho_vivo, 3) end as cons_ac_gr_m,
    case when p.peso_prom_m is null or p.peso_m_prev is null then null
         else dpr(p.peso_prom_m - p.peso_m_prev, 2)
    end as gr_ave_dia_m,
    case when gm.cons_ac_gr_obj is null or p.macho_vivo<=0 then null
         else dpr( (((p.ac_cons_kg_m*1000.0)/p.macho_vivo) - gm.cons_ac_gr_obj) * 100.0 / gm.cons_ac_gr_obj, 3)
    end as dif_cons_m_pct,
    case when gm.peso_obj is null or p.peso_prom_m is null then null
         else dpr( (p.peso_prom_m - gm.peso_obj) * 100.0 / gm.peso_obj, 3)
    end as dif_peso_m_pct,
    case when p.macho_vivo>0 then dpr( (coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0)) * 100.0 / p.macho_vivo, 3) end as retiro_m_pct,
    case when (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m)>0
         then dpr( (p.ac_sel_m + p.ac_err_m) * 100.0 / (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m), 3)
    end as retiro_m_ac_pct,
    case when p.hembra_viva is null or p.hembra_viva=0 then null
         else dpr(p.macho_vivo * 100.0 / p.hembra_viva, 3)
    end as rel_m_h_pct,
    gh.peso_obj, gh.unif_obj, gh.cons_ac_gr_obj, gh.gr_ave_dia_obj, gh.mort_pct_obj,
    gm.peso_obj, gm.unif_obj, gm.cons_ac_gr_obj, gm.gr_ave_dia_obj, gm.mort_pct_obj,
    gh.alimento_nom, gm.alimento_nom
  from pobl p
  left join gh on gh.semana = p.edad_sem
  left join gm on gm.semana = p.edad_sem
  order by p.fecha_registro;

end;
$function$


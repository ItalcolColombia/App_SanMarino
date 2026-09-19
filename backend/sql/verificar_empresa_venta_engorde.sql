-- verificar_empresa_venta_engorde.sql
-- Diagnostico de SOLO LECTURA de la «Empresa de venta» del despacho de pollo engorde.
-- No crea ni modifica nada (por eso el prefijo verificar_: exento del gate verificar-sql-llega-por-migracion).
-- La lista la crea la migracion 20260919120000_SeedListaMaestraEmpresaVentaEngorde; este archivo solo la mide.
--
-- Uso (despues de un deploy o sobre la copia local):
--   psql -h 127.0.0.1 -p 5433 -U postgres -d sanmarinoapplocal -X -f backend/sql/verificar_empresa_venta_engorde.sql

\echo '== 1) Lista maestra por (empresa, pais): debe haber UNA por cada fila de company_pais =='
SELECT cp.company_id,
       co.name                AS empresa,
       cp.pais_id,
       p.pais_nombre          AS pais,
       ml.id                  AS master_list_id,
       (SELECT count(*) FROM public.master_list_options o WHERE o.master_list_id = ml.id) AS opciones,
       (SELECT string_agg(o.value, ' | ' ORDER BY o."order", o.id)
          FROM public.master_list_options o WHERE o.master_list_id = ml.id)              AS valores
  FROM public.company_pais cp
  JOIN public.companies co ON co.id = cp.company_id
  JOIN public.paises    p  ON p.pais_id = cp.pais_id
  LEFT JOIN public.master_lists ml
         ON ml.key = 'venta_pollo_engorde_empresa'
        AND ml.company_id = cp.company_id
        AND ml.country_id = cp.pais_id
 ORDER BY cp.company_id, cp.pais_id;

\echo ''
\echo '== 2) Duplicados de la lista (debe estar VACIO: GetByKeyAsync usa SingleOrDefault y reventaria) =='
SELECT ml.company_id, ml.country_id, count(*) AS listas
  FROM public.master_lists ml
 WHERE ml.key = 'venta_pollo_engorde_empresa'
 GROUP BY 1, 2
HAVING count(*) > 1;

\echo ''
\echo '== 3) Ventas de engorde por empresa de venta (texto en planta_destino) =='
SELECT m.company_id,
       coalesce(nullif(btrim(m.planta_destino), ''), '(sin empresa)') AS empresa_venta,
       count(*)                                                        AS movimientos,
       count(DISTINCT m.factura_id)                                    AS despachos,
       sum(m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) AS aves
  FROM public.movimiento_pollo_engorde m
 WHERE m.deleted_at IS NULL
   AND m.tipo_movimiento = 'Venta'
 GROUP BY 1, 2
 ORDER BY 1, 3 DESC;

\echo ''
\echo '== 4) Valores guardados que NO estan (ya) en la lista de su empresa: texto libre de la carga masiva u opcion renombrada =='
SELECT m.company_id, m.planta_destino AS valor_guardado, count(*) AS movimientos
  FROM public.movimiento_pollo_engorde m
 WHERE m.deleted_at IS NULL
   AND nullif(btrim(m.planta_destino), '') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
          FROM public.master_lists ml
          JOIN public.master_list_options o ON o.master_list_id = ml.id
         WHERE ml.key = 'venta_pollo_engorde_empresa'
           AND ml.company_id = m.company_id
           AND lower(btrim(o.value)) = lower(btrim(m.planta_destino)))
 GROUP BY 1, 2
 ORDER BY 1, 3 DESC;

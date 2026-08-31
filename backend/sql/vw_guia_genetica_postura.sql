-- ============================================================================
-- vw_guia_genetica_postura — la fuente UNICA de guia genetica para el camino SQL
-- de postura (levante + produccion).
--
-- POR QUE EXISTE
-- Los indicadores de postura los calcula Postgres, y las 5 fns/vistas leian
-- `guia_genetica_sanmarino_colombia` HARDCODEADA. Para una empresa cuya guia vive
-- en la tabla reducida (`guia_genetica_santa_reyes`) devolvian 0 filas: la columna
-- «Tabla» salia VACIA, sin error. Los reportes tecnicos en C# si funcionaban,
-- porque pasan por `GuiaGeneticaLookup`, que consulta primero la tabla propia.
-- De ahi el sintoma que reporto el cliente: «a veces aparece y a veces no» —
-- dependia de si la pantalla la calculaba C# o Postgres.
--
-- 🔴 DELTA CERO POR CONSTRUCCION, no por revision
-- Los 5 objetos filtran `guia.company_id = lote.company_id`, y las dos tablas
-- estan PARTICIONADAS por empresa: ninguna empresa tiene filas en las dos.
-- Medido el 26-ago-2026 contra la copia de produccion:
--     guia_genetica_sanmarino_colombia -> companies 1 (889), 3 (15), 4 (224)
--     guia_genetica_santa_reyes        -> company 6 (615)   [interseccion vacia]
-- Para Sanmarino, Demo, Ecuador y Panama la rama nueva aporta CERO filas. No es
-- «se verifico despues»: es inalcanzable.
--
-- 🔴 LA COLUMNA `origen` NO ES DECORATIVA — es lo que evita un numero FALSO
-- La guia reducida tiene 3 columnas de dato; la compartida mas de 40. Las fns
-- coalescean a 0 lo que falta, y ese 0 NO es neutro:
--   * levante promedia por sexo con COALESCE de cada termino y divide por 2 FIJO
--     (`fn_indicadores_levante_postura:466`). Con una guia que trae hembras y no
--     machos: (95.00 + 0)/2 = 47.5 ⇒ mostraria LA MITAD de lo que dice el cliente.
--     Un numero plausible y equivocado por un factor de 2: el peor modo de falla.
--   * produccion coalescea 6 columnas a 0 (`:400-428`), y `fn_dif_pp` documenta
--     que con guia = 0 NO devuelve NULL ⇒ la columna «diferencia vs guia» de
--     mortalidad pintaria la mortalidad REAL del lote como si fuera desviacion.
-- Las fns leen `origen` y solo aplican el COALESCE cuando vale 'compartida' —
-- o sea, exactamente el comportamiento de hoy para las otras cuatro empresas.
-- Quitar esos COALESCE a secas NO seria delta cero: en el rango de produccion,
-- company 1 tiene entre 6 y 14 filas en blanco por columna.
--
-- ⚠️ `id` DE LA RAMA PROPIA VA NEGADO (`-id`)
-- `fn_indicadores_produccion_postura` desempata con `ORDER BY …, g.id`, y las dos
-- ramas tienen secuencias independientes. Negarlo garantiza que no colisionen y
-- delata el origen transitorio en un debug — el mismo criterio que ya usa
-- `GuiaGeneticaLookup.ATransitoria` en C#.
--
-- ⚠️ LO QUE ESTA VISTA NO ARREGLA (y no debe intentar)
-- Los criterios de join DIVERGEN A PROPOSITO entre fns: levante compara la raza
-- EXACTA y case-sensitive y NO filtra deleted_at; produccion usa btrim(lower()) y
-- SI filtra. Levante cruza la edad como texto exacto; produccion la parsea y
-- desempata prefiriendo '25P'. Unificarlos haria que empiecen a matchear filas que
-- hoy no matchean ⇒ el refactor cambiaria resultados por si solo. Esta vista NO
-- toca un solo WHERE: solo cambia de donde salen las filas.
--
-- ⚠️ TRES RAMAS, NO DOS (30-ago-2026)
-- A la rama propia se le sumo una tercera que proyecta esas MISMAS filas bajo la
-- grafia de raza del ERP del cliente (`BABCOK BROWN`, `HY LINE`). Es el alias que
-- el C# ya aplicaba desde el 24-ago y el camino SQL no: sin el, un lote cargado
-- con la grafia del ERP mostraba la guia en el reporte tecnico y no en indicadores.
-- Ver el bloque de esa rama, mas abajo, para el detalle y la regla de paridad.
--
-- Aplicada por las migraciones 20260826170000_VwGuiaGeneticaPosturaYFnsOrigen
-- (version original) y 20260831044636_AliasRazaGuiaSqlYSemanaInicioProduccion
-- (rama alias). Este archivo es el ESPEJO legible; la migracion es el vehiculo.
-- ============================================================================

CREATE OR REPLACE VIEW public.vw_guia_genetica_postura AS
-- ── Rama COMPARTIDA: las 53 columnas tal cual, sin tocar un tipo ──────────────
SELECT
    g.id,
    g.company_id,
    g.created_by_user_id,
    g.created_at,
    g.updated_by_user_id,
    g.updated_at,
    g.deleted_at,
    g.anio_guia,
    g.raza,
    g.edad,
    g.mort_sem_h,
    g.retiro_ac_h,
    g.mort_sem_m,
    g.retiro_ac_m,
    g.cons_ac_h,
    g.cons_ac_m,
    g.gr_ave_dia_h,
    g.gr_ave_dia_m,
    g.peso_h,
    g.peso_m,
    g.uniformidad,
    g.h_total_aa,
    g.prod_porcentaje,
    g.h_inc_aa,
    g.aprov_sem,
    g.peso_huevo,
    g.masa_huevo,
    g.grasa_porcentaje,
    g.nacim_porcentaje,
    g.pollito_aa,
    g.kcal_ave_dia_h,
    g.kcal_ave_dia_m,
    g.aprov_ac,
    g.gr_huevo_t,
    g.gr_huevo_inc,
    g.gr_pollito,
    g.valor_1000,
    g.valor_150,
    g.apareo,
    g.peso_mh,
    g.codigo_guia_genetica,
    g.hembras,
    g.machos,
    g.kcal_h,
    g.prot_h,
    g.kcal_m,
    g.prot_m,
    g.kcal_sem_h,
    g.prot_h_sem,
    g.kcal_sem_m,
    g.prot_sem_m,
    g.alim_h,
    g.alim_m,
    'compartida'::text AS origen
FROM public.guia_genetica_sanmarino_colombia g

UNION ALL

-- ── Rama PROPIA (tabla reducida): 3 columnas de dato, el resto NULL ───────────
-- NULL y no '' a proposito: las fns hacen NULLIF(btrim(x),'') y tratan los dos
-- igual, pero NULL es lo honesto y es lo que ya trae la compartida (nunca '').
SELECT
    -g.id                     AS id,          -- negado: ver nota de arriba
    g.company_id,
    g.created_by_user_id,
    g.created_at,
    g.updated_by_user_id,
    g.updated_at,
    g.deleted_at,
    g.anio_guia::text         AS anio_guia,
    g.raza::text              AS raza,
    g.edad::text              AS edad,        -- int -> '18', sin decimales
    NULL::text                AS mort_sem_h,  -- la guia reducida trae mortalidad
    g.retiro_ac_h::text       AS retiro_ac_h, --   ACUMULADA, no semanal
    NULL::text                AS mort_sem_m,
    NULL::text                AS retiro_ac_m,
    NULL::text                AS cons_ac_h,
    NULL::text                AS cons_ac_m,
    g.gr_ave_dia_h::text      AS gr_ave_dia_h,
    NULL::text                AS gr_ave_dia_m,
    NULL::text                AS peso_h,
    NULL::text                AS peso_m,
    NULL::text                AS uniformidad,
    NULL::text                AS h_total_aa,
    g.prod_porcentaje::text   AS prod_porcentaje,
    NULL::text                AS h_inc_aa,
    NULL::text                AS aprov_sem,
    NULL::text                AS peso_huevo,
    NULL::text                AS masa_huevo,
    NULL::text                AS grasa_porcentaje,
    NULL::text                AS nacim_porcentaje,
    NULL::text                AS pollito_aa,
    NULL::text                AS kcal_ave_dia_h,
    NULL::text                AS kcal_ave_dia_m,
    NULL::text                AS aprov_ac,
    NULL::text                AS gr_huevo_t,
    NULL::text                AS gr_huevo_inc,
    NULL::text                AS gr_pollito,
    NULL::text                AS valor_1000,
    NULL::text                AS valor_150,
    NULL::text                AS apareo,
    NULL::text                AS peso_mh,
    g.codigo_guia_genetica,
    NULL::varchar             AS hembras,
    NULL::varchar             AS machos,
    NULL::varchar             AS kcal_h,
    NULL::varchar             AS prot_h,
    NULL::varchar             AS kcal_m,
    NULL::varchar             AS prot_m,
    NULL::varchar             AS kcal_sem_h,
    NULL::varchar             AS prot_h_sem,
    NULL::varchar             AS kcal_sem_m,
    NULL::varchar             AS prot_sem_m,
    NULL::text                AS alim_h,
    NULL::text                AS alim_m,
    'propia'::text            AS origen
FROM public.guia_genetica_santa_reyes g

UNION ALL

-- ── Rama ALIAS de la PROPIA: la misma fila, indexada por la grafia del ERP ────
--
-- POR QUE EXISTE
-- Los lotes se cargan con el nombre de raza tal como viene del ERP del cliente
-- (`BABCOK BROWN` sin la 2a C, `HY LINE` sin el apellido), mientras que la guia
-- se sembro con el nombre comercial completo (`Babcock Brown`, `Hy Line Brown`).
-- El C# ya lo tolera desde el 24-ago-2026 (`RazaGuiaAliasCalculos`, usado por
-- `GuiaGeneticaLookup`), pero el camino SQL comparaba la raza CRUDA: medido el
-- 30-ago-2026, un lote `BABCOK BROWN` mostraba la guia en el reporte tecnico
-- (C#) y NADA en indicadores de produccion —y `0,00` en los de levante—, o sea
-- el mismo lote con dos verdades segun quien calculara la pantalla.
--
-- Se resuelve ACA y no en cada `WHERE` a proposito: los 4 objetos que consultan
-- la guia (los 2 fn_indicadores_*, los 2 fn_resumen_semanal_ra_pesadas_*) heredan
-- el alias sin tocar un solo criterio de join —que divergen entre si a proposito,
-- ver la nota de arriba—. Una sola definicion del alias para todos los lectores.
--
-- 🔴 DELTA CERO POR CONSTRUCCION
-- El JOIN solo produce filas para las razas de `guia_genetica_santa_reyes` que
-- esten en la lista, y esa tabla solo tiene filas de company 6. Para Sanmarino,
-- Demo, Ecuador y Panama esta rama devuelve CERO filas: es inalcanzable, no
-- «se reviso despues».
--
-- ⚠️ LA LISTA ES CERRADA Y SE MANTIENE EN PARIDAD CON EL C#
-- Espeja `RazaGuiaAliasCalculos.AliasPorRazaNormalizada` (Application/Calculos).
-- `Lohmann Brown` NO esta a proposito: es otra linea comercial que todavia no
-- tiene guia cargada, y mapearla a `Lohmann LSL` mostraria datos de un ave que
-- no es esa. Si se agrega un alias, va en los DOS lados o vuelven las dos verdades.
--
-- ⚠️ LAS DOS RAMAS NUNCA MATCHEAN JUNTAS: la propia se indexa por la grafia de la
-- guia y esta por la del ERP, que por definicion son distintas. El `id` lleva un
-- offset propio para que un debug delate de cual salio la fila.
SELECT
    -g.id - 10000000          AS id,          -- offset propio: ver nota de arriba
    g.company_id,
    g.created_by_user_id,
    g.created_at,
    g.updated_by_user_id,
    g.updated_at,
    g.deleted_at,
    g.anio_guia::text         AS anio_guia,
    a.alias::text             AS raza,       -- la grafia del ERP, no la de la guia
    g.edad::text              AS edad,        -- int -> '18', sin decimales
    NULL::text                AS mort_sem_h,  -- la guia reducida trae mortalidad
    g.retiro_ac_h::text       AS retiro_ac_h, --   ACUMULADA, no semanal
    NULL::text                AS mort_sem_m,
    NULL::text                AS retiro_ac_m,
    NULL::text                AS cons_ac_h,
    NULL::text                AS cons_ac_m,
    g.gr_ave_dia_h::text      AS gr_ave_dia_h,
    NULL::text                AS gr_ave_dia_m,
    NULL::text                AS peso_h,
    NULL::text                AS peso_m,
    NULL::text                AS uniformidad,
    NULL::text                AS h_total_aa,
    g.prod_porcentaje::text   AS prod_porcentaje,
    NULL::text                AS h_inc_aa,
    NULL::text                AS aprov_sem,
    NULL::text                AS peso_huevo,
    NULL::text                AS masa_huevo,
    NULL::text                AS grasa_porcentaje,
    NULL::text                AS nacim_porcentaje,
    NULL::text                AS pollito_aa,
    NULL::text                AS kcal_ave_dia_h,
    NULL::text                AS kcal_ave_dia_m,
    NULL::text                AS aprov_ac,
    NULL::text                AS gr_huevo_t,
    NULL::text                AS gr_huevo_inc,
    NULL::text                AS gr_pollito,
    NULL::text                AS valor_1000,
    NULL::text                AS valor_150,
    NULL::text                AS apareo,
    NULL::text                AS peso_mh,
    g.codigo_guia_genetica,
    NULL::varchar             AS hembras,
    NULL::varchar             AS machos,
    NULL::varchar             AS kcal_h,
    NULL::varchar             AS prot_h,
    NULL::varchar             AS kcal_m,
    NULL::varchar             AS prot_m,
    NULL::varchar             AS kcal_sem_h,
    NULL::varchar             AS prot_h_sem,
    NULL::varchar             AS kcal_sem_m,
    NULL::varchar             AS prot_sem_m,
    NULL::text                AS alim_h,
    NULL::text                AS alim_m,
    'propia'::text            AS origen
FROM public.guia_genetica_santa_reyes g
JOIN (VALUES ('babcock brown', 'BABCOK BROWN'),
             ('hy line brown', 'HY LINE')) AS a(raza_guia, alias)
  ON btrim(lower(g.raza)) = a.raza_guia;

COMMENT ON VIEW public.vw_guia_genetica_postura IS
  'Guia genetica de postura unificada para el camino SQL: guia_genetica_sanmarino_colombia '
  '(origen=compartida) + guia_genetica_santa_reyes proyectada al mismo shape (origen=propia) + '
  'esa misma guia propia indexada por la grafia de raza del ERP del cliente (tambien origen=propia; '
  'espeja RazaGuiaAliasCalculos del C#, para que un lote BABCOK BROWN / HY LINE cruce igual). '
  'Las dos tablas estan particionadas por company_id, asi que para una empresa dada la vista '
  'devuelve exactamente lo que devolvia su tabla. La columna origen la leen las fns para NO '
  'coalescear a 0 las metricas que la guia reducida no tiene (un 0 ahi es un numero falso, no '
  'un dato faltante).';

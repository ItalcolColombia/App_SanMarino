// Partial de la migracion AliasRazaGuiaSqlYSemanaInicioProduccion: SOLO las constantes SQL.
// Viven separadas para que el archivo principal se pueda leer.
//   *Nueva  = backend/sql/<objeto>.sql tal cual (el espejo).
//   *Previa = copiada VERBATIM de HEAD (git show HEAD:backend/sql/<objeto>.sql), ni una linea
//             tocada, para que el Down() devuelva el objeto EXACTAMENTE al estado anterior.

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class AliasRazaGuiaSqlYSemanaInicioProduccion
    {
        /// <summary>vw_guia_genetica_postura NUEVA: suma la rama ALIAS (grafia de raza del ERP).</summary>
        private const string VwGuiaGeneticaPosturaNueva = """
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
""";

        /// <summary>vw_guia_genetica_postura PREVIA (HEAD), verbatim.</summary>
        private const string VwGuiaGeneticaPosturaPrevia = """
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
-- Aplicada por la migracion 20260826170000_VwGuiaGeneticaPosturaYFnsOrigen.
-- Este archivo es el ESPEJO legible; la migracion es el vehiculo.
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
FROM public.guia_genetica_santa_reyes g;

COMMENT ON VIEW public.vw_guia_genetica_postura IS
  'Guia genetica de postura unificada para el camino SQL: guia_genetica_sanmarino_colombia '
  '(origen=compartida) + guia_genetica_santa_reyes proyectada al mismo shape (origen=propia). '
  'Las dos tablas estan particionadas por company_id, asi que para una empresa dada la vista '
  'devuelve exactamente lo que devolvia su tabla. La columna origen la leen las fns para NO '
  'coalescear a 0 las metricas que la guia reducida no tiene (un 0 ahi es un numero falso, no '
  'un dato faltante).';
""";

        /// <summary>fn_indicadores_levante_postura NUEVA: raza normalizada en la rama propia + sin 0 falso.</summary>
        private const string FnIndicadoresLevantePosturaNueva = """
-- ============================================================================
-- fn_indicadores_levante_postura(lote_id)
-- Indicadores semanales de LEVANTE (postura Colombia) calculados en la BD.
-- Reemplaza el cómputo del front (lote-levante/tabla-lista-indicadores +
-- graficas-principal): el front solo debe pintar.
--
-- Replica EXACTO el algoritmo del front (double precision, mismo orden) e
-- incorpora las correcciones ya acordadas:
--   * Peso/uniformidad del PESAJE semanal: último registro de la semana con
--     peso>0 (no el último día, que suele venir en 0) + arrastre del último
--     peso conocido cuando la semana no tiene pesaje (evita ganancia negativa
--     y dif -100%).  [bug histórico corregido]
--   * Guía genética REAL desde guia_genetica_sanmarino_colombia por
--     raza + año + company + semana (no valores hardcodeados / no Ecuador).
--
-- Correcciones matriz Verenice rev 6-jul-26:
--   * REQ-002e — Consumo por sexo: además del consumo mixto (compatibilidad),
--     se exponen consumo_diario_hembras / consumo_diario_machos (g/ave/día reales
--     por sexo = consumo_kg_sexo*1000 / saldo_prom_sexo / días) y
--     consumo_tabla_hembras / consumo_tabla_machos (gr_ave_dia_h/_m de la guía, SIN
--     promediar). Requiere llevar el saldo de aves POR GÉNERO dentro de la fn.
--     (Columnas renombradas de _h/_m a _hembras/_machos por el mapeo EF, ver nota abajo.)
--   * REQ-002f — Acumulados reales: mortalidad/selección acumuladas =
--     bajas_acumuladas / aves_encasetadas * 100 (acumulado real sobre aves
--     iniciales), no la suma de % semanales sobre base decreciente.
--   * REQ-002f/B36 — Semana fantasma: se EXCLUYEN las filas de PURO traslado
--     (sin mortalidad/selección/error/consumo/pesaje) posteriores a la
--     semana 25; ya no se clampean con LEAST(25) generando una "semana 25"
--     falsa con el salto de saldo del traslado post-levante.
--   * REQ-002B36 — Defensas:
--       - Base de aves con fallback: COALESCE(aves_encasetadas,
--         hembras_l+machos_l, primer traslado_ingreso, 0).
--       - Encaset futuro/ausente: si fecha_encaset es NULL o es POSTERIOR al
--         primer registro (encaset tecleado a futuro, p. ej. lote 116), se
--         devuelven CERO filas en lugar de colapsar 140+ días en una
--         "semana 1" absurda con base 0 y %pérdidas 100%. Se eligió devolver
--         cero filas (y no "usar el primer registro como referencia") porque
--         con un encaset inconsistente NINGÚN indicador es confiable: es más
--         seguro que el front muestre su empty-state a mostrar cifras
--         engañosas. Al devolver cero filas ya no hace falta GREATEST(1,…)
--         (no quedan semanas negativas que clampear).
--       - Idempotencia intra-transacción: DROP TABLE IF EXISTS _seg_sem antes
--         del CREATE TEMP TABLE (permite llamar la fn 2+ veces en la misma
--         transacción sin 'relation _seg_sem already exists').
--
-- Fuente de verdad del algoritmo: tabla-lista-indicadores.component.ts
-- Zona horaria: America/Bogota para el corte de semanas (calendario local).
--
-- Fase 3 (convergencia levante a Feature-13): lee la tabla CANÓNICA
-- seguimiento_diario_levante (tipo_seguimiento='levante') y las
-- salidas de la semana incluyen error de sexaje y traslados dedicados:
--   out = mort + sel + err + traslado_salida - traslado_ingreso;  aves_fin = aves - out.
-- ============================================================================
--   * REQ-010b — Series POR SEXO para el selector Hembras/Machos/Ambos de la
--     pestaña Gráfica: además del consumo por sexo, se exponen peso (real +
--     guía), mortalidad % (real + guía) y retiro % (real; la guía por sexo no
--     existe ⇒ NULL) por sexo, para que el control cambie las series Real/Guía.
--     Aritmética por sexo consistente con la mixta (mismo denominador = aves al
--     inicio de la semana del sexo; NULL cuando el sexo no tiene saldo/pesaje).
--
--   * TK-2026-000022 — TODOS los parametros por sexo en la TABLA de indicadores.
--     El usuario reporto que «los parametros aparecen solo para un grupo de aves y
--     no identifica si se refieren a hembras o machos». Peor: varias columnas
--     mixtas son un PROMEDIO ARITMETICO simple de los dos sexos (peso_cierre y
--     unif_real: (H+M)/2, sin ponderar por cantidad de aves), o sea un valor que
--     no le corresponde a ninguna ave del galpon —en reproductoras la hembra y el
--     macho tienen pesos muy distintos—. Se exponen aves inicio/fin, consumo total,
--     uniformidad, ganancia, dif % de peso vs guia, seleccion % y error de sexaje %
--     por sexo. NO se agrega aritmetica nueva: son las mismas variables internas
--     con las que ya se arman las columnas mixtas, publicadas sin promediar.
--
-- IMPORTANTE (mapeo EF): los nombres de las columnas por sexo son el snake_case
-- EXACTO de las props del DTO (…Hembras→…_hembras, …Machos→…_machos). EF Core
-- (SqlQueryRaw<IndicadorSemanalLevanteDto> con convención snake_case) mapea
-- ConsumoDiarioHembras↔consumo_diario_hembras, PesoHembras↔peso_hembras, etc.
-- Un nombre abreviado (_h/_m) NO mapearía a props …Hembras/…Machos (mismo patrón
-- probado en fn_indicadores_produccion_postura: porcentaje_mortalidad_hembras…).
-- Por eso las columnas de consumo por sexo se renombran de _h/_m a _hembras/_machos.
--
-- DROP previo: la firma cambió (se renombraron/agregaron columnas OUT por sexo),
-- y CREATE OR REPLACE no puede alterar el tipo de retorno.
DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    aves_inicio_semana              double precision,
    aves_fin_semana                 double precision,
    consumo_diario                  double precision,   -- g/ave/día real (mixto H+M)
    consumo_tabla                   double precision,   -- g/ave/día guía (promedio H,M)
    consumo_total_semana            double precision,   -- gramos
    conversion_alimenticia          double precision,
    peso_tabla                      double precision,
    unif_real                       double precision,
    unif_tabla                      double precision,
    mort_tabla                      double precision,
    dif_peso_pct                    double precision,
    ganancia_semana                 double precision,
    ganancia_diaria_acumulada       double precision,
    ganancia_tabla                  double precision,
    mortalidad_sem                  double precision,
    seleccion_sem                   double precision,
    error_sexaje_sem                double precision,
    mortalidad_mas_seleccion        double precision,
    eficiencia                      double precision,
    ip                              double precision,
    vpi                             double precision,
    saldo_aves_semanal              double precision,
    mortalidad_acum                 double precision,
    seleccion_acum                  double precision,
    mortalidad_mas_seleccion_acum   double precision,
    piso_termico_visible            boolean,
    peso_inicial                    double precision,
    peso_cierre                     double precision,
    dias_con_registro               integer,
    -- REQ-002e / REQ-010b: series POR SEXO (reales y guía SIN promediar). numeric → decimal? en el DTO.
    -- Nombres = snake_case EXACTO de las props del DTO para que EF las mapee (ver nota de cabecera).
    consumo_diario_hembras          numeric,            -- g/ave/día real hembras
    consumo_diario_machos           numeric,            -- g/ave/día real machos
    consumo_tabla_hembras           numeric,            -- gr_ave_dia_h de la guía
    consumo_tabla_machos            numeric,            -- gr_ave_dia_m de la guía
    peso_hembras                    numeric,            -- peso prom hembras (arrastre si semana sin pesaje)
    peso_machos                     numeric,            -- peso prom machos  (arrastre si semana sin pesaje)
    peso_tabla_hembras              numeric,            -- guía peso_h
    peso_tabla_machos               numeric,            -- guía peso_m
    mort_pct_hembras                numeric,            -- % mort semana hembras = mort_h / aves_inicio_h * 100
    mort_pct_machos                 numeric,            -- % mort semana machos  = mort_m / aves_inicio_m * 100
    mort_tabla_hembras              numeric,            -- guía mort_sem_h
    mort_tabla_machos               numeric,            -- guía mort_sem_m
    retiro_pct_hembras              numeric,            -- % retiro hembras = (mort+sel+err)_h / aves_inicio_h * 100
    retiro_pct_machos               numeric,            -- % retiro machos  = (mort+sel+err)_m / aves_inicio_m * 100
    -- TK-2026-000022: el resto de los parametros POR SEXO. La tabla de indicadores mostraba una
    -- sola serie sin decir de que sexo era —y varias de esas columnas mixtas son un PROMEDIO
    -- ARITMETICO de hembras y machos (peso, uniformidad), o sea un numero que no le corresponde a
    -- ninguna ave real. Todo esto ya se calculaba dentro de la funcion; solo faltaba exponerlo.
    -- Convencion identica a las de arriba: NULL cuando el sexo no existe en el lote o no hay dato,
    -- nunca 0 sintetico.
    aves_inicio_hembras             numeric,            -- saldo hembras al inicio de la semana
    aves_fin_hembras                numeric,            -- saldo hembras al cierre de la semana
    aves_inicio_machos              numeric,            -- saldo machos al inicio de la semana
    aves_fin_machos                 numeric,            -- saldo machos al cierre de la semana
    consumo_total_semana_hembras    numeric,            -- gramos consumidos por las hembras en la semana
    consumo_total_semana_machos     numeric,            -- gramos consumidos por los machos en la semana
    unif_hembras                    numeric,            -- % uniformidad hembras del pesaje de la semana
    unif_machos                     numeric,            -- % uniformidad machos  del pesaje de la semana
    ganancia_hembras                numeric,            -- g ganados por las hembras respecto de la semana previa
    ganancia_machos                 numeric,            -- g ganados por los machos  respecto de la semana previa
    dif_peso_pct_hembras            numeric,            -- (peso_h - guia peso_h) / guia peso_h * 100
    dif_peso_pct_machos             numeric,            -- (peso_m - guia peso_m) / guia peso_m * 100
    seleccion_pct_hembras           numeric,            -- % seleccion semana hembras = sel_h / aves_inicio_h * 100
    seleccion_pct_machos            numeric,            -- % seleccion semana machos  = sel_m / aves_inicio_m * 100
    error_sexaje_pct_hembras        numeric,            -- % error sexaje hembras = err_h / aves_inicio_h * 100
    error_sexaje_pct_machos         numeric             -- % error sexaje machos  = err_m / aves_inicio_m * 100
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza        text;
    v_anio        text;
    v_company     integer;
    v_aves_enc_col integer;   -- lotes.aves_encasetadas (crudo)
    v_hembras_l   integer;    -- lotes.hembras_l (crudo)
    v_machos_l    integer;    -- lotes.machos_l (crudo)
    v_aves_enc    double precision;   -- base total resuelta (con fallback)
    v_aves_enc_h  double precision;   -- base hembras resuelta
    v_aves_enc_m  double precision;   -- base machos resuelta
    v_peso_ini    double precision;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;   -- primer traslado_ingreso (fallback base)
    v_first_ing_m double precision;

    -- acumuladores (mismos nombres que el front)
    v_aves_acum       double precision;
    v_aves_acum_h     double precision;
    v_aves_acum_m     double precision;
    v_mort_bajas_acum double precision := 0;   -- bajas acumuladas (unidades) REQ-002f
    v_sel_bajas_acum  double precision := 0;   -- selección acumulada (unidades) REQ-002f
    v_peso_anterior   double precision;
    v_peso_tabla_ant  double precision := 0;

    v_max_sem     integer;
    s             integer;

    -- por semana
    r_mort_tot    double precision;
    r_sel_tot     double precision;
    r_cons_kg     double precision;
    r_err_tot     double precision;
    r_tras_sal    double precision;
    r_tras_ing    double precision;
    r_venta_tot   double precision;   -- venta de aves: sale del lote y no llega a ningún otro
    r_dias        integer;
    r_aves_fin    double precision;
    -- por semana / por género
    r_mort_h      double precision;
    r_mort_m      double precision;
    r_sel_h       double precision;
    r_sel_m       double precision;
    r_err_h       double precision;
    r_err_m       double precision;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_tras_sal_h  double precision;
    r_tras_sal_m  double precision;
    r_tras_ing_h  double precision;
    r_tras_ing_m  double precision;
    r_venta_h     double precision;
    r_venta_m     double precision;
    r_aves_fin_h  double precision;
    r_aves_fin_m  double precision;
    r_aves_prom_h double precision;
    r_aves_prom_m double precision;
    r_cons_dia_h  double precision;
    r_cons_dia_m  double precision;
    r_cons_tabla_h double precision;
    r_cons_tabla_m double precision;
    -- REQ-010b: peso / mortalidad / retiro POR SEXO + guía por sexo.
    v_peso_ant_h   double precision;   -- arrastre peso hembras
    v_peso_ant_m   double precision;   -- arrastre peso machos
    r_peso_h       double precision;
    r_peso_m       double precision;
    r_peso_tabla_h double precision;
    r_peso_tabla_m double precision;
    r_mort_tabla_h double precision;
    r_mort_tabla_m double precision;
    -- De que tabla salio la fila de guia: 'compartida' (guia_genetica_sanmarino_colombia,
    -- >40 columnas) o 'propia' (guia_genetica_santa_reyes, 3 metricas y solo hembras).
    -- Ver el bloque de la guia mas abajo: gobierna si se coalescea a 0 o se deja NULL.
    v_origen_guia  text;
    -- ¿La EMPRESA del lote tiene guia propia (tabla reducida)? Distinto de v_origen_guia, que
    -- dice de donde salio LA FILA de esta semana y queda NULL cuando no hubo ninguna. Se necesita
    -- separado para el caso «empresa con guia propia + semana sin fila»: ahi un 0 seria un
    -- objetivo inventado (su guia arranca en la semana 18 y no cubre todo el levante).
    v_guia_propia_empresa boolean := false;
    r_mort_pct_h   double precision;
    r_mort_pct_m   double precision;
    r_retiro_pct_h double precision;
    r_retiro_pct_m double precision;

    r_pH          double precision;
    r_pM          double precision;
    r_peso_prom   double precision;
    r_uH          double precision;
    r_uM          double precision;
    r_unif_real   double precision;
    r_cons_g      double precision;
    r_aves_prom   double precision;
    r_cons_dia    double precision;
    r_cons_tabla  double precision;
    r_peso_tabla  double precision;
    r_unif_tabla  double precision;
    r_mort_tabla  double precision;
    r_gan_sem     double precision;
    r_cons_ave    double precision;
    r_conv        double precision;
    r_gan_dia_ac  double precision;
    r_gan_tabla   double precision;
    r_mort_sem    double precision;
    r_sel_sem     double precision;
    r_err_sem     double precision;
    r_mort_mas_sel double precision;
    r_efic        double precision;
    r_superv      double precision;
    r_ip          double precision;
BEGIN
    SELECT l.raza, l.ano_tabla_genetica::text, l.company_id,
           l.aves_encasetadas, l.hembras_l, l.machos_l,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc_col, v_hembras_l, v_machos_l, v_peso_ini, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Una sola vez por lote: ¿esta empresa tiene guia propia? Gobierna el COALESCE a 0 de mas
    -- abajo. Para las cuatro empresas que leen la guia compartida da FALSE, y la expresion que
    -- se ejecuta queda identica a la de siempre.
    SELECT EXISTS (SELECT 1 FROM guia_genetica_santa_reyes gp
                    WHERE gp.company_id = v_company AND gp.deleted_at IS NULL)
      INTO v_guia_propia_empresa;

    -- Aves entradas por traslado en filas que el armado de la serie DESCARTA (puro traslado
    -- > sem 25): fallback de base cuando el lote se pobló por traslado y no trae
    -- aves_encasetadas / hembras_l / machos_l. Nadie más suma esas aves — la ventana las tira.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) del armado de la serie. Si acá
    --    entrara una fila que sí se procesa, sus aves contarían DOS veces (base + ingreso).
    -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de días distintos,
    -- y con LIMIT 1 el sexo ausente de la fila más antigua quedaba con base 0 ⇒ saldo negativo.
    SELECT COALESCE(SUM(COALESCE(sl.traslado_ingreso_hembras,0)),0)::double precision,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_machos,0)),0)::double precision
      INTO v_first_ing_h, v_first_ing_m
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
       AND (floor(((( sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date) / 7.0))::int) + 1 > 25
       AND COALESCE(sl.mortalidad_hembras,0) = 0 AND COALESCE(sl.mortalidad_machos,0) = 0
       AND COALESCE(sl.sel_h,0) = 0 AND COALESCE(sl.sel_m,0) = 0
       AND COALESCE(sl.error_sexaje_hembras,0) = 0 AND COALESCE(sl.error_sexaje_machos,0) = 0
       AND COALESCE(sl.consumo_kg_hembras,0) = 0 AND COALESCE(sl.consumo_kg_machos,0) = 0
       AND COALESCE(sl.peso_prom_hembras,0) = 0 AND COALESCE(sl.peso_prom_machos,0) = 0
       AND COALESCE(sl.venta_aves_hembras,0) = 0 AND COALESCE(sl.venta_aves_machos,0) = 0
       AND (COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0;
    v_first_ing_h := COALESCE(v_first_ing_h, 0);
    v_first_ing_m := COALESCE(v_first_ing_m, 0);

    -- Primer registro (calendario Bogotá) para validar el encaset.
    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;   -- sin registros

    -- REQ-002B36: encaset ausente o POSTERIOR al primer registro (futuro) ⇒
    -- datos inconsistentes ⇒ cero filas (el front muestra su empty-state).
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    -- Base de aves con fallback (REQ-002B36).
    v_aves_enc := COALESCE(
        NULLIF(v_aves_enc_col, 0)::double precision,
        NULLIF(COALESCE(v_hembras_l,0) + COALESCE(v_machos_l,0), 0)::double precision,
        NULLIF(v_first_ing_h + v_first_ing_m, 0),
        0);
    v_aves_enc_h := COALESCE(
        NULLIF(v_hembras_l, 0)::double precision,
        NULLIF(v_first_ing_h, 0),
        0);
    v_aves_enc_m := COALESCE(
        NULLIF(v_machos_l, 0)::double precision,
        NULLIF(v_first_ing_m, 0),
        0);

    v_aves_acum     := v_aves_enc;
    v_aves_acum_h   := v_aves_enc_h;
    v_aves_acum_m   := v_aves_enc_m;
    v_peso_anterior := v_peso_ini;
    v_peso_ant_h    := NULLIF(v_peso_ini, 0);   -- peso_inicial_h como base hembras (NULL si 0)
    v_peso_ant_m    := NULL;                     -- no hay peso_inicial_m ⇒ arranca NULL

    -- Semana de cada registro (calendario local Bogotá). real_sem = semana real
    -- (sin clamp inferior: el guard de encaset ya garantiza real_sem >= 1).
    -- LEAST(25,…) sólo topa por arriba filas de DATOS legítimos > 25 (no existen
    -- en levante); las filas de PURO traslado > 25 se EXCLUYEN (REQ-002f).
    DROP TABLE IF EXISTS _seg_sem;
    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
    WITH base AS (
        SELECT
            (floor((( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0))::int) + 1 AS real_sem,
            (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
            COALESCE(sl.mortalidad_hembras,0) AS mort_h,
            COALESCE(sl.mortalidad_machos,0)  AS mort_m,
            COALESCE(sl.sel_h,0)              AS sel_h,
            COALESCE(sl.sel_m,0)              AS sel_m,
            COALESCE(sl.error_sexaje_hembras,0) AS err_h,
            COALESCE(sl.error_sexaje_machos,0)  AS err_m,
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,   -- numeric
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,   -- numeric
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves (2026-08-17): salen del lote igual que un traslado de salida, pero no
            -- llegan a ningún otro lote. Se usan los splits por sexo —no `venta_aves_cantidad`—
            -- porque el saldo también se lleva por sexo; es el mismo criterio de
            -- `fn_resumen_semanal_ra_pesadas_levante`, y el mixto se arma como h+m igual que
            -- mort/sel/err/traslados.
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            COALESCE(sl.uniformidad_hembras,0) AS uh,
            COALESCE(sl.uniformidad_machos,0)  AS um,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem)                       AS sem,
        reg_date,
        (mort_h + mort_m)                         AS mort,
        (sel_h + sel_m)                           AS sel,
        (cons_kg_h_num + cons_kg_m_num)           AS cons_kg,   -- numeric (idéntico al original)
        (err_h + err_m)                           AS err,
        (tras_sal_h + tras_sal_m)                 AS tras_sal,
        (tras_ing_h + tras_ing_m)                 AS tras_ing,
        (venta_h + venta_m)                       AS venta,
        mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision           AS cons_kg_h,
        cons_kg_m_num::double precision           AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        venta_h, venta_m,
        ph, pm, uh, um, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        -- Una fila que trae VENTA no es «puro traslado»: descartarla perdería esas aves, que es el
        -- defecto que este cambio viene a cerrar. El mismo término se agrega al predicado gemelo de
        -- `v_first_ing_*` — los dos tienen que seguir siendo idénticos o las aves cuentan dos veces.
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        -- ¿la semana tiene registros? (el front solo itera semanas presentes)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0),
               COALESCE(SUM(sel_h),0),  COALESCE(SUM(sel_m),0),
               COALESCE(SUM(err_h),0),  COALESCE(SUM(err_m),0),
               COALESCE(SUM(cons_kg_h),0), COALESCE(SUM(cons_kg_m),0),
               COALESCE(SUM(tras_sal_h),0), COALESCE(SUM(tras_sal_m),0),
               COALESCE(SUM(tras_ing_h),0), COALESCE(SUM(tras_ing_m),0),
               COALESCE(SUM(venta),0), COALESCE(SUM(venta_h),0), COALESCE(SUM(venta_m),0)
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias,
               r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_cons_kg_h, r_cons_kg_m, r_tras_sal_h, r_tras_sal_m, r_tras_ing_h, r_tras_ing_m,
               r_venta_tot, r_venta_h, r_venta_m
          FROM _seg_sem WHERE sem = s;

        -- Saldo físico Feature-13: salidas = mort + sel + err + traslado_salida + VENTA - traslado_ingreso.
        --
        -- ⭐ 2026-08-17: la VENTA entró acá. Antes esta fn era el único lector del saldo de levante
        -- que no la descontaba, así que el mismo lote y la misma semana mostraban dos conteos según
        -- la pantalla (lote 143 sem 24: 10.619 acá contra 10.329 en `fn_reporte_semanal_levante_extras`,
        -- diferencia = la venta acumulada). Una ave vendida sale del lote: no contarla infla el saldo
        -- y, en cascada, subestima el consumo por ave — el mismo mecanismo por el que en su momento
        -- hubo que sumar el error de sexaje. La especificación ejecutable es
        -- `SaldoAvesLevanteCalculos.BajasNetas`, que ya la incluía.
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal - r_venta_tot + r_tras_ing;
        -- Saldo por género (REQ-002e). Por sexo se usan los splits dedicados, no `venta_aves_cantidad`.
        r_aves_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_aves_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        -- Pesaje: último registro (por fecha, luego id) de la semana con peso>0.
        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem
         WHERE sem = s AND (ph > 0 OR pm > 0)
         ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0);
        r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);

        r_peso_prom := CASE WHEN r_pH > 0 AND r_pM > 0 THEN (r_pH + r_pM)/2
                            WHEN r_pH > 0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH > 0 AND r_uM > 0 THEN (r_uH + r_uM)/2
                            WHEN r_uH > 0 THEN r_uH ELSE r_uM END;

        -- Peso por sexo (REQ-010b): valor del pesaje del sexo; arrastre del último conocido
        -- cuando la semana no tiene pesaje del sexo (mismo criterio que el peso mixto, que
        -- también arrastra). NULL si nunca hubo pesaje del sexo (p.ej. machos sin pesaje ⇒
        -- serie vacía en el chart, degrada con spanGaps).
        r_peso_h := CASE WHEN r_pH > 0 THEN r_pH ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pM > 0 THEN r_pM ELSE v_peso_ant_m END;

        r_cons_g    := r_cons_kg * 1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia  := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        -- Consumo real por sexo (g/ave/día): consumo_kg_sexo*1000 / saldo_prom_sexo / días.
        r_aves_prom_h := (v_aves_acum_h + r_aves_fin_h)/2;
        r_aves_prom_m := (v_aves_acum_m + r_aves_fin_m)/2;
        r_cons_dia_h  := CASE WHEN r_aves_prom_h > 0 AND r_dias > 0
                              THEN (r_cons_kg_h*1000)/(r_aves_prom_h*r_dias) ELSE NULL END;
        r_cons_dia_m  := CASE WHEN r_aves_prom_m > 0 AND r_dias > 0
                              THEN (r_cons_kg_m*1000)/(r_aves_prom_m*r_dias) ELSE NULL END;

        -- Guía real para la semana. Mixto (compat) + por sexo SIN promediar (REQ-002e).
        --
        -- 🔴 EL PROMEDIO MIXTO NO SE PUEDE APLICAR A UNA GUÍA DE SOLO HEMBRAS.
        -- Las tres expresiones mixtas hacen COALESCE de cada término y dividen por 2 FIJO.
        -- Con la guía reducida —que trae hembras y NO machos— eso da (95.00 + 0)/2 = 47,5
        -- donde el cliente dice 95,00: no es NULL, no es 0, no revienta. Es un número
        -- plausible y equivocado por un factor de 2, que nadie detecta mirando la pantalla.
        -- Por eso el promedio se aplica SOLO cuando la fila viene de la guía compartida;
        -- para la propia se usa el valor de hembras tal cual, que es el único que existe.
        -- La rama 'compartida' es LITERALMENTE la expresión de siempre ⇒ delta cero por
        -- construcción para Sanmarino, Demo, Ecuador y Panamá, no «verificado después».
        SELECT CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.gr_ave_dia_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.peso_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.uniformidad),'')::double precision
                    ELSE COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0) END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.mort_sem_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2 END,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               g.origen
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla, r_cons_tabla_h, r_cons_tabla_m,
               r_peso_tabla_h, r_peso_tabla_m, r_mort_tabla_h, r_mort_tabla_m, v_origen_guia
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = v_company
           -- ⚠️ La comparacion de raza de la rama COMPARTIDA queda EXACTA y case-sensitive, como
           -- siempre: aflojarla haria matchear filas que hoy no matchean para Sanmarino, Demo,
           -- Ecuador y Panama, o sea el refactor cambiaria resultados por si solo. La rama PROPIA
           -- —inalcanzable para esas cuatro— si compara normalizado, porque produccion ya lo hace
           -- y tenerlo de un solo lado era la causa medida de que `CRIOLLA` cruzara en produccion
           -- y no en levante (30-ago-2026). La grafia del ERP la resuelve la vista, con su alias.
           AND (CASE WHEN g.origen = 'propia'
                     THEN btrim(lower(g.raza)) = btrim(lower(v_raza))
                     ELSE g.raza = v_raza END)
           AND g.anio_guia = v_anio
           AND btrim(g.edad) = s::text
         LIMIT 1;
        -- El COALESCE a 0 también es exclusivo de la guía compartida: ahí la columna existe en
        -- toda la curva y el 0 se lee como «la guía dice 0». En la propia la métrica NO EXISTE
        -- (no trae peso, ni uniformidad, ni mortalidad semanal — su retiro_ac_h es ACUMULADO),
        -- y un 0 ahí se leería como un objetivo real. NULL es la única lectura honesta, y el
        -- front ya lo sabe pintar: las series por sexo llegan NULL desde siempre.
        -- `AND NOT v_guia_propia_empresa`: sin eso, una semana SIN fila de guia (v_origen_guia
        -- NULL) caia igual en el COALESCE. Para una empresa con guia propia eso pinta 0,00 en las
        -- cuatro columnas de guia —un objetivo inventado— justo donde su guia no llega: la de
        -- Santa Reyes arranca en la semana 18 y el levante empieza en la 1. Medido el 30-ago-2026.
        -- Para las cuatro empresas sin guia propia la condicion nueva es siempre TRUE ⇒ la misma
        -- expresion de hoy, incluido el 0 legitimo cuando la guia compartida trae la columna vacia.
        IF v_origen_guia IS DISTINCT FROM 'propia' AND NOT v_guia_propia_empresa THEN
            r_cons_tabla := COALESCE(r_cons_tabla,0);
            r_peso_tabla := COALESCE(r_peso_tabla,0);
            r_unif_tabla := COALESCE(r_unif_tabla,0);
            r_mort_tabla := COALESCE(r_mort_tabla,0);
        END IF;
        -- r_cons_tabla_h/_m, r_peso_tabla_h/_m, r_mort_tabla_h/_m se dejan NULL si la guía
        -- no trae el dato del sexo (series de guía degradan a NULL, sin promediar).

        r_gan_sem   := r_peso_prom - v_peso_anterior;
        r_cons_ave  := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv      := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem  := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem   := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem   := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        -- REQ-010b: mortalidad y retiro POR SEXO. Mismo denominador que el mixto (aves al inicio
        -- de la semana del sexo). El retiro replica el mixto retiroSem = mort+sel+errSex del sexo.
        -- NULL (no 0 sintético) cuando el sexo no tiene saldo ⇒ la serie degrada con spanGaps.
        r_mort_pct_h   := CASE WHEN v_aves_acum_h > 0 THEN (r_mort_h / v_aves_acum_h) * 100 ELSE NULL END;
        r_mort_pct_m   := CASE WHEN v_aves_acum_m > 0 THEN (r_mort_m / v_aves_acum_m) * 100 ELSE NULL END;
        r_retiro_pct_h := CASE WHEN v_aves_acum_h > 0 THEN ((r_mort_h + r_sel_h + r_err_h) / v_aves_acum_h) * 100 ELSE NULL END;
        r_retiro_pct_m := CASE WHEN v_aves_acum_m > 0 THEN ((r_mort_m + r_sel_m + r_err_m) / v_aves_acum_m) * 100 ELSE NULL END;

        r_efic   := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip     := r_efic * r_superv;

        -- REQ-002f: acumulados reales = bajas_acumuladas / aves_encasetadas * 100.
        v_mort_bajas_acum := v_mort_bajas_acum + r_mort_tot;
        v_sel_bajas_acum  := v_sel_bajas_acum + r_sel_tot;

        semana                        := s;
        aves_inicio_semana            := v_aves_acum;
        aves_fin_semana               := r_aves_fin;
        consumo_diario                := r_cons_dia;
        consumo_tabla                 := r_cons_tabla;
        consumo_total_semana          := r_cons_g;
        conversion_alimenticia        := r_conv;
        peso_tabla                    := r_peso_tabla;
        unif_real                     := r_unif_real;
        unif_tabla                    := r_unif_tabla;
        mort_tabla                    := r_mort_tabla;
        dif_peso_pct                  := CASE WHEN r_peso_tabla > 0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana               := r_gan_sem;
        ganancia_diaria_acumulada     := r_gan_dia_ac;
        ganancia_tabla                := r_gan_tabla;
        mortalidad_sem                := r_mort_sem;
        seleccion_sem                 := r_sel_sem;
        error_sexaje_sem              := r_err_sem;
        mortalidad_mas_seleccion      := r_mort_mas_sel;
        eficiencia                    := r_efic;
        ip                            := r_ip;
        vpi                           := r_ip;   -- front: vpi = supervivencia*eficiencia = ip
        saldo_aves_semanal            := r_aves_fin;
        mortalidad_acum               := CASE WHEN v_aves_enc > 0 THEN (v_mort_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        seleccion_acum                := CASE WHEN v_aves_enc > 0 THEN (v_sel_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        mortalidad_mas_seleccion_acum := CASE WHEN v_aves_enc > 0 THEN ((v_mort_bajas_acum + v_sel_bajas_acum)/v_aves_enc)*100 ELSE 0 END;
        piso_termico_visible          := false;  -- la guía no expone el flag; front daba false
        peso_inicial                  := v_peso_anterior;
        peso_cierre                   := r_peso_prom;
        dias_con_registro             := r_dias;
        consumo_diario_hembras        := r_cons_dia_h;
        consumo_diario_machos         := r_cons_dia_m;
        consumo_tabla_hembras         := r_cons_tabla_h;
        consumo_tabla_machos          := r_cons_tabla_m;
        peso_hembras                  := r_peso_h;
        peso_machos                   := r_peso_m;
        peso_tabla_hembras            := r_peso_tabla_h;
        peso_tabla_machos             := r_peso_tabla_m;
        mort_pct_hembras              := r_mort_pct_h;
        mort_pct_machos               := r_mort_pct_m;
        mort_tabla_hembras            := r_mort_tabla_h;
        mort_tabla_machos             := r_mort_tabla_m;
        retiro_pct_hembras            := r_retiro_pct_h;
        retiro_pct_machos             := r_retiro_pct_m;

        -- TK-2026-000022 — el resto de los parametros por sexo. Ninguno introduce aritmetica
        -- nueva: son las MISMAS variables con las que ya se arman las columnas mixtas, expuestas
        -- sin promediar. El criterio de NULL es el de las series por sexo de arriba: si el sexo no
        -- tiene saldo (o la semana no tuvo pesaje / la guia no trae el dato) va NULL, para que la
        -- pantalla muestre un guion en vez de un cero que se leeria como dato real.
        aves_inicio_hembras           := CASE WHEN v_aves_enc_h > 0 THEN v_aves_acum_h ELSE NULL END;
        aves_fin_hembras              := CASE WHEN v_aves_enc_h > 0 THEN r_aves_fin_h  ELSE NULL END;
        aves_inicio_machos            := CASE WHEN v_aves_enc_m > 0 THEN v_aves_acum_m ELSE NULL END;
        aves_fin_machos               := CASE WHEN v_aves_enc_m > 0 THEN r_aves_fin_m  ELSE NULL END;
        consumo_total_semana_hembras  := CASE WHEN v_aves_enc_h > 0 THEN r_cons_kg_h * 1000 ELSE NULL END;
        consumo_total_semana_machos   := CASE WHEN v_aves_enc_m > 0 THEN r_cons_kg_m * 1000 ELSE NULL END;
        -- Uniformidad: 0 significa "no hubo pesaje esta semana", no "0 % de uniformidad".
        unif_hembras                  := CASE WHEN r_uH > 0 THEN r_uH ELSE NULL END;
        unif_machos                   := CASE WHEN r_uM > 0 THEN r_uM ELSE NULL END;
        ganancia_hembras              := CASE WHEN r_peso_h IS NOT NULL AND v_peso_ant_h IS NOT NULL
                                              THEN r_peso_h - v_peso_ant_h ELSE NULL END;
        ganancia_machos               := CASE WHEN r_peso_m IS NOT NULL AND v_peso_ant_m IS NOT NULL
                                              THEN r_peso_m - v_peso_ant_m ELSE NULL END;
        dif_peso_pct_hembras          := CASE WHEN r_peso_tabla_h > 0 AND r_peso_h IS NOT NULL
                                              THEN ((r_peso_h - r_peso_tabla_h)/r_peso_tabla_h)*100 ELSE NULL END;
        dif_peso_pct_machos           := CASE WHEN r_peso_tabla_m > 0 AND r_peso_m IS NOT NULL
                                              THEN ((r_peso_m - r_peso_tabla_m)/r_peso_tabla_m)*100 ELSE NULL END;
        seleccion_pct_hembras         := CASE WHEN v_aves_acum_h > 0 THEN (r_sel_h / v_aves_acum_h) * 100 ELSE NULL END;
        seleccion_pct_machos          := CASE WHEN v_aves_acum_m > 0 THEN (r_sel_m / v_aves_acum_m) * 100 ELSE NULL END;
        error_sexaje_pct_hembras      := CASE WHEN v_aves_acum_h > 0 THEN (r_err_h / v_aves_acum_h) * 100 ELSE NULL END;
        error_sexaje_pct_machos       := CASE WHEN v_aves_acum_m > 0 THEN (r_err_m / v_aves_acum_m) * 100 ELSE NULL END;

        RETURN NEXT;

        -- avanzar acumuladores (idéntico al front) + saldo por género.
        v_aves_acum      := r_aves_fin;
        v_aves_acum_h    := r_aves_fin_h;
        v_aves_acum_m    := r_aves_fin_m;
        v_peso_anterior  := r_peso_prom;
        v_peso_tabla_ant := r_peso_tabla;
        v_peso_ant_h     := r_peso_h;   -- arrastre peso por sexo (REQ-010b)
        v_peso_ant_m     := r_peso_m;
    END LOOP;

    RETURN;
END;
$$;
""";

        /// <summary>fn_indicadores_levante_postura PREVIA (HEAD), verbatim.</summary>
        private const string FnIndicadoresLevantePosturaPrevia = """
-- ============================================================================
-- fn_indicadores_levante_postura(lote_id)
-- Indicadores semanales de LEVANTE (postura Colombia) calculados en la BD.
-- Reemplaza el cómputo del front (lote-levante/tabla-lista-indicadores +
-- graficas-principal): el front solo debe pintar.
--
-- Replica EXACTO el algoritmo del front (double precision, mismo orden) e
-- incorpora las correcciones ya acordadas:
--   * Peso/uniformidad del PESAJE semanal: último registro de la semana con
--     peso>0 (no el último día, que suele venir en 0) + arrastre del último
--     peso conocido cuando la semana no tiene pesaje (evita ganancia negativa
--     y dif -100%).  [bug histórico corregido]
--   * Guía genética REAL desde guia_genetica_sanmarino_colombia por
--     raza + año + company + semana (no valores hardcodeados / no Ecuador).
--
-- Correcciones matriz Verenice rev 6-jul-26:
--   * REQ-002e — Consumo por sexo: además del consumo mixto (compatibilidad),
--     se exponen consumo_diario_hembras / consumo_diario_machos (g/ave/día reales
--     por sexo = consumo_kg_sexo*1000 / saldo_prom_sexo / días) y
--     consumo_tabla_hembras / consumo_tabla_machos (gr_ave_dia_h/_m de la guía, SIN
--     promediar). Requiere llevar el saldo de aves POR GÉNERO dentro de la fn.
--     (Columnas renombradas de _h/_m a _hembras/_machos por el mapeo EF, ver nota abajo.)
--   * REQ-002f — Acumulados reales: mortalidad/selección acumuladas =
--     bajas_acumuladas / aves_encasetadas * 100 (acumulado real sobre aves
--     iniciales), no la suma de % semanales sobre base decreciente.
--   * REQ-002f/B36 — Semana fantasma: se EXCLUYEN las filas de PURO traslado
--     (sin mortalidad/selección/error/consumo/pesaje) posteriores a la
--     semana 25; ya no se clampean con LEAST(25) generando una "semana 25"
--     falsa con el salto de saldo del traslado post-levante.
--   * REQ-002B36 — Defensas:
--       - Base de aves con fallback: COALESCE(aves_encasetadas,
--         hembras_l+machos_l, primer traslado_ingreso, 0).
--       - Encaset futuro/ausente: si fecha_encaset es NULL o es POSTERIOR al
--         primer registro (encaset tecleado a futuro, p. ej. lote 116), se
--         devuelven CERO filas en lugar de colapsar 140+ días en una
--         "semana 1" absurda con base 0 y %pérdidas 100%. Se eligió devolver
--         cero filas (y no "usar el primer registro como referencia") porque
--         con un encaset inconsistente NINGÚN indicador es confiable: es más
--         seguro que el front muestre su empty-state a mostrar cifras
--         engañosas. Al devolver cero filas ya no hace falta GREATEST(1,…)
--         (no quedan semanas negativas que clampear).
--       - Idempotencia intra-transacción: DROP TABLE IF EXISTS _seg_sem antes
--         del CREATE TEMP TABLE (permite llamar la fn 2+ veces en la misma
--         transacción sin 'relation _seg_sem already exists').
--
-- Fuente de verdad del algoritmo: tabla-lista-indicadores.component.ts
-- Zona horaria: America/Bogota para el corte de semanas (calendario local).
--
-- Fase 3 (convergencia levante a Feature-13): lee la tabla CANÓNICA
-- seguimiento_diario_levante (tipo_seguimiento='levante') y las
-- salidas de la semana incluyen error de sexaje y traslados dedicados:
--   out = mort + sel + err + traslado_salida - traslado_ingreso;  aves_fin = aves - out.
-- ============================================================================
--   * REQ-010b — Series POR SEXO para el selector Hembras/Machos/Ambos de la
--     pestaña Gráfica: además del consumo por sexo, se exponen peso (real +
--     guía), mortalidad % (real + guía) y retiro % (real; la guía por sexo no
--     existe ⇒ NULL) por sexo, para que el control cambie las series Real/Guía.
--     Aritmética por sexo consistente con la mixta (mismo denominador = aves al
--     inicio de la semana del sexo; NULL cuando el sexo no tiene saldo/pesaje).
--
--   * TK-2026-000022 — TODOS los parametros por sexo en la TABLA de indicadores.
--     El usuario reporto que «los parametros aparecen solo para un grupo de aves y
--     no identifica si se refieren a hembras o machos». Peor: varias columnas
--     mixtas son un PROMEDIO ARITMETICO simple de los dos sexos (peso_cierre y
--     unif_real: (H+M)/2, sin ponderar por cantidad de aves), o sea un valor que
--     no le corresponde a ninguna ave del galpon —en reproductoras la hembra y el
--     macho tienen pesos muy distintos—. Se exponen aves inicio/fin, consumo total,
--     uniformidad, ganancia, dif % de peso vs guia, seleccion % y error de sexaje %
--     por sexo. NO se agrega aritmetica nueva: son las mismas variables internas
--     con las que ya se arman las columnas mixtas, publicadas sin promediar.
--
-- IMPORTANTE (mapeo EF): los nombres de las columnas por sexo son el snake_case
-- EXACTO de las props del DTO (…Hembras→…_hembras, …Machos→…_machos). EF Core
-- (SqlQueryRaw<IndicadorSemanalLevanteDto> con convención snake_case) mapea
-- ConsumoDiarioHembras↔consumo_diario_hembras, PesoHembras↔peso_hembras, etc.
-- Un nombre abreviado (_h/_m) NO mapearía a props …Hembras/…Machos (mismo patrón
-- probado en fn_indicadores_produccion_postura: porcentaje_mortalidad_hembras…).
-- Por eso las columnas de consumo por sexo se renombran de _h/_m a _hembras/_machos.
--
-- DROP previo: la firma cambió (se renombraron/agregaron columnas OUT por sexo),
-- y CREATE OR REPLACE no puede alterar el tipo de retorno.
DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    aves_inicio_semana              double precision,
    aves_fin_semana                 double precision,
    consumo_diario                  double precision,   -- g/ave/día real (mixto H+M)
    consumo_tabla                   double precision,   -- g/ave/día guía (promedio H,M)
    consumo_total_semana            double precision,   -- gramos
    conversion_alimenticia          double precision,
    peso_tabla                      double precision,
    unif_real                       double precision,
    unif_tabla                      double precision,
    mort_tabla                      double precision,
    dif_peso_pct                    double precision,
    ganancia_semana                 double precision,
    ganancia_diaria_acumulada       double precision,
    ganancia_tabla                  double precision,
    mortalidad_sem                  double precision,
    seleccion_sem                   double precision,
    error_sexaje_sem                double precision,
    mortalidad_mas_seleccion        double precision,
    eficiencia                      double precision,
    ip                              double precision,
    vpi                             double precision,
    saldo_aves_semanal              double precision,
    mortalidad_acum                 double precision,
    seleccion_acum                  double precision,
    mortalidad_mas_seleccion_acum   double precision,
    piso_termico_visible            boolean,
    peso_inicial                    double precision,
    peso_cierre                     double precision,
    dias_con_registro               integer,
    -- REQ-002e / REQ-010b: series POR SEXO (reales y guía SIN promediar). numeric → decimal? en el DTO.
    -- Nombres = snake_case EXACTO de las props del DTO para que EF las mapee (ver nota de cabecera).
    consumo_diario_hembras          numeric,            -- g/ave/día real hembras
    consumo_diario_machos           numeric,            -- g/ave/día real machos
    consumo_tabla_hembras           numeric,            -- gr_ave_dia_h de la guía
    consumo_tabla_machos            numeric,            -- gr_ave_dia_m de la guía
    peso_hembras                    numeric,            -- peso prom hembras (arrastre si semana sin pesaje)
    peso_machos                     numeric,            -- peso prom machos  (arrastre si semana sin pesaje)
    peso_tabla_hembras              numeric,            -- guía peso_h
    peso_tabla_machos               numeric,            -- guía peso_m
    mort_pct_hembras                numeric,            -- % mort semana hembras = mort_h / aves_inicio_h * 100
    mort_pct_machos                 numeric,            -- % mort semana machos  = mort_m / aves_inicio_m * 100
    mort_tabla_hembras              numeric,            -- guía mort_sem_h
    mort_tabla_machos               numeric,            -- guía mort_sem_m
    retiro_pct_hembras              numeric,            -- % retiro hembras = (mort+sel+err)_h / aves_inicio_h * 100
    retiro_pct_machos               numeric,            -- % retiro machos  = (mort+sel+err)_m / aves_inicio_m * 100
    -- TK-2026-000022: el resto de los parametros POR SEXO. La tabla de indicadores mostraba una
    -- sola serie sin decir de que sexo era —y varias de esas columnas mixtas son un PROMEDIO
    -- ARITMETICO de hembras y machos (peso, uniformidad), o sea un numero que no le corresponde a
    -- ninguna ave real. Todo esto ya se calculaba dentro de la funcion; solo faltaba exponerlo.
    -- Convencion identica a las de arriba: NULL cuando el sexo no existe en el lote o no hay dato,
    -- nunca 0 sintetico.
    aves_inicio_hembras             numeric,            -- saldo hembras al inicio de la semana
    aves_fin_hembras                numeric,            -- saldo hembras al cierre de la semana
    aves_inicio_machos              numeric,            -- saldo machos al inicio de la semana
    aves_fin_machos                 numeric,            -- saldo machos al cierre de la semana
    consumo_total_semana_hembras    numeric,            -- gramos consumidos por las hembras en la semana
    consumo_total_semana_machos     numeric,            -- gramos consumidos por los machos en la semana
    unif_hembras                    numeric,            -- % uniformidad hembras del pesaje de la semana
    unif_machos                     numeric,            -- % uniformidad machos  del pesaje de la semana
    ganancia_hembras                numeric,            -- g ganados por las hembras respecto de la semana previa
    ganancia_machos                 numeric,            -- g ganados por los machos  respecto de la semana previa
    dif_peso_pct_hembras            numeric,            -- (peso_h - guia peso_h) / guia peso_h * 100
    dif_peso_pct_machos             numeric,            -- (peso_m - guia peso_m) / guia peso_m * 100
    seleccion_pct_hembras           numeric,            -- % seleccion semana hembras = sel_h / aves_inicio_h * 100
    seleccion_pct_machos            numeric,            -- % seleccion semana machos  = sel_m / aves_inicio_m * 100
    error_sexaje_pct_hembras        numeric,            -- % error sexaje hembras = err_h / aves_inicio_h * 100
    error_sexaje_pct_machos         numeric             -- % error sexaje machos  = err_m / aves_inicio_m * 100
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza        text;
    v_anio        text;
    v_company     integer;
    v_aves_enc_col integer;   -- lotes.aves_encasetadas (crudo)
    v_hembras_l   integer;    -- lotes.hembras_l (crudo)
    v_machos_l    integer;    -- lotes.machos_l (crudo)
    v_aves_enc    double precision;   -- base total resuelta (con fallback)
    v_aves_enc_h  double precision;   -- base hembras resuelta
    v_aves_enc_m  double precision;   -- base machos resuelta
    v_peso_ini    double precision;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;   -- primer traslado_ingreso (fallback base)
    v_first_ing_m double precision;

    -- acumuladores (mismos nombres que el front)
    v_aves_acum       double precision;
    v_aves_acum_h     double precision;
    v_aves_acum_m     double precision;
    v_mort_bajas_acum double precision := 0;   -- bajas acumuladas (unidades) REQ-002f
    v_sel_bajas_acum  double precision := 0;   -- selección acumulada (unidades) REQ-002f
    v_peso_anterior   double precision;
    v_peso_tabla_ant  double precision := 0;

    v_max_sem     integer;
    s             integer;

    -- por semana
    r_mort_tot    double precision;
    r_sel_tot     double precision;
    r_cons_kg     double precision;
    r_err_tot     double precision;
    r_tras_sal    double precision;
    r_tras_ing    double precision;
    r_venta_tot   double precision;   -- venta de aves: sale del lote y no llega a ningún otro
    r_dias        integer;
    r_aves_fin    double precision;
    -- por semana / por género
    r_mort_h      double precision;
    r_mort_m      double precision;
    r_sel_h       double precision;
    r_sel_m       double precision;
    r_err_h       double precision;
    r_err_m       double precision;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_tras_sal_h  double precision;
    r_tras_sal_m  double precision;
    r_tras_ing_h  double precision;
    r_tras_ing_m  double precision;
    r_venta_h     double precision;
    r_venta_m     double precision;
    r_aves_fin_h  double precision;
    r_aves_fin_m  double precision;
    r_aves_prom_h double precision;
    r_aves_prom_m double precision;
    r_cons_dia_h  double precision;
    r_cons_dia_m  double precision;
    r_cons_tabla_h double precision;
    r_cons_tabla_m double precision;
    -- REQ-010b: peso / mortalidad / retiro POR SEXO + guía por sexo.
    v_peso_ant_h   double precision;   -- arrastre peso hembras
    v_peso_ant_m   double precision;   -- arrastre peso machos
    r_peso_h       double precision;
    r_peso_m       double precision;
    r_peso_tabla_h double precision;
    r_peso_tabla_m double precision;
    r_mort_tabla_h double precision;
    r_mort_tabla_m double precision;
    -- De que tabla salio la fila de guia: 'compartida' (guia_genetica_sanmarino_colombia,
    -- >40 columnas) o 'propia' (guia_genetica_santa_reyes, 3 metricas y solo hembras).
    -- Ver el bloque de la guia mas abajo: gobierna si se coalescea a 0 o se deja NULL.
    v_origen_guia  text;
    r_mort_pct_h   double precision;
    r_mort_pct_m   double precision;
    r_retiro_pct_h double precision;
    r_retiro_pct_m double precision;

    r_pH          double precision;
    r_pM          double precision;
    r_peso_prom   double precision;
    r_uH          double precision;
    r_uM          double precision;
    r_unif_real   double precision;
    r_cons_g      double precision;
    r_aves_prom   double precision;
    r_cons_dia    double precision;
    r_cons_tabla  double precision;
    r_peso_tabla  double precision;
    r_unif_tabla  double precision;
    r_mort_tabla  double precision;
    r_gan_sem     double precision;
    r_cons_ave    double precision;
    r_conv        double precision;
    r_gan_dia_ac  double precision;
    r_gan_tabla   double precision;
    r_mort_sem    double precision;
    r_sel_sem     double precision;
    r_err_sem     double precision;
    r_mort_mas_sel double precision;
    r_efic        double precision;
    r_superv      double precision;
    r_ip          double precision;
BEGIN
    SELECT l.raza, l.ano_tabla_genetica::text, l.company_id,
           l.aves_encasetadas, l.hembras_l, l.machos_l,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc_col, v_hembras_l, v_machos_l, v_peso_ini, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Aves entradas por traslado en filas que el armado de la serie DESCARTA (puro traslado
    -- > sem 25): fallback de base cuando el lote se pobló por traslado y no trae
    -- aves_encasetadas / hembras_l / machos_l. Nadie más suma esas aves — la ventana las tira.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) del armado de la serie. Si acá
    --    entrara una fila que sí se procesa, sus aves contarían DOS veces (base + ingreso).
    -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de días distintos,
    -- y con LIMIT 1 el sexo ausente de la fila más antigua quedaba con base 0 ⇒ saldo negativo.
    SELECT COALESCE(SUM(COALESCE(sl.traslado_ingreso_hembras,0)),0)::double precision,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_machos,0)),0)::double precision
      INTO v_first_ing_h, v_first_ing_m
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
       AND (floor(((( sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date) / 7.0))::int) + 1 > 25
       AND COALESCE(sl.mortalidad_hembras,0) = 0 AND COALESCE(sl.mortalidad_machos,0) = 0
       AND COALESCE(sl.sel_h,0) = 0 AND COALESCE(sl.sel_m,0) = 0
       AND COALESCE(sl.error_sexaje_hembras,0) = 0 AND COALESCE(sl.error_sexaje_machos,0) = 0
       AND COALESCE(sl.consumo_kg_hembras,0) = 0 AND COALESCE(sl.consumo_kg_machos,0) = 0
       AND COALESCE(sl.peso_prom_hembras,0) = 0 AND COALESCE(sl.peso_prom_machos,0) = 0
       AND COALESCE(sl.venta_aves_hembras,0) = 0 AND COALESCE(sl.venta_aves_machos,0) = 0
       AND (COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0;
    v_first_ing_h := COALESCE(v_first_ing_h, 0);
    v_first_ing_m := COALESCE(v_first_ing_m, 0);

    -- Primer registro (calendario Bogotá) para validar el encaset.
    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;   -- sin registros

    -- REQ-002B36: encaset ausente o POSTERIOR al primer registro (futuro) ⇒
    -- datos inconsistentes ⇒ cero filas (el front muestra su empty-state).
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    -- Base de aves con fallback (REQ-002B36).
    v_aves_enc := COALESCE(
        NULLIF(v_aves_enc_col, 0)::double precision,
        NULLIF(COALESCE(v_hembras_l,0) + COALESCE(v_machos_l,0), 0)::double precision,
        NULLIF(v_first_ing_h + v_first_ing_m, 0),
        0);
    v_aves_enc_h := COALESCE(
        NULLIF(v_hembras_l, 0)::double precision,
        NULLIF(v_first_ing_h, 0),
        0);
    v_aves_enc_m := COALESCE(
        NULLIF(v_machos_l, 0)::double precision,
        NULLIF(v_first_ing_m, 0),
        0);

    v_aves_acum     := v_aves_enc;
    v_aves_acum_h   := v_aves_enc_h;
    v_aves_acum_m   := v_aves_enc_m;
    v_peso_anterior := v_peso_ini;
    v_peso_ant_h    := NULLIF(v_peso_ini, 0);   -- peso_inicial_h como base hembras (NULL si 0)
    v_peso_ant_m    := NULL;                     -- no hay peso_inicial_m ⇒ arranca NULL

    -- Semana de cada registro (calendario local Bogotá). real_sem = semana real
    -- (sin clamp inferior: el guard de encaset ya garantiza real_sem >= 1).
    -- LEAST(25,…) sólo topa por arriba filas de DATOS legítimos > 25 (no existen
    -- en levante); las filas de PURO traslado > 25 se EXCLUYEN (REQ-002f).
    DROP TABLE IF EXISTS _seg_sem;
    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
    WITH base AS (
        SELECT
            (floor((( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0))::int) + 1 AS real_sem,
            (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
            COALESCE(sl.mortalidad_hembras,0) AS mort_h,
            COALESCE(sl.mortalidad_machos,0)  AS mort_m,
            COALESCE(sl.sel_h,0)              AS sel_h,
            COALESCE(sl.sel_m,0)              AS sel_m,
            COALESCE(sl.error_sexaje_hembras,0) AS err_h,
            COALESCE(sl.error_sexaje_machos,0)  AS err_m,
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,   -- numeric
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,   -- numeric
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves (2026-08-17): salen del lote igual que un traslado de salida, pero no
            -- llegan a ningún otro lote. Se usan los splits por sexo —no `venta_aves_cantidad`—
            -- porque el saldo también se lleva por sexo; es el mismo criterio de
            -- `fn_resumen_semanal_ra_pesadas_levante`, y el mixto se arma como h+m igual que
            -- mort/sel/err/traslados.
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            COALESCE(sl.uniformidad_hembras,0) AS uh,
            COALESCE(sl.uniformidad_machos,0)  AS um,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem)                       AS sem,
        reg_date,
        (mort_h + mort_m)                         AS mort,
        (sel_h + sel_m)                           AS sel,
        (cons_kg_h_num + cons_kg_m_num)           AS cons_kg,   -- numeric (idéntico al original)
        (err_h + err_m)                           AS err,
        (tras_sal_h + tras_sal_m)                 AS tras_sal,
        (tras_ing_h + tras_ing_m)                 AS tras_ing,
        (venta_h + venta_m)                       AS venta,
        mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision           AS cons_kg_h,
        cons_kg_m_num::double precision           AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        venta_h, venta_m,
        ph, pm, uh, um, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        -- Una fila que trae VENTA no es «puro traslado»: descartarla perdería esas aves, que es el
        -- defecto que este cambio viene a cerrar. El mismo término se agrega al predicado gemelo de
        -- `v_first_ing_*` — los dos tienen que seguir siendo idénticos o las aves cuentan dos veces.
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        -- ¿la semana tiene registros? (el front solo itera semanas presentes)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0),
               COALESCE(SUM(sel_h),0),  COALESCE(SUM(sel_m),0),
               COALESCE(SUM(err_h),0),  COALESCE(SUM(err_m),0),
               COALESCE(SUM(cons_kg_h),0), COALESCE(SUM(cons_kg_m),0),
               COALESCE(SUM(tras_sal_h),0), COALESCE(SUM(tras_sal_m),0),
               COALESCE(SUM(tras_ing_h),0), COALESCE(SUM(tras_ing_m),0),
               COALESCE(SUM(venta),0), COALESCE(SUM(venta_h),0), COALESCE(SUM(venta_m),0)
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias,
               r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_cons_kg_h, r_cons_kg_m, r_tras_sal_h, r_tras_sal_m, r_tras_ing_h, r_tras_ing_m,
               r_venta_tot, r_venta_h, r_venta_m
          FROM _seg_sem WHERE sem = s;

        -- Saldo físico Feature-13: salidas = mort + sel + err + traslado_salida + VENTA - traslado_ingreso.
        --
        -- ⭐ 2026-08-17: la VENTA entró acá. Antes esta fn era el único lector del saldo de levante
        -- que no la descontaba, así que el mismo lote y la misma semana mostraban dos conteos según
        -- la pantalla (lote 143 sem 24: 10.619 acá contra 10.329 en `fn_reporte_semanal_levante_extras`,
        -- diferencia = la venta acumulada). Una ave vendida sale del lote: no contarla infla el saldo
        -- y, en cascada, subestima el consumo por ave — el mismo mecanismo por el que en su momento
        -- hubo que sumar el error de sexaje. La especificación ejecutable es
        -- `SaldoAvesLevanteCalculos.BajasNetas`, que ya la incluía.
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal - r_venta_tot + r_tras_ing;
        -- Saldo por género (REQ-002e). Por sexo se usan los splits dedicados, no `venta_aves_cantidad`.
        r_aves_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_aves_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        -- Pesaje: último registro (por fecha, luego id) de la semana con peso>0.
        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem
         WHERE sem = s AND (ph > 0 OR pm > 0)
         ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0);
        r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);

        r_peso_prom := CASE WHEN r_pH > 0 AND r_pM > 0 THEN (r_pH + r_pM)/2
                            WHEN r_pH > 0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH > 0 AND r_uM > 0 THEN (r_uH + r_uM)/2
                            WHEN r_uH > 0 THEN r_uH ELSE r_uM END;

        -- Peso por sexo (REQ-010b): valor del pesaje del sexo; arrastre del último conocido
        -- cuando la semana no tiene pesaje del sexo (mismo criterio que el peso mixto, que
        -- también arrastra). NULL si nunca hubo pesaje del sexo (p.ej. machos sin pesaje ⇒
        -- serie vacía en el chart, degrada con spanGaps).
        r_peso_h := CASE WHEN r_pH > 0 THEN r_pH ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pM > 0 THEN r_pM ELSE v_peso_ant_m END;

        r_cons_g    := r_cons_kg * 1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia  := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        -- Consumo real por sexo (g/ave/día): consumo_kg_sexo*1000 / saldo_prom_sexo / días.
        r_aves_prom_h := (v_aves_acum_h + r_aves_fin_h)/2;
        r_aves_prom_m := (v_aves_acum_m + r_aves_fin_m)/2;
        r_cons_dia_h  := CASE WHEN r_aves_prom_h > 0 AND r_dias > 0
                              THEN (r_cons_kg_h*1000)/(r_aves_prom_h*r_dias) ELSE NULL END;
        r_cons_dia_m  := CASE WHEN r_aves_prom_m > 0 AND r_dias > 0
                              THEN (r_cons_kg_m*1000)/(r_aves_prom_m*r_dias) ELSE NULL END;

        -- Guía real para la semana. Mixto (compat) + por sexo SIN promediar (REQ-002e).
        --
        -- 🔴 EL PROMEDIO MIXTO NO SE PUEDE APLICAR A UNA GUÍA DE SOLO HEMBRAS.
        -- Las tres expresiones mixtas hacen COALESCE de cada término y dividen por 2 FIJO.
        -- Con la guía reducida —que trae hembras y NO machos— eso da (95.00 + 0)/2 = 47,5
        -- donde el cliente dice 95,00: no es NULL, no es 0, no revienta. Es un número
        -- plausible y equivocado por un factor de 2, que nadie detecta mirando la pantalla.
        -- Por eso el promedio se aplica SOLO cuando la fila viene de la guía compartida;
        -- para la propia se usa el valor de hembras tal cual, que es el único que existe.
        -- La rama 'compartida' es LITERALMENTE la expresión de siempre ⇒ delta cero por
        -- construcción para Sanmarino, Demo, Ecuador y Panamá, no «verificado después».
        SELECT CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.gr_ave_dia_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.peso_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.uniformidad),'')::double precision
                    ELSE COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0) END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.mort_sem_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2 END,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               g.origen
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla, r_cons_tabla_h, r_cons_tabla_m,
               r_peso_tabla_h, r_peso_tabla_m, r_mort_tabla_h, r_mort_tabla_m, v_origen_guia
          FROM vw_guia_genetica_postura g
         WHERE g.raza = v_raza AND g.anio_guia = v_anio AND g.company_id = v_company
           AND btrim(g.edad) = s::text
         LIMIT 1;
        -- El COALESCE a 0 también es exclusivo de la guía compartida: ahí la columna existe en
        -- toda la curva y el 0 se lee como «la guía dice 0». En la propia la métrica NO EXISTE
        -- (no trae peso, ni uniformidad, ni mortalidad semanal — su retiro_ac_h es ACUMULADO),
        -- y un 0 ahí se leería como un objetivo real. NULL es la única lectura honesta, y el
        -- front ya lo sabe pintar: las series por sexo llegan NULL desde siempre.
        IF v_origen_guia IS DISTINCT FROM 'propia' THEN
            r_cons_tabla := COALESCE(r_cons_tabla,0);
            r_peso_tabla := COALESCE(r_peso_tabla,0);
            r_unif_tabla := COALESCE(r_unif_tabla,0);
            r_mort_tabla := COALESCE(r_mort_tabla,0);
        END IF;
        -- r_cons_tabla_h/_m, r_peso_tabla_h/_m, r_mort_tabla_h/_m se dejan NULL si la guía
        -- no trae el dato del sexo (series de guía degradan a NULL, sin promediar).

        r_gan_sem   := r_peso_prom - v_peso_anterior;
        r_cons_ave  := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv      := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem  := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem   := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem   := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        -- REQ-010b: mortalidad y retiro POR SEXO. Mismo denominador que el mixto (aves al inicio
        -- de la semana del sexo). El retiro replica el mixto retiroSem = mort+sel+errSex del sexo.
        -- NULL (no 0 sintético) cuando el sexo no tiene saldo ⇒ la serie degrada con spanGaps.
        r_mort_pct_h   := CASE WHEN v_aves_acum_h > 0 THEN (r_mort_h / v_aves_acum_h) * 100 ELSE NULL END;
        r_mort_pct_m   := CASE WHEN v_aves_acum_m > 0 THEN (r_mort_m / v_aves_acum_m) * 100 ELSE NULL END;
        r_retiro_pct_h := CASE WHEN v_aves_acum_h > 0 THEN ((r_mort_h + r_sel_h + r_err_h) / v_aves_acum_h) * 100 ELSE NULL END;
        r_retiro_pct_m := CASE WHEN v_aves_acum_m > 0 THEN ((r_mort_m + r_sel_m + r_err_m) / v_aves_acum_m) * 100 ELSE NULL END;

        r_efic   := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip     := r_efic * r_superv;

        -- REQ-002f: acumulados reales = bajas_acumuladas / aves_encasetadas * 100.
        v_mort_bajas_acum := v_mort_bajas_acum + r_mort_tot;
        v_sel_bajas_acum  := v_sel_bajas_acum + r_sel_tot;

        semana                        := s;
        aves_inicio_semana            := v_aves_acum;
        aves_fin_semana               := r_aves_fin;
        consumo_diario                := r_cons_dia;
        consumo_tabla                 := r_cons_tabla;
        consumo_total_semana          := r_cons_g;
        conversion_alimenticia        := r_conv;
        peso_tabla                    := r_peso_tabla;
        unif_real                     := r_unif_real;
        unif_tabla                    := r_unif_tabla;
        mort_tabla                    := r_mort_tabla;
        dif_peso_pct                  := CASE WHEN r_peso_tabla > 0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana               := r_gan_sem;
        ganancia_diaria_acumulada     := r_gan_dia_ac;
        ganancia_tabla                := r_gan_tabla;
        mortalidad_sem                := r_mort_sem;
        seleccion_sem                 := r_sel_sem;
        error_sexaje_sem              := r_err_sem;
        mortalidad_mas_seleccion      := r_mort_mas_sel;
        eficiencia                    := r_efic;
        ip                            := r_ip;
        vpi                           := r_ip;   -- front: vpi = supervivencia*eficiencia = ip
        saldo_aves_semanal            := r_aves_fin;
        mortalidad_acum               := CASE WHEN v_aves_enc > 0 THEN (v_mort_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        seleccion_acum                := CASE WHEN v_aves_enc > 0 THEN (v_sel_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        mortalidad_mas_seleccion_acum := CASE WHEN v_aves_enc > 0 THEN ((v_mort_bajas_acum + v_sel_bajas_acum)/v_aves_enc)*100 ELSE 0 END;
        piso_termico_visible          := false;  -- la guía no expone el flag; front daba false
        peso_inicial                  := v_peso_anterior;
        peso_cierre                   := r_peso_prom;
        dias_con_registro             := r_dias;
        consumo_diario_hembras        := r_cons_dia_h;
        consumo_diario_machos         := r_cons_dia_m;
        consumo_tabla_hembras         := r_cons_tabla_h;
        consumo_tabla_machos          := r_cons_tabla_m;
        peso_hembras                  := r_peso_h;
        peso_machos                   := r_peso_m;
        peso_tabla_hembras            := r_peso_tabla_h;
        peso_tabla_machos             := r_peso_tabla_m;
        mort_pct_hembras              := r_mort_pct_h;
        mort_pct_machos               := r_mort_pct_m;
        mort_tabla_hembras            := r_mort_tabla_h;
        mort_tabla_machos             := r_mort_tabla_m;
        retiro_pct_hembras            := r_retiro_pct_h;
        retiro_pct_machos             := r_retiro_pct_m;

        -- TK-2026-000022 — el resto de los parametros por sexo. Ninguno introduce aritmetica
        -- nueva: son las MISMAS variables con las que ya se arman las columnas mixtas, expuestas
        -- sin promediar. El criterio de NULL es el de las series por sexo de arriba: si el sexo no
        -- tiene saldo (o la semana no tuvo pesaje / la guia no trae el dato) va NULL, para que la
        -- pantalla muestre un guion en vez de un cero que se leeria como dato real.
        aves_inicio_hembras           := CASE WHEN v_aves_enc_h > 0 THEN v_aves_acum_h ELSE NULL END;
        aves_fin_hembras              := CASE WHEN v_aves_enc_h > 0 THEN r_aves_fin_h  ELSE NULL END;
        aves_inicio_machos            := CASE WHEN v_aves_enc_m > 0 THEN v_aves_acum_m ELSE NULL END;
        aves_fin_machos               := CASE WHEN v_aves_enc_m > 0 THEN r_aves_fin_m  ELSE NULL END;
        consumo_total_semana_hembras  := CASE WHEN v_aves_enc_h > 0 THEN r_cons_kg_h * 1000 ELSE NULL END;
        consumo_total_semana_machos   := CASE WHEN v_aves_enc_m > 0 THEN r_cons_kg_m * 1000 ELSE NULL END;
        -- Uniformidad: 0 significa "no hubo pesaje esta semana", no "0 % de uniformidad".
        unif_hembras                  := CASE WHEN r_uH > 0 THEN r_uH ELSE NULL END;
        unif_machos                   := CASE WHEN r_uM > 0 THEN r_uM ELSE NULL END;
        ganancia_hembras              := CASE WHEN r_peso_h IS NOT NULL AND v_peso_ant_h IS NOT NULL
                                              THEN r_peso_h - v_peso_ant_h ELSE NULL END;
        ganancia_machos               := CASE WHEN r_peso_m IS NOT NULL AND v_peso_ant_m IS NOT NULL
                                              THEN r_peso_m - v_peso_ant_m ELSE NULL END;
        dif_peso_pct_hembras          := CASE WHEN r_peso_tabla_h > 0 AND r_peso_h IS NOT NULL
                                              THEN ((r_peso_h - r_peso_tabla_h)/r_peso_tabla_h)*100 ELSE NULL END;
        dif_peso_pct_machos           := CASE WHEN r_peso_tabla_m > 0 AND r_peso_m IS NOT NULL
                                              THEN ((r_peso_m - r_peso_tabla_m)/r_peso_tabla_m)*100 ELSE NULL END;
        seleccion_pct_hembras         := CASE WHEN v_aves_acum_h > 0 THEN (r_sel_h / v_aves_acum_h) * 100 ELSE NULL END;
        seleccion_pct_machos          := CASE WHEN v_aves_acum_m > 0 THEN (r_sel_m / v_aves_acum_m) * 100 ELSE NULL END;
        error_sexaje_pct_hembras      := CASE WHEN v_aves_acum_h > 0 THEN (r_err_h / v_aves_acum_h) * 100 ELSE NULL END;
        error_sexaje_pct_machos       := CASE WHEN v_aves_acum_m > 0 THEN (r_err_m / v_aves_acum_m) * 100 ELSE NULL END;

        RETURN NEXT;

        -- avanzar acumuladores (idéntico al front) + saldo por género.
        v_aves_acum      := r_aves_fin;
        v_aves_acum_h    := r_aves_fin_h;
        v_aves_acum_m    := r_aves_fin_m;
        v_peso_anterior  := r_peso_prom;
        v_peso_tabla_ant := r_peso_tabla;
        v_peso_ant_h     := r_peso_h;   -- arrastre peso por sexo (REQ-010b)
        v_peso_ant_m     := r_peso_m;
    END LOOP;

    RETURN;
END;
$$;
""";

        /// <summary>fn_indicadores_produccion_postura NUEVA: semana de arranque por empresa.</summary>
        private const string FnIndicadoresProduccionPosturaNueva = """
-- ============================================================================
-- fn_indicadores_produccion_postura(company, lote_produccion, lote, semanas, fechas)
-- Indicadores semanales de PRODUCCION (postura Colombia).
--
-- ⚠️ Este archivo se REGENERO el 14ago26 desde la funcion DESPLEGADA
--    (pg_get_functiondef), porque la version anterior del espejo estaba
--    DESINCRONIZADA: le faltaba la columna de salida `porcentaje_seleccion_machos`,
--    que si existe en la base. Aplicarlo tal cual habria fallado con
--    «42P13: cannot change return type of existing function» — y de hecho fallo,
--    que es como se detecto. Antes de tocar este archivo, comparalo contra
--    pg_get_functiondef; el espejo NO es automaticamente lo desplegado.
--
-- Cambio de esta version (TK-2026-000023): `diferencia_mortalidad_hembras/machos`
-- pasan de fn_dif_pct (porcentaje relativo) a fn_dif_pp (diferencia directa en
-- puntos porcentuales). El resto de las diferencias no se toca.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_indicadores_produccion_postura(p_company_id integer, p_lote_postura_produccion_id integer DEFAULT NULL::integer, p_lote_id integer DEFAULT NULL::integer, p_semana_desde integer DEFAULT NULL::integer, p_semana_hasta integer DEFAULT NULL::integer, p_fecha_desde date DEFAULT NULL::date, p_fecha_hasta date DEFAULT NULL::date)
 RETURNS TABLE(semana integer, fecha_inicio_semana date, fecha_fin_semana date, total_registros integer, mortalidad_hembras integer, mortalidad_machos integer, porcentaje_mortalidad_hembras double precision, porcentaje_mortalidad_machos double precision, mortalidad_guia_hembras double precision, mortalidad_guia_machos double precision, diferencia_mortalidad_hembras double precision, diferencia_mortalidad_machos double precision, seleccion_hembras integer, porcentaje_seleccion_hembras double precision, seleccion_machos integer, porcentaje_seleccion_machos double precision, consumo_kg_hembras double precision, consumo_kg_machos double precision, consumo_total_kg double precision, consumo_promedio_diario_kg double precision, consumo_guia_hembras double precision, consumo_guia_machos double precision, diferencia_consumo_hembras double precision, diferencia_consumo_machos double precision, huevos_totales integer, huevos_incubables integer, promedio_huevos_por_dia double precision, eficiencia_produccion double precision, huevos_totales_guia double precision, huevos_incubables_guia double precision, porcentaje_produccion_guia double precision, diferencia_huevos_totales double precision, diferencia_huevos_incubables double precision, diferencia_porcentaje_produccion double precision, peso_huevo_promedio double precision, peso_huevo_guia double precision, diferencia_peso_huevo double precision, peso_promedio_hembras double precision, peso_promedio_machos double precision, peso_guia_hembras double precision, peso_guia_machos double precision, diferencia_peso_hembras double precision, diferencia_peso_machos double precision, uniformidad_promedio double precision, uniformidad_guia double precision, diferencia_uniformidad double precision, coeficiente_variacion_promedio double precision, huevos_limpios integer, huevos_tratados integer, huevos_sucios integer, huevos_deformes integer, huevos_blancos integer, huevos_doble_yema integer, huevos_piso integer, huevos_pequenos integer, huevos_rotos integer, huevos_desecho integer, huevos_otro integer, aves_hembras_inicio_semana integer, aves_machos_inicio_semana integer, aves_hembras_fin_semana integer, aves_machos_fin_semana integer, htaa_real double precision, hiaa_real double precision, retiro_sem_h double precision, retiro_sem_m double precision, retiro_ac_h double precision, retiro_ac_m double precision, retiro_ac_h_guia double precision, retiro_ac_m_guia double precision)
 LANGUAGE plpgsql
AS $function$
DECLARE
    -- ── contexto del lote resuelto ──
    v_enc_date       date;            -- fechaEncaset.Date (Bogotá)
    v_aves_h_ini     integer;
    v_aves_m_ini     integer;
    v_raza           text;
    v_ano            text;            -- ano_tabla_genetica::text
    v_lote_id_str    text;            -- para el flujo legacy (lote_id como texto)
    v_lote_id_int    integer;         -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_has_lote       boolean := false;
    -- Semana de VIDA desde la que esta empresa muestra produccion (companies.
    -- semana_inicio_indicadores_produccion, DEFAULT 25 = el valor que estuvo hardcodeado hasta el
    -- 30-ago-2026). Existe porque no todas las empresas empiezan a poner en la misma semana: la
    -- postura comercial de Santa Reyes arranca en la 18 —es la primera edad de su guia propia y es
    -- coherente con su huevo_primera_postura_hasta_semana = 22—, y con el 25 fijo sus semanas
    -- 18-24 no aparecian en ningun indicador. Con el DEFAULT 25 las otras cuatro empresas
    -- ejecutan exactamente lo mismo que antes.
    v_sem_inicio     integer;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;
    -- REQ-004: acumulados de retiro por sexo (mortalidad + selección)
    v_cum_mort_h     bigint := 0;
    v_cum_sel_h      bigint := 0;
    v_cum_mort_m     bigint := 0;
    v_cum_sel_m      bigint := 0;

    v_max_sem        integer;
    s                integer;

    -- ── por semana ──
    r_dias           integer;
    r_mort_h         integer;
    r_mort_m         integer;
    r_sel_h          integer;
    r_cons_kg_h      double precision;
    r_cons_kg_m      double precision;
    r_huevos_tot     integer;
    r_huevos_inc     integer;
    r_prom_huevos    double precision;
    r_efic           double precision;
    r_htaa           double precision;
    r_hiaa           double precision;
    r_peso_h         double precision;
    r_peso_m         double precision;
    r_unif           double precision;
    r_cv             double precision;
    r_peso_huevo     double precision;
    r_porc_mort_h    double precision;
    r_porc_mort_m    double precision;
    r_porc_sel_h     double precision;
    r_porc_sel_m     double precision;
    -- REQ-004: %Retiro real por semana
    r_retiro_sem_h   double precision;
    r_retiro_sem_m   double precision;
    r_retiro_ac_h    double precision;
    r_retiro_ac_m    double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- Movimientos de aves de la semana (ventas, retiros y traslados). Antes el saldo
    --   solo restaba mortalidad y selección, así que una venta de producción —que no deja
    --   columna numérica en la fila diaria, solo nota— quedaba fuera y el saldo del
    --   reporte terminaba por encima del real en exactamente el total vendido.
    r_sel_m          integer;   -- la fn nunca llevó la selección de machos: ni al saldo ni a la salida
    r_venta_h        integer;
    r_venta_m        integer;
    r_retiro_h       integer;
    r_retiro_m       integer;
    r_tras_out_h     integer;
    r_tras_out_m     integer;
    r_tras_in_h      integer;
    r_tras_in_m      integer;
    -- guía
    g_cons_h         double precision;
    g_cons_m         double precision;
    g_mort_h         double precision;
    g_mort_m         double precision;
    g_peso_h         double precision;
    g_peso_m         double precision;
    g_unif           double precision;
    g_huevos_tot     double precision;
    g_huevos_inc     double precision;
    g_prod_pct       double precision;
    g_peso_huevo     double precision;
    -- REQ-004 (Verenice): %Retiro acumulado de guía por sexo.
    g_retiro_ac_h    double precision;
    g_retiro_ac_m    double precision;
    g_found          boolean;
    -- De que tabla salio la fila: 'compartida' (guia_genetica_sanmarino_colombia) o 'propia'
    -- (guia_genetica_santa_reyes, 3 metricas y solo hembras). Gobierna los COALESCE de abajo.
    g_origen         text;
    -- consumo real
    r_cons_real_h    double precision;
    r_cons_real_m    double precision;
    -- clasificadora
    r_limpios        integer;
    r_tratados       integer;
    r_sucios         integer;
    r_deformes       integer;
    r_blancos        integer;
    r_doble_yema     integer;
    r_piso           integer;
    r_pequenos       integer;
    r_rotos          integer;
    r_desecho        integer;
    r_otro           integer;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que el C#).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date,
            COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0),
            COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0),
            COALESCE(lpp.raza, ''),
            lpp.ano_tabla_genetica::text
          INTO v_enc_date, v_aves_h_ini, v_aves_m_ini, v_raza, v_ano
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas (el C# lanza; el servicio valida antes)
        END IF;
        v_has_lote := true;

        -- Seguimientos: desde fn_seguimiento_diario_produccion (la fn diaria canónica ya hace el
        -- UNION dual-fuente + dedup por día Bogotá «gana el más temprano»); solo días con registro
        -- (seg_id IS NOT NULL — sin días movimiento-only).
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h, COALESCE(f.sel_m,0) AS sel_m,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv,
               COALESCE(f.mov_venta_h,0) AS mv_venta_h, COALESCE(f.mov_venta_m,0) AS mv_venta_m,
               COALESCE(f.mov_retiro_h,0) AS mv_retiro_h, COALESCE(f.mov_retiro_m,0) AS mv_retiro_m,
               COALESCE(f.mov_traslado_out_h,0) AS mv_out_h, COALESCE(f.mov_traslado_out_m,0) AS mv_out_m,
               COALESCE(f.mov_traslado_in_h,0) AS mv_in_h, COALESCE(f.mov_traslado_in_m,0) AS mv_in_m
          FROM fn_seguimiento_diario_produccion(p_lote_postura_produccion_id, NULL) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
            v_lp_raza         text;
            v_lp_ano          integer;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_has_lote := true;
            v_lote_id_str := v_lp_lote_id::text;
            v_lote_id_int := v_lp_lote_id;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;

            SELECT COALESCE(hembras_iniciales_prod,0), COALESCE(machos_iniciales_prod,0)
              INTO v_aves_h_ini, v_aves_m_ini
              FROM lotes WHERE lote_id = v_lp_lote_id;

            -- raza/año del lote; si faltan y hay padre, del padre
            v_raza := COALESCE(v_lp_raza, '');
            v_ano  := v_lp_ano::text;
            IF (v_raza = '' OR v_lp_ano IS NULL) AND v_lp_padre_id IS NOT NULL THEN
                SELECT COALESCE(p.raza,''), p.ano_tabla_genetica::text
                  INTO v_raza, v_ano
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
        END;

        -- Seguimientos legacy: desde fn_seguimiento_diario_produccion (dedup dual-fuente ya
        -- resuelto por la fn diaria); solo días con registro.
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h, COALESCE(f.sel_m,0) AS sel_m,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv,
               COALESCE(f.mov_venta_h,0) AS mv_venta_h, COALESCE(f.mov_venta_m,0) AS mv_venta_m,
               COALESCE(f.mov_retiro_h,0) AS mv_retiro_h, COALESCE(f.mov_retiro_m,0) AS mv_retiro_m,
               COALESCE(f.mov_traslado_out_h,0) AS mv_out_h, COALESCE(f.mov_traslado_out_m,0) AS mv_out_m,
               COALESCE(f.mov_traslado_in_h,0) AS mv_in_h, COALESCE(f.mov_traslado_in_m,0) AS mv_in_m
          FROM fn_seguimiento_diario_produccion(NULL, v_lote_id_int) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Semana de VIDA de cada registro + filtro de fechas (== C#).
    --    semanaVida = floor(dias/7)+1 con dias = regDate - encDate (división entera).
    -- ════════════════════════════════════════════════════════════════════
    ALTER TABLE _seg ADD COLUMN reg_date date;
    ALTER TABLE _seg ADD COLUMN sem_vida integer;
    UPDATE _seg SET reg_date = (ts AT TIME ZONE 'America/Bogota')::date;
    -- filtro de fechas (request.FechaDesde/Hasta) sobre la fecha local
    IF p_fecha_desde IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date < p_fecha_desde;
    END IF;
    IF p_fecha_hasta IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date > p_fecha_hasta;
    END IF;
    UPDATE _seg SET sem_vida = ((reg_date - v_enc_date) / 7) + 1;  -- división entera == C# (dias/7)+1
    -- REQ-012b: producción arranca en la semana 25 de vida (antes 26). La guía genética empieza en
    --   la semana 26, así que la 25 queda con columnas de guía en NULL (g_found=false ya lo soporta).
    -- 30-ago-2026: ese 25 pasa a ser el DEFAULT de la columna por empresa, no una constante. El
    --   COALESCE cubre tanto la empresa inexistente como la columna en NULL: sin fila, el valor es
    --   el de siempre.
    SELECT COALESCE(c.semana_inicio_indicadores_produccion, 25)
      INTO v_sem_inicio
      FROM companies c
     WHERE c.id = p_company_id;
    v_sem_inicio := COALESCE(v_sem_inicio, 25);

    DELETE FROM _seg WHERE sem_vida < v_sem_inicio;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: itera SOLO las semanas con registros (>=25 tras REQ-012b) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa, retiro) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN v_sem_inicio..v_max_sem LOOP  -- REQ-012b: incluir semana 25 (antes 26); hoy, la de la empresa
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0),
               COALESCE(SUM(mv_venta_h),0), COALESCE(SUM(mv_venta_m),0),
               COALESCE(SUM(mv_retiro_h),0), COALESCE(SUM(mv_retiro_m),0),
               COALESCE(SUM(mv_out_h),0), COALESCE(SUM(mv_out_m),0),
               COALESCE(SUM(mv_in_h),0), COALESCE(SUM(mv_in_m),0), COALESCE(SUM(sel_m),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro,
               r_venta_h, r_venta_m, r_retiro_h, r_retiro_m,
               r_tras_out_h, r_tras_out_m, r_tras_in_h, r_tras_in_m, r_sel_m
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;

        -- REQ-004: acumulados de retiro (mortalidad + selección) por sexo. Desde
        --   20260806093256 los MACHOS también acumulan selección, igual que las hembras.
        v_cum_mort_h := v_cum_mort_h + r_mort_h;
        v_cum_sel_h  := v_cum_sel_h + r_sel_h;
        v_cum_mort_m := v_cum_mort_m + r_mort_m;
        v_cum_sel_m  := v_cum_sel_m + r_sel_m;
        r_htaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_tot::double precision / v_aves_h_ini ELSE 0 END;
        r_hiaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_inc::double precision / v_aves_h_ini ELSE 0 END;

        -- Peso aves (kg, REQ-004b): promedio de registros con valor NO NULO, luego normalizar.
        SELECT AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL),
               AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL),
               AVG(unif)   FILTER (WHERE unif   IS NOT NULL),
               AVG(cv)     FILTER (WHERE cv     IS NOT NULL),
               AVG(peso_huevo) FILTER (WHERE peso_huevo > 0)
          INTO r_peso_h, r_peso_m, r_unif, r_cv, r_peso_huevo
          FROM _seg WHERE sem_vida = s;
        IF r_peso_h IS NOT NULL THEN r_peso_h := CASE WHEN r_peso_h > 100 THEN r_peso_h/1000 ELSE r_peso_h END; END IF;
        IF r_peso_m IS NOT NULL THEN r_peso_m := CASE WHEN r_peso_m > 100 THEN r_peso_m/1000 ELSE r_peso_m END; END IF;

        -- %mortalidad / %selección: sobre el saldo REAL de inicio (avesActuales)
        r_porc_mort_h := CASE WHEN v_aves_h_act > 0 THEN r_mort_h::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_porc_mort_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_porc_sel_h  := CASE WHEN v_aves_h_act > 0 THEN r_sel_h::double precision  / v_aves_h_act * 100 ELSE 0 END;
        r_porc_sel_m  := CASE WHEN v_aves_m_act > 0 THEN r_sel_m::double precision  / v_aves_m_act * 100 ELSE 0 END;

        -- REQ-004: %Retiro REAL (== ProduccionCalculos.PorcentajeRetiroSemanal/Acumulado).
        --   Semanal: (mort + sel de la semana) / saldo REAL de inicio del sexo (v_aves_*_act, pre-decremento) * 100.
        --   Acumulado: (mort + sel acumulados) / aves iniciales del sexo * 100.
        r_retiro_sem_h := CASE WHEN v_aves_h_act > 0 THEN (r_mort_h + r_sel_h)::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_retiro_sem_m := CASE WHEN v_aves_m_act > 0 THEN (r_mort_m + r_sel_m)::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_retiro_ac_h  := CASE WHEN v_aves_h_ini > 0 THEN (v_cum_mort_h + v_cum_sel_h)::double precision / v_aves_h_ini * 100 ELSE 0 END;
        r_retiro_ac_m  := CASE WHEN v_aves_m_ini > 0 THEN (v_cum_mort_m + v_cum_sel_m)::double precision / v_aves_m_ini * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m + r_sel_m;

        -- ── Guía (una sola tabla) por Edad = semana de VIDA (s) ──
        g_found := false;
        SELECT true,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.uniformidad),'')::double precision,
               NULLIF(btrim(g.h_total_aa),'')::double precision,
               NULLIF(btrim(g.h_inc_aa),'')::double precision,
               NULLIF(btrim(g.prod_porcentaje),'')::double precision,
               NULLIF(btrim(g.peso_huevo),'')::double precision,
               NULLIF(btrim(g.retiro_ac_h),'')::double precision,
               NULLIF(btrim(g.retiro_ac_m),'')::double precision,
               g.origen
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo, g_retiro_ac_h, g_retiro_ac_m,
               g_origen
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         -- La semana 25 tiene DOS filas que parsean a 25: '25' (cierre de
         -- levante) y '25P' (arranque de producción), con valores muy distintos
         -- (retiro_ac_h 4,03 vs 0,10). Sin ORDER BY la que gana depende del
         -- plan y del orden físico de la tabla: hoy sale '25P' por el ctid, no
         -- por contrato. Se fija el desempate en la variante con sufijo —la de
         -- producción, que es la correcta acá y la que ya venía devolviendo—
         -- para que un VACUUM o un re-seed no cambien el reporte en silencio.
         ORDER BY (CASE WHEN btrim(g.edad) = s::text THEN 1 ELSE 0 END), g.id
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía "obtenerGuiaGeneticaProduccion" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            -- 🔴 Los COALESCE a 0 son EXCLUSIVOS de la guía compartida.
            -- Ahí la columna existe en toda la curva y el 0 se lee como «la guía dice 0»
            -- (y quitarlos NO sería delta cero: en el rango de producción, company 1 tiene
            -- entre 6 y 14 filas en blanco por columna). En la guía propia esas métricas
            -- NO EXISTEN —no trae peso, ni consumo de machos, ni mortalidad semanal— y el 0
            -- ahí no es «sin dato»: es un objetivo falso. Peor todavía, `fn_dif_pp` documenta
            -- que con guía = 0 NO devuelve NULL, así que la columna «diferencia vs guía» de
            -- mortalidad pintaría la mortalidad REAL del lote como si fuera la desviación.
            -- Con NULL, `fn_dif_pct`/`fn_dif_pp` degradan solas y el front pinta un guion.
            IF g_origen IS DISTINCT FROM 'propia' THEN
                g_cons_h := COALESCE(g_cons_h, 0);
                g_cons_m := COALESCE(g_cons_m, 0);
                g_mort_h := COALESCE(g_mort_h, 0);
                g_mort_m := COALESCE(g_mort_m, 0);
            END IF;
            -- El /1000 sí se aplica siempre (la guía viene en gramos y la salida en kg);
            -- lo condicional es el COALESCE, porque NULL/1000 = NULL y eso es lo correcto.
            g_peso_h := CASE WHEN g_origen = 'propia' THEN g_peso_h / 1000
                             ELSE COALESCE(g_peso_h, 0) / 1000 END;   -- peso_h/1000
            g_peso_m := CASE WHEN g_origen = 'propia' THEN g_peso_m / 1000
                             ELSE COALESCE(g_peso_m, 0) / 1000 END;   -- peso_m/1000
            -- ⚠️ EXCEPCIÓN DELIBERADA a la regla ParseDouble=>0 de sus vecinas: g_unif NO se
            --   coalescea. La guía genética no define uniformidad para las edades de PRODUCCIÓN
            --   (solo 25 de sus 98 filas la traen, todas de levante) ⇒ el 0 se pintaba en TODAS
            --   las semanas y se lee como «la guía exige 0 %» en vez de «sin dato», además de
            --   calcular la diferencia contra ese 0. Un 0 real tampoco existe como objetivo de
            --   uniformidad, así que NULL es la única lectura honesta.
            --   `diferencia_uniformidad` no se mueve: fn_dif_pct ya devolvía NULL con guía = 0.
            --   Los demás (cons/mort/peso/retiro_ac) SÍ conservan el 0: la guía los trae en toda
            --   la curva y cambiarlos movería números sin necesidad.
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
            -- retiro_ac_h/m guía: mismo criterio que mort_h/mort_m (ParseDouble => 0 si vacío).
            -- retiro_ac_h SÍ lo trae la guía propia (es su métrica de mortalidad, acumulada);
            -- retiro_ac_m no, y por eso el COALESCE queda condicionado igual que los de arriba.
            IF g_origen IS DISTINCT FROM 'propia' THEN
                g_retiro_ac_h := COALESCE(g_retiro_ac_h, 0);
                g_retiro_ac_m := COALESCE(g_retiro_ac_m, 0);
            END IF;
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
            g_retiro_ac_h := NULL; g_retiro_ac_m := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves. Además de mortalidad y selección descuenta VENTAS, retiros
        --   y salidas por traslado, y suma los ingresos: son aves que dejan (o entran a)
        --   el lote igual que las bajas. Misma composición que SaldoAvesLevanteCalculos.
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h
                                    - r_venta_h - r_retiro_h - r_tras_out_h + r_tras_in_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m - r_sel_m
                                    - r_venta_m - r_retiro_m - r_tras_out_m + r_tras_in_m);

        -- ── Emitir fila (respetando filtro semanaDesde/Hasta como en C#) ──
        IF (p_semana_desde IS NULL OR s >= p_semana_desde)
           AND (p_semana_hasta IS NULL OR s <= p_semana_hasta) THEN
            semana                           := s;
            fecha_inicio_semana              := v_enc_date + ((s - 1) * 7);
            fecha_fin_semana                 := v_enc_date + ((s - 1) * 7) + 6;
            total_registros                  := r_dias;
            mortalidad_hembras               := r_mort_h;
            mortalidad_machos                := r_mort_m;
            porcentaje_mortalidad_hembras    := r_porc_mort_h;
            porcentaje_mortalidad_machos     := r_porc_mort_m;
            mortalidad_guia_hembras          := g_mort_h;
            mortalidad_guia_machos           := g_mort_m;
            -- TK-2026-000023: la diferencia de MORTALIDAD es DIRECTA (puntos porcentuales),
            -- no porcentaje diferencial. Real y guia ya son porcentajes: restarlos da la
            -- distancia real (0,07 % vs 0,33 % => -0,26 pp). El porcentaje relativo
            -- ((real-guia)/guia*100) sobre numeros tan chicos explota: la pantalla llegaba a
            -- mostrar +2.212,10 % para 0,26 % contra 0,01 % de guia.
            -- Las demas diferencias (consumo, peso, huevos) SIGUEN relativas: ahi real y guia
            -- son magnitudes (kg, g, unidades), no porcentajes.
            diferencia_mortalidad_hembras    := fn_dif_pp(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pp(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            seleccion_machos                 := r_sel_m;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            porcentaje_seleccion_machos      := r_porc_sel_m;
            consumo_kg_hembras               := r_cons_kg_h;
            consumo_kg_machos                := r_cons_kg_m;
            consumo_total_kg                 := r_cons_kg_h + r_cons_kg_m;
            consumo_promedio_diario_kg       := CASE WHEN r_dias > 0 THEN (r_cons_kg_h + r_cons_kg_m)/r_dias ELSE 0 END;
            consumo_guia_hembras             := g_cons_h;
            consumo_guia_machos              := g_cons_m;
            diferencia_consumo_hembras       := fn_dif_pct(r_cons_real_h, g_cons_h);
            diferencia_consumo_machos        := fn_dif_pct(r_cons_real_m, g_cons_m);
            huevos_totales                   := r_huevos_tot;
            huevos_incubables                := r_huevos_inc;
            promedio_huevos_por_dia          := r_prom_huevos;
            eficiencia_produccion            := r_efic;
            huevos_totales_guia              := g_huevos_tot;
            huevos_incubables_guia           := g_huevos_inc;
            porcentaje_produccion_guia       := g_prod_pct;
            diferencia_huevos_totales        := fn_dif_pct(r_htaa, g_huevos_tot);
            diferencia_huevos_incubables     := fn_dif_pct(r_hiaa, g_huevos_inc);
            diferencia_porcentaje_produccion := fn_dif_pct(r_efic, g_prod_pct);
            peso_huevo_promedio              := r_peso_huevo;
            peso_huevo_guia                  := g_peso_huevo;
            diferencia_peso_huevo            := fn_dif_pct(r_peso_huevo, g_peso_huevo);
            peso_promedio_hembras            := r_peso_h;
            peso_promedio_machos             := r_peso_m;
            peso_guia_hembras                := g_peso_h;
            peso_guia_machos                 := g_peso_m;
            diferencia_peso_hembras          := fn_dif_pct(r_peso_h, g_peso_h);
            diferencia_peso_machos           := fn_dif_pct(r_peso_m, g_peso_m);
            uniformidad_promedio             := r_unif;
            uniformidad_guia                 := g_unif;
            diferencia_uniformidad           := fn_dif_pct(r_unif, g_unif);
            coeficiente_variacion_promedio   := r_cv;
            huevos_limpios                   := r_limpios;
            huevos_tratados                  := r_tratados;
            huevos_sucios                    := r_sucios;
            huevos_deformes                  := r_deformes;
            huevos_blancos                   := r_blancos;
            huevos_doble_yema                := r_doble_yema;
            huevos_piso                      := r_piso;
            huevos_pequenos                  := r_pequenos;
            huevos_rotos                     := r_rotos;
            huevos_desecho                   := r_desecho;
            huevos_otro                      := r_otro;
            aves_hembras_inicio_semana       := r_aves_h_inicio;
            aves_machos_inicio_semana        := r_aves_m_inicio;
            aves_hembras_fin_semana          := v_aves_h_act;
            aves_machos_fin_semana           := v_aves_m_act;
            htaa_real                        := r_htaa;
            hiaa_real                        := r_hiaa;
            retiro_sem_h                     := r_retiro_sem_h;
            retiro_sem_m                     := r_retiro_sem_m;
            retiro_ac_h                      := r_retiro_ac_h;
            retiro_ac_m                      := r_retiro_ac_m;
            retiro_ac_h_guia                 := g_retiro_ac_h;
            retiro_ac_m_guia                 := g_retiro_ac_m;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$function$
""";

        /// <summary>fn_indicadores_produccion_postura PREVIA (HEAD), verbatim.</summary>
        private const string FnIndicadoresProduccionPosturaPrevia = """
-- ============================================================================
-- fn_indicadores_produccion_postura(company, lote_produccion, lote, semanas, fechas)
-- Indicadores semanales de PRODUCCION (postura Colombia).
--
-- ⚠️ Este archivo se REGENERO el 14ago26 desde la funcion DESPLEGADA
--    (pg_get_functiondef), porque la version anterior del espejo estaba
--    DESINCRONIZADA: le faltaba la columna de salida `porcentaje_seleccion_machos`,
--    que si existe en la base. Aplicarlo tal cual habria fallado con
--    «42P13: cannot change return type of existing function» — y de hecho fallo,
--    que es como se detecto. Antes de tocar este archivo, comparalo contra
--    pg_get_functiondef; el espejo NO es automaticamente lo desplegado.
--
-- Cambio de esta version (TK-2026-000023): `diferencia_mortalidad_hembras/machos`
-- pasan de fn_dif_pct (porcentaje relativo) a fn_dif_pp (diferencia directa en
-- puntos porcentuales). El resto de las diferencias no se toca.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_indicadores_produccion_postura(p_company_id integer, p_lote_postura_produccion_id integer DEFAULT NULL::integer, p_lote_id integer DEFAULT NULL::integer, p_semana_desde integer DEFAULT NULL::integer, p_semana_hasta integer DEFAULT NULL::integer, p_fecha_desde date DEFAULT NULL::date, p_fecha_hasta date DEFAULT NULL::date)
 RETURNS TABLE(semana integer, fecha_inicio_semana date, fecha_fin_semana date, total_registros integer, mortalidad_hembras integer, mortalidad_machos integer, porcentaje_mortalidad_hembras double precision, porcentaje_mortalidad_machos double precision, mortalidad_guia_hembras double precision, mortalidad_guia_machos double precision, diferencia_mortalidad_hembras double precision, diferencia_mortalidad_machos double precision, seleccion_hembras integer, porcentaje_seleccion_hembras double precision, seleccion_machos integer, porcentaje_seleccion_machos double precision, consumo_kg_hembras double precision, consumo_kg_machos double precision, consumo_total_kg double precision, consumo_promedio_diario_kg double precision, consumo_guia_hembras double precision, consumo_guia_machos double precision, diferencia_consumo_hembras double precision, diferencia_consumo_machos double precision, huevos_totales integer, huevos_incubables integer, promedio_huevos_por_dia double precision, eficiencia_produccion double precision, huevos_totales_guia double precision, huevos_incubables_guia double precision, porcentaje_produccion_guia double precision, diferencia_huevos_totales double precision, diferencia_huevos_incubables double precision, diferencia_porcentaje_produccion double precision, peso_huevo_promedio double precision, peso_huevo_guia double precision, diferencia_peso_huevo double precision, peso_promedio_hembras double precision, peso_promedio_machos double precision, peso_guia_hembras double precision, peso_guia_machos double precision, diferencia_peso_hembras double precision, diferencia_peso_machos double precision, uniformidad_promedio double precision, uniformidad_guia double precision, diferencia_uniformidad double precision, coeficiente_variacion_promedio double precision, huevos_limpios integer, huevos_tratados integer, huevos_sucios integer, huevos_deformes integer, huevos_blancos integer, huevos_doble_yema integer, huevos_piso integer, huevos_pequenos integer, huevos_rotos integer, huevos_desecho integer, huevos_otro integer, aves_hembras_inicio_semana integer, aves_machos_inicio_semana integer, aves_hembras_fin_semana integer, aves_machos_fin_semana integer, htaa_real double precision, hiaa_real double precision, retiro_sem_h double precision, retiro_sem_m double precision, retiro_ac_h double precision, retiro_ac_m double precision, retiro_ac_h_guia double precision, retiro_ac_m_guia double precision)
 LANGUAGE plpgsql
AS $function$
DECLARE
    -- ── contexto del lote resuelto ──
    v_enc_date       date;            -- fechaEncaset.Date (Bogotá)
    v_aves_h_ini     integer;
    v_aves_m_ini     integer;
    v_raza           text;
    v_ano            text;            -- ano_tabla_genetica::text
    v_lote_id_str    text;            -- para el flujo legacy (lote_id como texto)
    v_lote_id_int    integer;         -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_has_lote       boolean := false;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;
    -- REQ-004: acumulados de retiro por sexo (mortalidad + selección)
    v_cum_mort_h     bigint := 0;
    v_cum_sel_h      bigint := 0;
    v_cum_mort_m     bigint := 0;
    v_cum_sel_m      bigint := 0;

    v_max_sem        integer;
    s                integer;

    -- ── por semana ──
    r_dias           integer;
    r_mort_h         integer;
    r_mort_m         integer;
    r_sel_h          integer;
    r_cons_kg_h      double precision;
    r_cons_kg_m      double precision;
    r_huevos_tot     integer;
    r_huevos_inc     integer;
    r_prom_huevos    double precision;
    r_efic           double precision;
    r_htaa           double precision;
    r_hiaa           double precision;
    r_peso_h         double precision;
    r_peso_m         double precision;
    r_unif           double precision;
    r_cv             double precision;
    r_peso_huevo     double precision;
    r_porc_mort_h    double precision;
    r_porc_mort_m    double precision;
    r_porc_sel_h     double precision;
    r_porc_sel_m     double precision;
    -- REQ-004: %Retiro real por semana
    r_retiro_sem_h   double precision;
    r_retiro_sem_m   double precision;
    r_retiro_ac_h    double precision;
    r_retiro_ac_m    double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- Movimientos de aves de la semana (ventas, retiros y traslados). Antes el saldo
    --   solo restaba mortalidad y selección, así que una venta de producción —que no deja
    --   columna numérica en la fila diaria, solo nota— quedaba fuera y el saldo del
    --   reporte terminaba por encima del real en exactamente el total vendido.
    r_sel_m          integer;   -- la fn nunca llevó la selección de machos: ni al saldo ni a la salida
    r_venta_h        integer;
    r_venta_m        integer;
    r_retiro_h       integer;
    r_retiro_m       integer;
    r_tras_out_h     integer;
    r_tras_out_m     integer;
    r_tras_in_h      integer;
    r_tras_in_m      integer;
    -- guía
    g_cons_h         double precision;
    g_cons_m         double precision;
    g_mort_h         double precision;
    g_mort_m         double precision;
    g_peso_h         double precision;
    g_peso_m         double precision;
    g_unif           double precision;
    g_huevos_tot     double precision;
    g_huevos_inc     double precision;
    g_prod_pct       double precision;
    g_peso_huevo     double precision;
    -- REQ-004 (Verenice): %Retiro acumulado de guía por sexo.
    g_retiro_ac_h    double precision;
    g_retiro_ac_m    double precision;
    g_found          boolean;
    -- De que tabla salio la fila: 'compartida' (guia_genetica_sanmarino_colombia) o 'propia'
    -- (guia_genetica_santa_reyes, 3 metricas y solo hembras). Gobierna los COALESCE de abajo.
    g_origen         text;
    -- consumo real
    r_cons_real_h    double precision;
    r_cons_real_m    double precision;
    -- clasificadora
    r_limpios        integer;
    r_tratados       integer;
    r_sucios         integer;
    r_deformes       integer;
    r_blancos        integer;
    r_doble_yema     integer;
    r_piso           integer;
    r_pequenos       integer;
    r_rotos          integer;
    r_desecho        integer;
    r_otro           integer;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que el C#).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date,
            COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0),
            COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0),
            COALESCE(lpp.raza, ''),
            lpp.ano_tabla_genetica::text
          INTO v_enc_date, v_aves_h_ini, v_aves_m_ini, v_raza, v_ano
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas (el C# lanza; el servicio valida antes)
        END IF;
        v_has_lote := true;

        -- Seguimientos: desde fn_seguimiento_diario_produccion (la fn diaria canónica ya hace el
        -- UNION dual-fuente + dedup por día Bogotá «gana el más temprano»); solo días con registro
        -- (seg_id IS NOT NULL — sin días movimiento-only).
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h, COALESCE(f.sel_m,0) AS sel_m,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv,
               COALESCE(f.mov_venta_h,0) AS mv_venta_h, COALESCE(f.mov_venta_m,0) AS mv_venta_m,
               COALESCE(f.mov_retiro_h,0) AS mv_retiro_h, COALESCE(f.mov_retiro_m,0) AS mv_retiro_m,
               COALESCE(f.mov_traslado_out_h,0) AS mv_out_h, COALESCE(f.mov_traslado_out_m,0) AS mv_out_m,
               COALESCE(f.mov_traslado_in_h,0) AS mv_in_h, COALESCE(f.mov_traslado_in_m,0) AS mv_in_m
          FROM fn_seguimiento_diario_produccion(p_lote_postura_produccion_id, NULL) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
            v_lp_raza         text;
            v_lp_ano          integer;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_has_lote := true;
            v_lote_id_str := v_lp_lote_id::text;
            v_lote_id_int := v_lp_lote_id;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;

            SELECT COALESCE(hembras_iniciales_prod,0), COALESCE(machos_iniciales_prod,0)
              INTO v_aves_h_ini, v_aves_m_ini
              FROM lotes WHERE lote_id = v_lp_lote_id;

            -- raza/año del lote; si faltan y hay padre, del padre
            v_raza := COALESCE(v_lp_raza, '');
            v_ano  := v_lp_ano::text;
            IF (v_raza = '' OR v_lp_ano IS NULL) AND v_lp_padre_id IS NOT NULL THEN
                SELECT COALESCE(p.raza,''), p.ano_tabla_genetica::text
                  INTO v_raza, v_ano
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
        END;

        -- Seguimientos legacy: desde fn_seguimiento_diario_produccion (dedup dual-fuente ya
        -- resuelto por la fn diaria); solo días con registro.
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h, COALESCE(f.sel_m,0) AS sel_m,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv,
               COALESCE(f.mov_venta_h,0) AS mv_venta_h, COALESCE(f.mov_venta_m,0) AS mv_venta_m,
               COALESCE(f.mov_retiro_h,0) AS mv_retiro_h, COALESCE(f.mov_retiro_m,0) AS mv_retiro_m,
               COALESCE(f.mov_traslado_out_h,0) AS mv_out_h, COALESCE(f.mov_traslado_out_m,0) AS mv_out_m,
               COALESCE(f.mov_traslado_in_h,0) AS mv_in_h, COALESCE(f.mov_traslado_in_m,0) AS mv_in_m
          FROM fn_seguimiento_diario_produccion(NULL, v_lote_id_int) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Semana de VIDA de cada registro + filtro de fechas (== C#).
    --    semanaVida = floor(dias/7)+1 con dias = regDate - encDate (división entera).
    -- ════════════════════════════════════════════════════════════════════
    ALTER TABLE _seg ADD COLUMN reg_date date;
    ALTER TABLE _seg ADD COLUMN sem_vida integer;
    UPDATE _seg SET reg_date = (ts AT TIME ZONE 'America/Bogota')::date;
    -- filtro de fechas (request.FechaDesde/Hasta) sobre la fecha local
    IF p_fecha_desde IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date < p_fecha_desde;
    END IF;
    IF p_fecha_hasta IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date > p_fecha_hasta;
    END IF;
    UPDATE _seg SET sem_vida = ((reg_date - v_enc_date) / 7) + 1;  -- división entera == C# (dias/7)+1
    -- REQ-012b: producción arranca en la semana 25 de vida (antes 26). La guía genética empieza en
    --   la semana 26, así que la 25 queda con columnas de guía en NULL (g_found=false ya lo soporta).
    DELETE FROM _seg WHERE sem_vida < 25;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: itera SOLO las semanas con registros (>=25 tras REQ-012b) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa, retiro) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN 25..v_max_sem LOOP  -- REQ-012b: incluir semana 25 (antes 26)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0),
               COALESCE(SUM(mv_venta_h),0), COALESCE(SUM(mv_venta_m),0),
               COALESCE(SUM(mv_retiro_h),0), COALESCE(SUM(mv_retiro_m),0),
               COALESCE(SUM(mv_out_h),0), COALESCE(SUM(mv_out_m),0),
               COALESCE(SUM(mv_in_h),0), COALESCE(SUM(mv_in_m),0), COALESCE(SUM(sel_m),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro,
               r_venta_h, r_venta_m, r_retiro_h, r_retiro_m,
               r_tras_out_h, r_tras_out_m, r_tras_in_h, r_tras_in_m, r_sel_m
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;

        -- REQ-004: acumulados de retiro (mortalidad + selección) por sexo. Desde
        --   20260806093256 los MACHOS también acumulan selección, igual que las hembras.
        v_cum_mort_h := v_cum_mort_h + r_mort_h;
        v_cum_sel_h  := v_cum_sel_h + r_sel_h;
        v_cum_mort_m := v_cum_mort_m + r_mort_m;
        v_cum_sel_m  := v_cum_sel_m + r_sel_m;
        r_htaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_tot::double precision / v_aves_h_ini ELSE 0 END;
        r_hiaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_inc::double precision / v_aves_h_ini ELSE 0 END;

        -- Peso aves (kg, REQ-004b): promedio de registros con valor NO NULO, luego normalizar.
        SELECT AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL),
               AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL),
               AVG(unif)   FILTER (WHERE unif   IS NOT NULL),
               AVG(cv)     FILTER (WHERE cv     IS NOT NULL),
               AVG(peso_huevo) FILTER (WHERE peso_huevo > 0)
          INTO r_peso_h, r_peso_m, r_unif, r_cv, r_peso_huevo
          FROM _seg WHERE sem_vida = s;
        IF r_peso_h IS NOT NULL THEN r_peso_h := CASE WHEN r_peso_h > 100 THEN r_peso_h/1000 ELSE r_peso_h END; END IF;
        IF r_peso_m IS NOT NULL THEN r_peso_m := CASE WHEN r_peso_m > 100 THEN r_peso_m/1000 ELSE r_peso_m END; END IF;

        -- %mortalidad / %selección: sobre el saldo REAL de inicio (avesActuales)
        r_porc_mort_h := CASE WHEN v_aves_h_act > 0 THEN r_mort_h::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_porc_mort_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_porc_sel_h  := CASE WHEN v_aves_h_act > 0 THEN r_sel_h::double precision  / v_aves_h_act * 100 ELSE 0 END;
        r_porc_sel_m  := CASE WHEN v_aves_m_act > 0 THEN r_sel_m::double precision  / v_aves_m_act * 100 ELSE 0 END;

        -- REQ-004: %Retiro REAL (== ProduccionCalculos.PorcentajeRetiroSemanal/Acumulado).
        --   Semanal: (mort + sel de la semana) / saldo REAL de inicio del sexo (v_aves_*_act, pre-decremento) * 100.
        --   Acumulado: (mort + sel acumulados) / aves iniciales del sexo * 100.
        r_retiro_sem_h := CASE WHEN v_aves_h_act > 0 THEN (r_mort_h + r_sel_h)::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_retiro_sem_m := CASE WHEN v_aves_m_act > 0 THEN (r_mort_m + r_sel_m)::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_retiro_ac_h  := CASE WHEN v_aves_h_ini > 0 THEN (v_cum_mort_h + v_cum_sel_h)::double precision / v_aves_h_ini * 100 ELSE 0 END;
        r_retiro_ac_m  := CASE WHEN v_aves_m_ini > 0 THEN (v_cum_mort_m + v_cum_sel_m)::double precision / v_aves_m_ini * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m + r_sel_m;

        -- ── Guía (una sola tabla) por Edad = semana de VIDA (s) ──
        g_found := false;
        SELECT true,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.uniformidad),'')::double precision,
               NULLIF(btrim(g.h_total_aa),'')::double precision,
               NULLIF(btrim(g.h_inc_aa),'')::double precision,
               NULLIF(btrim(g.prod_porcentaje),'')::double precision,
               NULLIF(btrim(g.peso_huevo),'')::double precision,
               NULLIF(btrim(g.retiro_ac_h),'')::double precision,
               NULLIF(btrim(g.retiro_ac_m),'')::double precision,
               g.origen
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo, g_retiro_ac_h, g_retiro_ac_m,
               g_origen
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         -- La semana 25 tiene DOS filas que parsean a 25: '25' (cierre de
         -- levante) y '25P' (arranque de producción), con valores muy distintos
         -- (retiro_ac_h 4,03 vs 0,10). Sin ORDER BY la que gana depende del
         -- plan y del orden físico de la tabla: hoy sale '25P' por el ctid, no
         -- por contrato. Se fija el desempate en la variante con sufijo —la de
         -- producción, que es la correcta acá y la que ya venía devolviendo—
         -- para que un VACUUM o un re-seed no cambien el reporte en silencio.
         ORDER BY (CASE WHEN btrim(g.edad) = s::text THEN 1 ELSE 0 END), g.id
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía "obtenerGuiaGeneticaProduccion" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            -- 🔴 Los COALESCE a 0 son EXCLUSIVOS de la guía compartida.
            -- Ahí la columna existe en toda la curva y el 0 se lee como «la guía dice 0»
            -- (y quitarlos NO sería delta cero: en el rango de producción, company 1 tiene
            -- entre 6 y 14 filas en blanco por columna). En la guía propia esas métricas
            -- NO EXISTEN —no trae peso, ni consumo de machos, ni mortalidad semanal— y el 0
            -- ahí no es «sin dato»: es un objetivo falso. Peor todavía, `fn_dif_pp` documenta
            -- que con guía = 0 NO devuelve NULL, así que la columna «diferencia vs guía» de
            -- mortalidad pintaría la mortalidad REAL del lote como si fuera la desviación.
            -- Con NULL, `fn_dif_pct`/`fn_dif_pp` degradan solas y el front pinta un guion.
            IF g_origen IS DISTINCT FROM 'propia' THEN
                g_cons_h := COALESCE(g_cons_h, 0);
                g_cons_m := COALESCE(g_cons_m, 0);
                g_mort_h := COALESCE(g_mort_h, 0);
                g_mort_m := COALESCE(g_mort_m, 0);
            END IF;
            -- El /1000 sí se aplica siempre (la guía viene en gramos y la salida en kg);
            -- lo condicional es el COALESCE, porque NULL/1000 = NULL y eso es lo correcto.
            g_peso_h := CASE WHEN g_origen = 'propia' THEN g_peso_h / 1000
                             ELSE COALESCE(g_peso_h, 0) / 1000 END;   -- peso_h/1000
            g_peso_m := CASE WHEN g_origen = 'propia' THEN g_peso_m / 1000
                             ELSE COALESCE(g_peso_m, 0) / 1000 END;   -- peso_m/1000
            -- ⚠️ EXCEPCIÓN DELIBERADA a la regla ParseDouble=>0 de sus vecinas: g_unif NO se
            --   coalescea. La guía genética no define uniformidad para las edades de PRODUCCIÓN
            --   (solo 25 de sus 98 filas la traen, todas de levante) ⇒ el 0 se pintaba en TODAS
            --   las semanas y se lee como «la guía exige 0 %» en vez de «sin dato», además de
            --   calcular la diferencia contra ese 0. Un 0 real tampoco existe como objetivo de
            --   uniformidad, así que NULL es la única lectura honesta.
            --   `diferencia_uniformidad` no se mueve: fn_dif_pct ya devolvía NULL con guía = 0.
            --   Los demás (cons/mort/peso/retiro_ac) SÍ conservan el 0: la guía los trae en toda
            --   la curva y cambiarlos movería números sin necesidad.
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
            -- retiro_ac_h/m guía: mismo criterio que mort_h/mort_m (ParseDouble => 0 si vacío).
            -- retiro_ac_h SÍ lo trae la guía propia (es su métrica de mortalidad, acumulada);
            -- retiro_ac_m no, y por eso el COALESCE queda condicionado igual que los de arriba.
            IF g_origen IS DISTINCT FROM 'propia' THEN
                g_retiro_ac_h := COALESCE(g_retiro_ac_h, 0);
                g_retiro_ac_m := COALESCE(g_retiro_ac_m, 0);
            END IF;
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
            g_retiro_ac_h := NULL; g_retiro_ac_m := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves. Además de mortalidad y selección descuenta VENTAS, retiros
        --   y salidas por traslado, y suma los ingresos: son aves que dejan (o entran a)
        --   el lote igual que las bajas. Misma composición que SaldoAvesLevanteCalculos.
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h
                                    - r_venta_h - r_retiro_h - r_tras_out_h + r_tras_in_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m - r_sel_m
                                    - r_venta_m - r_retiro_m - r_tras_out_m + r_tras_in_m);

        -- ── Emitir fila (respetando filtro semanaDesde/Hasta como en C#) ──
        IF (p_semana_desde IS NULL OR s >= p_semana_desde)
           AND (p_semana_hasta IS NULL OR s <= p_semana_hasta) THEN
            semana                           := s;
            fecha_inicio_semana              := v_enc_date + ((s - 1) * 7);
            fecha_fin_semana                 := v_enc_date + ((s - 1) * 7) + 6;
            total_registros                  := r_dias;
            mortalidad_hembras               := r_mort_h;
            mortalidad_machos                := r_mort_m;
            porcentaje_mortalidad_hembras    := r_porc_mort_h;
            porcentaje_mortalidad_machos     := r_porc_mort_m;
            mortalidad_guia_hembras          := g_mort_h;
            mortalidad_guia_machos           := g_mort_m;
            -- TK-2026-000023: la diferencia de MORTALIDAD es DIRECTA (puntos porcentuales),
            -- no porcentaje diferencial. Real y guia ya son porcentajes: restarlos da la
            -- distancia real (0,07 % vs 0,33 % => -0,26 pp). El porcentaje relativo
            -- ((real-guia)/guia*100) sobre numeros tan chicos explota: la pantalla llegaba a
            -- mostrar +2.212,10 % para 0,26 % contra 0,01 % de guia.
            -- Las demas diferencias (consumo, peso, huevos) SIGUEN relativas: ahi real y guia
            -- son magnitudes (kg, g, unidades), no porcentajes.
            diferencia_mortalidad_hembras    := fn_dif_pp(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pp(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            seleccion_machos                 := r_sel_m;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            porcentaje_seleccion_machos      := r_porc_sel_m;
            consumo_kg_hembras               := r_cons_kg_h;
            consumo_kg_machos                := r_cons_kg_m;
            consumo_total_kg                 := r_cons_kg_h + r_cons_kg_m;
            consumo_promedio_diario_kg       := CASE WHEN r_dias > 0 THEN (r_cons_kg_h + r_cons_kg_m)/r_dias ELSE 0 END;
            consumo_guia_hembras             := g_cons_h;
            consumo_guia_machos              := g_cons_m;
            diferencia_consumo_hembras       := fn_dif_pct(r_cons_real_h, g_cons_h);
            diferencia_consumo_machos        := fn_dif_pct(r_cons_real_m, g_cons_m);
            huevos_totales                   := r_huevos_tot;
            huevos_incubables                := r_huevos_inc;
            promedio_huevos_por_dia          := r_prom_huevos;
            eficiencia_produccion            := r_efic;
            huevos_totales_guia              := g_huevos_tot;
            huevos_incubables_guia           := g_huevos_inc;
            porcentaje_produccion_guia       := g_prod_pct;
            diferencia_huevos_totales        := fn_dif_pct(r_htaa, g_huevos_tot);
            diferencia_huevos_incubables     := fn_dif_pct(r_hiaa, g_huevos_inc);
            diferencia_porcentaje_produccion := fn_dif_pct(r_efic, g_prod_pct);
            peso_huevo_promedio              := r_peso_huevo;
            peso_huevo_guia                  := g_peso_huevo;
            diferencia_peso_huevo            := fn_dif_pct(r_peso_huevo, g_peso_huevo);
            peso_promedio_hembras            := r_peso_h;
            peso_promedio_machos             := r_peso_m;
            peso_guia_hembras                := g_peso_h;
            peso_guia_machos                 := g_peso_m;
            diferencia_peso_hembras          := fn_dif_pct(r_peso_h, g_peso_h);
            diferencia_peso_machos           := fn_dif_pct(r_peso_m, g_peso_m);
            uniformidad_promedio             := r_unif;
            uniformidad_guia                 := g_unif;
            diferencia_uniformidad           := fn_dif_pct(r_unif, g_unif);
            coeficiente_variacion_promedio   := r_cv;
            huevos_limpios                   := r_limpios;
            huevos_tratados                  := r_tratados;
            huevos_sucios                    := r_sucios;
            huevos_deformes                  := r_deformes;
            huevos_blancos                   := r_blancos;
            huevos_doble_yema                := r_doble_yema;
            huevos_piso                      := r_piso;
            huevos_pequenos                  := r_pequenos;
            huevos_rotos                     := r_rotos;
            huevos_desecho                   := r_desecho;
            huevos_otro                      := r_otro;
            aves_hembras_inicio_semana       := r_aves_h_inicio;
            aves_machos_inicio_semana        := r_aves_m_inicio;
            aves_hembras_fin_semana          := v_aves_h_act;
            aves_machos_fin_semana           := v_aves_m_act;
            htaa_real                        := r_htaa;
            hiaa_real                        := r_hiaa;
            retiro_sem_h                     := r_retiro_sem_h;
            retiro_sem_m                     := r_retiro_sem_m;
            retiro_ac_h                      := r_retiro_ac_h;
            retiro_ac_m                      := r_retiro_ac_m;
            retiro_ac_h_guia                 := g_retiro_ac_h;
            retiro_ac_m_guia                 := g_retiro_ac_m;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$function$
""";

    }
}

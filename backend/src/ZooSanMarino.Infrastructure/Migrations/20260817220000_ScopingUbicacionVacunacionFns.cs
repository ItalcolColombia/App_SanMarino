using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// W4 — Alcance por núcleo/galpón/lote en las DOS funciones de lectura del módulo Vacunación.
    ///
    /// <para>Hasta acá las dos filtraban sólo por granja (<c>user_farms</c>), así que un usuario
    /// restringido a un galpón veía en el combo todos los lotes de la granja y recibía en la bandeja
    /// pendientes que el guard del cronograma después le rechazaba. Ahora reciben el CIERRE de
    /// visibilidad ya calculado en C# (<c>UserLocationScopeCalculos</c>) como 4 arrays y prueban
    /// pertenencia a conjuntos: la lógica del alcance no se duplica en SQL.</para>
    ///
    /// <para>Data-only: no toca tablas, columnas ni índices. Las dos funciones suben JUNTAS a
    /// propósito — si subiera una sola, la bandeja mostraría lotes que los combos ya no ofrecen.
    /// Sincronizada con backend/sql/fn_vacunacion_filter_data.sql y fn_vacunacion_pendientes.sql.</para>
    ///
    /// <para>Usuario sin granjas restringidas ⇒ los 4 arrays van vacíos ⇒ salida idéntica a la previa.</para>
    /// </summary>
    public partial class ScopingUbicacionVacunacionFns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_FILTER_DATA, suppressTransaction: true);
            migrationBuilder.Sql(FN_PENDIENTES, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se retiran sólo las firmas NUEVAS. Volver a crear las viejas exigiría el cuerpo previo:
            // el rollback real es re-aplicar las migraciones AddFnVacunacionConsultas y
            // AddFnVacunacionPendientes, que traen su propio DROP + CREATE OR REPLACE.
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT, INT[], TEXT[], TEXT[], INT[]);",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.fn_vacunacion_pendientes(UUID, INT, INT, DATE, INT[], TEXT[], TEXT[], INT[], INT);",
                suppressTransaction: true);
        }

        // Copias literales de backend/sql/. Arrancan con DROP + CREATE OR REPLACE ⇒ re-ejecutables.
        private const string FN_FILTER_DATA = @"
-- ============================================================================
-- fn_vacunacion_filter_data — Datos de los combos del módulo Vacunación en UN
-- solo round trip: granjas asignadas al usuario (lite), lotes de las 3 líneas,
-- vacunas del catálogo y usuarios de la empresa (para ""aplicado por"").
--
-- Reemplaza las 5+ consultas secuenciales de VacunacionCronogramaService.GetFilterDataAsync
-- (granjas FarmDto completo + 3 tablas de lote + vacunas). La BD filtra, el backend orquesta.
--
-- Claves jsonb en camelCase 1:1 con VacunacionFilterDataDto (el wrapper C# deserializa
-- vía VacunacionFilterDataJson.Parse — sincronizar si se cambia una clave).
--
-- El id de ""usuarios"" es la CÉDULA parseada a int: es el UserId entero del sistema
-- (mismo mapeo que TicketService.BuildNotaUserInfoAsync). Se excluyen cédulas no
-- numéricas o fuera de rango int4 para no reventar el cast.
--
-- ⚠ ALCANCE GRANULAR (W4): los 4 parámetros p_scope_* son el CIERRE de visibilidad del
--   usuario, ya calculado en C# por UserLocationScopeCalculos.ComputeScope y aplanado por
--   AplanarParaSql. Acá NO se recalcula nada: sólo se prueba pertenencia a conjuntos.
--   · p_scope_farm_ids  = granjas del usuario con restrict_locations = true.
--   · p_scope_nucleos   = claves COMPUESTAS 'granjaId|nucleoId' (nucleo_id se repite entre granjas).
--   · p_scope_galpones  = ids de galpón (PK global) · p_scope_lotes = lotes.lote_id permitidos.
--   Una granja en p_scope_farm_ids y ausente del resto ⇒ CERO lotes (fail-closed).
--   Los 4 parámetros son OBLIGATORIOS a propósito (sin DEFAULT): un llamador que se los
--   olvide debe fallar, no ver toda la empresa.
--   La regla del CASE espeja a UserLocationScopeCalculos.PermiteUbicacion, que es su dueña.
--   Va UNA sola vez, después del UNION: repetirla por rama la volvería a duplicar (y un
--   helper SQL aparte rompería la restauración de dumps, que ordena funciones por OID).
--
-- Sincronizada con las migraciones AddFnVacunacionConsultas y ScopingUbicacionVacunacionFns
-- — si se edita este archivo, actualizar la migración también.
-- ============================================================================
DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT, INT[], TEXT[], TEXT[], INT[]);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_filter_data(
    p_user_guid       UUID,
    p_company_id      INT,
    p_pais_id         INT,
    p_scope_farm_ids  INT[],
    p_scope_nucleos   TEXT[],
    p_scope_galpones  TEXT[],
    p_scope_lotes     INT[]
)
RETURNS jsonb
LANGUAGE sql
STABLE
AS $$
WITH granjas AS (
    SELECT f.id, f.company_id, f.name
    FROM public.farms f
    WHERE f.company_id = p_company_id
      AND f.deleted_at IS NULL
      AND EXISTS (SELECT 1 FROM public.user_farms uf
                  WHERE uf.farm_id = f.id AND uf.user_id = p_user_guid)
      AND (p_pais_id IS NULL OR EXISTS (
            SELECT 1 FROM public.departamentos d
            WHERE d.departamento_id = f.departamento_id AND d.pais_id = p_pais_id))
),
lotes_ubicados AS (
    SELECT l.lote_postura_levante_id AS lote_id, 'Levante' AS linea_productiva,
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_cierre AS estado_cierre,
           l.lote_id AS lote_tabla_id
    FROM public.lote_postura_levante l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_levante_id IS NOT NULL

    UNION ALL

    SELECT l.lote_postura_produccion_id, 'Produccion',
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_cierre,
           l.lote_id
    FROM public.lote_postura_produccion l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_produccion_id IS NOT NULL

    UNION ALL

    -- Engorde no tiene FK a la tabla `lotes` ⇒ se gobierna por galpón/núcleo (limitación
    -- conocida del alcance granular, espejada en UserLocationScopeCalculos.PermiteUbicacion).
    SELECT l.lote_ave_engorde_id, 'Engorde',
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_operativo_lote,
           NULL::int
    FROM public.lote_ave_engorde l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_ave_engorde_id IS NOT NULL
),
lotes AS (
    -- Alcance granular. Espejo de UserLocationScopeCalculos.PermiteUbicacion (su dueña).
    SELECT u.* FROM lotes_ubicados u
    WHERE NOT (u.granja_id = ANY(COALESCE(p_scope_farm_ids, '{}'::int[])))
       OR CASE
            WHEN u.lote_tabla_id IS NOT NULL
                THEN u.lote_tabla_id = ANY(COALESCE(p_scope_lotes, '{}'::int[]))
            WHEN COALESCE(u.galpon_id, '') <> ''
                THEN u.galpon_id = ANY(COALESCE(p_scope_galpones, '{}'::text[]))
            WHEN COALESCE(u.nucleo_id, '') <> ''
                THEN (u.granja_id::text || '|' || u.nucleo_id) = ANY(COALESCE(p_scope_nucleos, '{}'::text[]))
            ELSE false
          END
),
vacunas AS (
    -- Case-insensitive: el dato real trae ""Vacuna""/""vacuna"" mezclado (paridad con EF ILike).
    SELECT i.id, i.codigo, i.nombre, i.unidad
    FROM public.item_inventario_ecuador i
    WHERE i.company_id = p_company_id AND i.activo = true AND i.tipo_item ILIKE 'vacuna'
),
usuarios AS (
    SELECT DISTINCT (u.cedula)::int AS id,
           NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE u.is_active = true
      AND u.cedula ~ '^[0-9]{1,18}$'
      AND (u.cedula)::bigint BETWEEN 1 AND 2147483647
      AND EXISTS (SELECT 1 FROM public.user_companies uc
                  WHERE uc.user_id = u.id AND uc.company_id = p_company_id)
)
SELECT jsonb_build_object(
    'granjas', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
                   'id', g.id, 'companyId', g.company_id, 'name', g.name)
               ORDER BY g.name)
        FROM granjas g), '[]'::jsonb),
    'lotes', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
                   'loteId', l.lote_id, 'lineaProductiva', l.linea_productiva,
                   'loteNombre', l.lote_nombre, 'granjaId', l.granja_id,
                   'nucleoId', l.nucleo_id, 'galponId', l.galpon_id,
                   -- ::date: serialización estable (sin hora/offset del timezone del servidor)
                   'fechaEncaset', l.fecha_encaset::date, 'estadoCierre', l.estado_cierre)
               -- El desempate por línea+id hace TOTAL el orden: sólo con fecha_encaset, dos lotes
               -- del mismo día salían en el orden que quisiera el plan y la lista cambiaba sola.
               ORDER BY l.fecha_encaset DESC NULLS LAST, l.linea_productiva, l.lote_id)
        FROM lotes l), '[]'::jsonb),
    'vacunas', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
                   'id', v.id, 'codigo', v.codigo, 'nombre', v.nombre, 'unidad', v.unidad)
               ORDER BY v.nombre)
        FROM vacunas v), '[]'::jsonb),
    'usuarios', COALESCE((
        SELECT jsonb_agg(jsonb_build_object('id', u.id, 'nombre', u.nombre)
               ORDER BY u.nombre NULLS LAST)
        FROM usuarios u), '[]'::jsonb)
);
$$;
";

        private const string FN_PENDIENTES = @"
-- ============================================================================
-- fn_vacunacion_pendientes — Bandeja ""hoy me toca"": las vacunas que un usuario
-- tiene sin registrar, de TODOS sus lotes vivos, en UN solo round trip.
--
-- Alimenta GET /api/VacunacionRegistro/pendientes y el panel del inicio. Sin esta
-- función habría que abrir lote por lote para saber qué falta.
--
-- ⚠ La fórmula de franja DEBE mantenerse idéntica a VacunacionCalculos.CalcularFranja
--   y a fn_vacunacion_cronograma_lote:
--     Semana → encaset + (valor-1)*7 · Dia → encaset + valor · Fecha → fecha_objetivo,
--     franja = [base - rango_dias_antes, base + rango_dias_despues].
--   Franja NULL (Semana/Dia sin fecha de encaset) ⇒ la fila NO entra en la bandeja:
--   no se inventa una fecha para apurar a nadie.
--
-- ⚠ La clasificación (Vencido / EnFranja / Proximo) espeja a
--   VacunacionPendientesCalculos.Clasificar, que es su especificación ejecutable
--   (tests xUnit). El día del fin de franja TODAVÍA cumple: hoy = fin ⇒ EnFranja.
--
-- ⚠ p_hoy viaja desde C# con DateTime.UtcNow.Date — la MISMA base con la que el
--   registro sella la aplicación. No se usa CURRENT_DATE: dependería de la zona
--   horaria de la sesión y la bandeja diría un día distinto que el guardado.
--
-- ⚠ ALCANCE: el filtro es el MISMO que el de fn_vacunacion_filter_data — granja
--   (user_farms + empresa + país) y, desde W4, ubicación granular. Las dos funciones se
--   cambian juntas: si una sube y la otra no, la bandeja muestra lotes que el resto del
--   módulo ya no deja ver.
--   Los 4 parámetros p_scope_* son el CIERRE de visibilidad ya calculado en C#
--   (UserLocationScopeCalculos.ComputeScope + AplanarParaSql); acá sólo se prueba
--   pertenencia a conjuntos. Núcleos con clave COMPUESTA 'granjaId|nucleoId'.
--   Granja en p_scope_farm_ids y ausente del resto ⇒ CERO filas (fail-closed).
--   Sin DEFAULT a propósito: el llamador que los olvide debe fallar, no ver toda la empresa.
--
-- Pendiente = sin registro, o con registro en estado 'Pendiente' (mismo criterio que
-- el guard de VacunacionRegistroService.CargarItemAsync y que el materializador).
-- Lote cerrado ⇒ fuera: se compara SIEMPRE por desigualdad contra 'Cerrado' (el dato
-- dice 'Abierto' y 'Abierta' según quién creó el lote).
--
-- Granja/núcleo/galpón salen del LOTE (dónde está hoy), no del ítem: el que va a
-- vacunar necesita la ubicación vigente.
--
-- Columnas snake_case (gotcha SqlQueryRaw + EFCore.NamingConventions), sin dígitos.
-- Sincronizada con las migraciones AddFnVacunacionPendientes y ScopingUbicacionVacunacionFns.
-- ============================================================================
DROP FUNCTION IF EXISTS public.fn_vacunacion_pendientes(UUID, INT, INT, DATE, INT);
DROP FUNCTION IF EXISTS public.fn_vacunacion_pendientes(UUID, INT, INT, DATE, INT[], TEXT[], TEXT[], INT[], INT);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_pendientes(
    p_user_guid       UUID,
    p_company_id      INT,
    p_pais_id         INT,
    p_hoy             DATE,
    -- Los scope_* van ANTES del horizonte: en Postgres, después de un parámetro con
    -- DEFAULT todos los siguientes deben tenerlo, y estos no lo llevan a propósito.
    p_scope_farm_ids  INT[],
    p_scope_nucleos   TEXT[],
    p_scope_galpones  TEXT[],
    p_scope_lotes     INT[],
    p_dias_horizonte  INT DEFAULT 7
)
RETURNS TABLE (
    cronograma_item_id     INT,
    linea_productiva       TEXT,
    lote_id                INT,
    lote_nombre            TEXT,
    granja_id              INT,
    granja_nombre          TEXT,
    nucleo_id              TEXT,
    galpon_id              TEXT,
    item_inventario_id     INT,
    item_inventario_nombre TEXT,
    unidad_objetivo        TEXT,
    valor_objetivo         INT,
    fecha_inicio_franja    DATE,
    fecha_fin_franja       DATE,
    situacion              TEXT,
    dias                   INT
)
LANGUAGE sql
STABLE
AS $$
WITH granjas AS (
    -- COPIA del alcance de fn_vacunacion_filter_data (ver nota de W4 en el encabezado).
    SELECT f.id, f.name
    FROM public.farms f
    WHERE f.company_id = p_company_id
      AND f.deleted_at IS NULL
      AND EXISTS (SELECT 1 FROM public.user_farms uf
                  WHERE uf.farm_id = f.id AND uf.user_id = p_user_guid)
      AND (p_pais_id IS NULL OR EXISTS (
            SELECT 1 FROM public.departamentos d
            WHERE d.departamento_id = f.departamento_id AND d.pais_id = p_pais_id))
),
lotes_ubicados AS (
    SELECT l.lote_postura_levante_id AS lote_id, 'Levante'::text AS linea,
           l.lote_nombre, l.fecha_encaset::date AS fecha_encaset,
           l.granja_id, l.nucleo_id, l.galpon_id,
           l.lote_id AS lote_tabla_id
    FROM public.lote_postura_levante l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_levante_id IS NOT NULL
      AND l.estado_cierre IS DISTINCT FROM 'Cerrado'

    UNION ALL

    SELECT l.lote_postura_produccion_id, 'Produccion'::text,
           l.lote_nombre, l.fecha_encaset::date,
           l.granja_id, l.nucleo_id, l.galpon_id,
           l.lote_id
    FROM public.lote_postura_produccion l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_produccion_id IS NOT NULL
      AND l.estado_cierre IS DISTINCT FROM 'Cerrado'

    UNION ALL

    -- Engorde no tiene FK a la tabla `lotes` ⇒ galpón/núcleo lo gobiernan.
    SELECT l.lote_ave_engorde_id, 'Engorde'::text,
           l.lote_nombre, l.fecha_encaset::date,
           l.granja_id, l.nucleo_id, l.galpon_id,
           NULL::int
    FROM public.lote_ave_engorde l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_ave_engorde_id IS NOT NULL
      AND l.estado_operativo_lote IS DISTINCT FROM 'Cerrado'
),
lotes AS (
    -- Alcance granular. Espejo de UserLocationScopeCalculos.PermiteUbicacion (su dueña) y
    -- de fn_vacunacion_filter_data: las dos funciones filtran con la MISMA expresión.
    SELECT u.* FROM lotes_ubicados u
    WHERE NOT (u.granja_id = ANY(COALESCE(p_scope_farm_ids, '{}'::int[])))
       OR CASE
            WHEN u.lote_tabla_id IS NOT NULL
                THEN u.lote_tabla_id = ANY(COALESCE(p_scope_lotes, '{}'::int[]))
            WHEN COALESCE(u.galpon_id, '') <> ''
                THEN u.galpon_id = ANY(COALESCE(p_scope_galpones, '{}'::text[]))
            WHEN COALESCE(u.nucleo_id, '') <> ''
                THEN (u.granja_id::text || '|' || u.nucleo_id) = ANY(COALESCE(p_scope_nucleos, '{}'::text[]))
            ELSE false
          END
),
base AS (
    SELECT
        ci.id AS cronograma_item_id,
        ci.linea_productiva,
        lo.lote_id, lo.lote_nombre, lo.fecha_encaset,
        lo.granja_id, lo.nucleo_id, lo.galpon_id,
        ci.item_inventario_id, ci.unidad_objetivo, ci.valor_objetivo, ci.fecha_objetivo,
        ci.rango_dias_antes, ci.rango_dias_despues, ci.orden
    FROM public.vacunacion_cronograma_item ci
    JOIN lotes lo
      ON lo.linea = ci.linea_productiva
     AND lo.lote_id = COALESCE(ci.lote_postura_levante_id,
                               ci.lote_postura_produccion_id,
                               ci.lote_ave_engorde_id)
    LEFT JOIN public.vacunacion_registro_aplicacion ra
      ON ra.vacunacion_cronograma_item_id = ci.id
    WHERE ci.company_id = p_company_id
      AND ci.deleted_at IS NULL
      AND ci.activo = true
      AND (ra.id IS NULL OR ra.estado = 'Pendiente')
),
franja AS (
    SELECT b.*,
        (CASE b.unidad_objetivo
            WHEN 'Semana' THEN CASE WHEN b.fecha_encaset IS NOT NULL AND b.valor_objetivo IS NOT NULL
                                    THEN b.fecha_encaset + ((b.valor_objetivo - 1) * 7) END
            WHEN 'Dia'    THEN CASE WHEN b.fecha_encaset IS NOT NULL AND b.valor_objetivo IS NOT NULL
                                    THEN b.fecha_encaset + b.valor_objetivo END
            WHEN 'Fecha'  THEN b.fecha_objetivo::date
         END) AS fecha_base
    FROM base b
),
clasificado AS (
    SELECT f.*,
        (f.fecha_base - f.rango_dias_antes)   AS inicio,
        (f.fecha_base + f.rango_dias_despues) AS fin
    FROM franja f
    WHERE f.fecha_base IS NOT NULL
)
SELECT
    c.cronograma_item_id,
    c.linea_productiva,
    c.lote_id,
    COALESCE(c.lote_nombre, '')  AS lote_nombre,
    c.granja_id,
    g.name                       AS granja_nombre,
    c.nucleo_id,
    c.galpon_id,
    c.item_inventario_id,
    COALESCE(ii.nombre, '')      AS item_inventario_nombre,
    c.unidad_objetivo,
    c.valor_objetivo,
    c.inicio                     AS fecha_inicio_franja,
    c.fin                        AS fecha_fin_franja,
    CASE WHEN c.fin < p_hoy    THEN 'Vencido'
         WHEN c.inicio <= p_hoy THEN 'EnFranja'
         ELSE 'Proximo'
    END                          AS situacion,
    CASE WHEN c.fin < p_hoy     THEN (p_hoy - c.fin)
         WHEN c.inicio <= p_hoy THEN 0
         ELSE -(c.inicio - p_hoy)
    END                          AS dias
FROM clasificado c
LEFT JOIN granjas g ON g.id = c.granja_id
LEFT JOIN public.item_inventario_ecuador ii ON ii.id = c.item_inventario_id
-- El horizonte recorta SÓLO lo que viene: un vencido de hace un año sigue siendo pendiente.
WHERE c.fin < p_hoy
   OR c.inicio <= p_hoy
   OR (c.inicio - p_hoy) <= GREATEST(COALESCE(p_dias_horizonte, 0), 0)
ORDER BY
    CASE WHEN c.fin < p_hoy THEN 0 WHEN c.inicio <= p_hoy THEN 1 ELSE 2 END,
    CASE WHEN c.fin < p_hoy THEN (p_hoy - c.fin) ELSE 0 END DESC,
    c.inicio,
    c.orden,
    c.cronograma_item_id;
$$;
";
    }
}

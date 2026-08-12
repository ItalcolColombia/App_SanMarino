using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations;

/// <summary>
/// Power BI: expone el consumo de alimento MIXTO de pollo engorde como columna propia.
///
/// Contexto (mismo defecto que se corrigió en la pantalla del seguimiento diario): desde el día 8
/// el galpón come una sola ración, pero se persiste en <c>consumo_kg_hembras</c> con machos en 0.
/// La vista la publicaba tal cual, documentada como «consumo por sexo», así que en Power BI la
/// ración mixta se grafica como si la comieran solo las hembras. Los días 1–7 sí traen desglose
/// real: esas filas las genera el cruce de lotes reproductora y nacen firmadas por SYSTEM_CRUCE.
///
/// El cambio es ADITIVO: <c>consumo_kg_hembras</c>, <c>consumo_kg_machos</c> y
/// <c>consumo_real_dia_kg</c> quedan intactos ⇒ ningún reporte existente cambia de número. Se
/// agregan al final <c>consumo_kg_mixto</c> y <c>consumo_es_mixto</c>.
///
/// ⚠️ Por qué se ENVUELVE la definición viva en vez de reescribir la vista: el espejo del repo
/// (<c>backend/sql/seguimiento_pollo_engorde_tabla_unificada_vista.sql</c>) está desincronizado —
/// nombra la vista <c>vw_seguimiento_pollo_engorde_unificado</c> y le faltan 14 columnas que la
/// vista desplegada sí tiene (tipo_fila, uniformidad_*, cv_*, ciclo, despacho_peso_*,
/// created_by_user_id…). Recrearla desde ese archivo BORRARÍA esas columnas en producción. Leyendo
/// <c>pg_get_viewdef</c> se conserva exactamente lo que haya en cada ambiente y solo se agregan las
/// dos columnas al final, que es justo lo que CREATE OR REPLACE VIEW permite.
/// </summary>
public partial class AddConsumoMixtoVistaPowerbiEngorde : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotente y fail-safe: recorre los nombres conocidos de la vista, salta las que no
        // existen, las que ya tienen la columna y las que no traigan los 3 campos de los que deriva.
        migrationBuilder.Sql(@"
DO $mig$
DECLARE
    v_nombre text;
    v_def    text;
BEGIN
    FOREACH v_nombre IN ARRAY ARRAY[
        'vw_seguimiento_pollo_engorde',
        'vw_seguimiento_pollo_engorde_unificado'
    ]
    LOOP
        IF to_regclass('public.' || quote_ident(v_nombre)) IS NULL THEN
            CONTINUE;
        END IF;

        IF EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = v_nombre
                      AND column_name = 'consumo_kg_mixto') THEN
            CONTINUE;
        END IF;

        IF (SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = v_nombre
               AND column_name IN ('consumo_kg_machos', 'consumo_real_dia_kg', 'created_by_user_id')) <> 3 THEN
            RAISE NOTICE 'Vista % sin las columnas base del consumo: se omite', v_nombre;
            CONTINUE;
        END IF;

        v_def := rtrim(btrim(pg_get_viewdef(('public.' || quote_ident(v_nombre))::regclass, true)), ';');

        EXECUTE format(
            'CREATE OR REPLACE VIEW public.%I AS
             SELECT v.*,
                    CASE
                        WHEN COALESCE(v.created_by_user_id, '''') = ''SYSTEM_CRUCE''
                          OR COALESCE(v.consumo_kg_machos, 0) > 0
                        THEN NULL::numeric
                        ELSE v.consumo_real_dia_kg
                    END AS consumo_kg_mixto,
                    NOT (COALESCE(v.created_by_user_id, '''') = ''SYSTEM_CRUCE''
                         OR COALESCE(v.consumo_kg_machos, 0) > 0) AS consumo_es_mixto
               FROM (%s) v',
            v_nombre, v_def);

        RAISE NOTICE 'Vista %: consumo_kg_mixto / consumo_es_mixto agregadas', v_nombre;
    END LOOP;
END
$mig$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // CREATE OR REPLACE no puede QUITAR columnas ⇒ DROP + CREATE proyectando todo menos las dos
        // nuevas. La definición vuelve a salir de pg_get_viewdef, así que no se pierde nada de lo que
        // el ambiente tuviera.
        migrationBuilder.Sql(@"
DO $mig$
DECLARE
    v_nombre text;
    v_def    text;
    v_cols   text;
BEGIN
    FOREACH v_nombre IN ARRAY ARRAY[
        'vw_seguimiento_pollo_engorde',
        'vw_seguimiento_pollo_engorde_unificado'
    ]
    LOOP
        IF to_regclass('public.' || quote_ident(v_nombre)) IS NULL THEN
            CONTINUE;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = v_nombre
                          AND column_name = 'consumo_kg_mixto') THEN
            CONTINUE;
        END IF;

        SELECT string_agg(quote_ident(column_name), ', ' ORDER BY ordinal_position)
          INTO v_cols
          FROM information_schema.columns
         WHERE table_schema = 'public' AND table_name = v_nombre
           AND column_name NOT IN ('consumo_kg_mixto', 'consumo_es_mixto');

        v_def := rtrim(btrim(pg_get_viewdef(('public.' || quote_ident(v_nombre))::regclass, true)), ';');

        EXECUTE format('DROP VIEW public.%I', v_nombre);
        EXECUTE format('CREATE VIEW public.%I AS SELECT %s FROM (%s) v', v_nombre, v_cols, v_def);
    END LOOP;
END
$mig$;
");
    }
}

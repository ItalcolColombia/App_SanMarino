using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Feature 14 (UX): renombrar las columnas de acumulado de traslado para
    /// reflejar la fase del lote en que ocurrió el movimiento.
    ///
    ///   lote_postura_levante.traslado_*    → levante_traslado_*
    ///   lote_postura_produccion.traslado_* → produccion_traslado_*
    ///
    /// Idempotente — usa IF EXISTS para que conviva con SQL aplicados manualmente.
    /// </summary>
    public partial class RenameTrasladoColumnsPerFase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── lote_postura_levante: prefijo "levante_" ─────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_levante'
                               AND column_name = 'traslado_ingreso_hembras') THEN
                    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_ingreso_hembras TO levante_traslado_ingreso_hembras;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_levante'
                               AND column_name = 'traslado_ingreso_machos') THEN
                    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_ingreso_machos TO levante_traslado_ingreso_machos;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_levante'
                               AND column_name = 'traslado_salida_hembras') THEN
                    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_salida_hembras TO levante_traslado_salida_hembras;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_levante'
                               AND column_name = 'traslado_salida_machos') THEN
                    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_salida_machos TO levante_traslado_salida_machos;
                  END IF;
                END $$;
            ");

            // ── lote_postura_produccion: prefijo "produccion_" ──────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_produccion'
                               AND column_name = 'traslado_ingreso_hembras') THEN
                    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_ingreso_hembras TO produccion_traslado_ingreso_hembras;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_produccion'
                               AND column_name = 'traslado_ingreso_machos') THEN
                    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_ingreso_machos TO produccion_traslado_ingreso_machos;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_produccion'
                               AND column_name = 'traslado_salida_hembras') THEN
                    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_salida_hembras TO produccion_traslado_salida_hembras;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_produccion'
                               AND column_name = 'traslado_salida_machos') THEN
                    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_salida_machos TO produccion_traslado_salida_machos;
                  END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_levante'
                               AND column_name = 'levante_traslado_ingreso_hembras') THEN
                    ALTER TABLE lote_postura_levante RENAME COLUMN levante_traslado_ingreso_hembras TO traslado_ingreso_hembras;
                    ALTER TABLE lote_postura_levante RENAME COLUMN levante_traslado_ingreso_machos  TO traslado_ingreso_machos;
                    ALTER TABLE lote_postura_levante RENAME COLUMN levante_traslado_salida_hembras  TO traslado_salida_hembras;
                    ALTER TABLE lote_postura_levante RENAME COLUMN levante_traslado_salida_machos   TO traslado_salida_machos;
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns
                             WHERE table_name = 'lote_postura_produccion'
                               AND column_name = 'produccion_traslado_ingreso_hembras') THEN
                    ALTER TABLE lote_postura_produccion RENAME COLUMN produccion_traslado_ingreso_hembras TO traslado_ingreso_hembras;
                    ALTER TABLE lote_postura_produccion RENAME COLUMN produccion_traslado_ingreso_machos  TO traslado_ingreso_machos;
                    ALTER TABLE lote_postura_produccion RENAME COLUMN produccion_traslado_salida_hembras  TO traslado_salida_hembras;
                    ALTER TABLE lote_postura_produccion RENAME COLUMN produccion_traslado_salida_machos   TO traslado_salida_machos;
                  END IF;
                END $$;
            ");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase3AugmentSeguimientoProduccionCanonica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Aumento ADITIVO e IDEMPOTENTE de la canónica de producción
            // (seguimiento_diario_produccion_reproductoras) con las columnas
            // traslado + sexaje + auditoría que usan los writers que se repuntan
            // en Fase 3-B (SeguimientoProduccionService + rama producción de
            // TrasladoAvesDesdeSegService). Las NOT NULL traen DEFAULT para
            // backfillear las filas existentes.
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_produccion_reproductoras
                    ADD COLUMN IF NOT EXISTS error_sexaje_hembras            integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS error_sexaje_machos             integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras        integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos         integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_hembras         integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_machos          integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS es_traslado                     boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS traslado_direccion              character varying(10),
                    ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id    integer,
                    ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id  integer,
                    ADD COLUMN IF NOT EXISTS traslado_hembras                integer,
                    ADD COLUMN IF NOT EXISTS traslado_machos                 integer,
                    ADD COLUMN IF NOT EXISTS lote_destino_id                 integer,
                    ADD COLUMN IF NOT EXISTS granja_destino_id               integer,
                    ADD COLUMN IF NOT EXISTS fecha_traslado                  timestamp without time zone,
                    ADD COLUMN IF NOT EXISTS traslado_observaciones          character varying(500),
                    ADD COLUMN IF NOT EXISTS company_id                      integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS created_by_user_id              integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS created_at                      timestamp with time zone NOT NULL DEFAULT now(),
                    ADD COLUMN IF NOT EXISTS updated_by_user_id              integer,
                    ADD COLUMN IF NOT EXISTS updated_at                      timestamp with time zone,
                    ADD COLUMN IF NOT EXISTS deleted_at                      timestamp with time zone;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_produccion_reproductoras
                    DROP COLUMN IF EXISTS error_sexaje_hembras,
                    DROP COLUMN IF EXISTS error_sexaje_machos,
                    DROP COLUMN IF EXISTS traslado_ingreso_hembras,
                    DROP COLUMN IF EXISTS traslado_ingreso_machos,
                    DROP COLUMN IF EXISTS traslado_salida_hembras,
                    DROP COLUMN IF EXISTS traslado_salida_machos,
                    DROP COLUMN IF EXISTS es_traslado,
                    DROP COLUMN IF EXISTS traslado_direccion,
                    DROP COLUMN IF EXISTS traslado_lote_contraparte_id,
                    DROP COLUMN IF EXISTS traslado_granja_contraparte_id,
                    DROP COLUMN IF EXISTS traslado_hembras,
                    DROP COLUMN IF EXISTS traslado_machos,
                    DROP COLUMN IF EXISTS lote_destino_id,
                    DROP COLUMN IF EXISTS granja_destino_id,
                    DROP COLUMN IF EXISTS fecha_traslado,
                    DROP COLUMN IF EXISTS traslado_observaciones,
                    DROP COLUMN IF EXISTS company_id,
                    DROP COLUMN IF EXISTS created_by_user_id,
                    DROP COLUMN IF EXISTS created_at,
                    DROP COLUMN IF EXISTS updated_by_user_id,
                    DROP COLUMN IF EXISTS updated_at,
                    DROP COLUMN IF EXISTS deleted_at;
            ");
        }
    }
}

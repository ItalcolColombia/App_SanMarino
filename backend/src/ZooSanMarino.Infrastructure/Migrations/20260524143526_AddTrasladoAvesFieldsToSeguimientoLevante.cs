using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrasladoAvesFieldsToSeguimientoLevante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // seguimiento_lote_levante — traslado columns
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS fecha_traslado date NULL;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS granja_destino_id integer NULL;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS lote_destino_id integer NULL;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS traslado_hembras integer NULL;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS traslado_machos integer NULL;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS traslado_observaciones character varying(500) NULL;");

            // produccion_seguimiento — traslado columns
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento ADD COLUMN IF NOT EXISTS fecha_traslado date NULL;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento ADD COLUMN IF NOT EXISTS granja_destino_id integer NULL;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento ADD COLUMN IF NOT EXISTS lote_destino_id integer NULL;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento ADD COLUMN IF NOT EXISTS traslado_hembras integer NULL;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento ADD COLUMN IF NOT EXISTS traslado_machos integer NULL;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento ADD COLUMN IF NOT EXISTS traslado_observaciones character varying(500) NULL;");

            // movimiento_pollo_engorde — peso real columns
            migrationBuilder.Sql("ALTER TABLE movimiento_pollo_engorde ADD COLUMN IF NOT EXISTS peso_bruto_real double precision NULL;");
            migrationBuilder.Sql("ALTER TABLE movimiento_pollo_engorde ADD COLUMN IF NOT EXISTS peso_tara_real double precision NULL;");

            // lote_ave_engorde — fecha_alistamiento
            migrationBuilder.Sql("ALTER TABLE lote_ave_engorde ADD COLUMN IF NOT EXISTS fecha_alistamiento timestamp with time zone NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante DROP COLUMN IF EXISTS fecha_traslado;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante DROP COLUMN IF EXISTS granja_destino_id;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante DROP COLUMN IF EXISTS lote_destino_id;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante DROP COLUMN IF EXISTS traslado_hembras;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante DROP COLUMN IF EXISTS traslado_machos;");
            migrationBuilder.Sql("ALTER TABLE seguimiento_lote_levante DROP COLUMN IF EXISTS traslado_observaciones;");

            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento DROP COLUMN IF EXISTS fecha_traslado;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento DROP COLUMN IF EXISTS granja_destino_id;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento DROP COLUMN IF EXISTS lote_destino_id;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento DROP COLUMN IF EXISTS traslado_hembras;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento DROP COLUMN IF EXISTS traslado_machos;");
            migrationBuilder.Sql("ALTER TABLE produccion_seguimiento DROP COLUMN IF EXISTS traslado_observaciones;");

            migrationBuilder.Sql("ALTER TABLE movimiento_pollo_engorde DROP COLUMN IF EXISTS peso_bruto_real;");
            migrationBuilder.Sql("ALTER TABLE movimiento_pollo_engorde DROP COLUMN IF EXISTS peso_tara_real;");

            migrationBuilder.Sql("ALTER TABLE lote_ave_engorde DROP COLUMN IF EXISTS fecha_alistamiento;");
        }
    }
}

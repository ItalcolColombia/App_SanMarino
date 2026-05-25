using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmIdErpCreateToLotePosturaBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: safe to run even if columns already exist
            migrationBuilder.Sql("ALTER TABLE lote_postura_base ADD COLUMN IF NOT EXISTS farm_id    integer NULL;");
            migrationBuilder.Sql("ALTER TABLE lote_postura_base ADD COLUMN IF NOT EXISTS erp_create date    NULL;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_lote_postura_base_farm_id ON lote_postura_base(farm_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_lote_postura_base_farm_id;");
            migrationBuilder.Sql("ALTER TABLE lote_postura_base DROP COLUMN IF EXISTS farm_id;");
            migrationBuilder.Sql("ALTER TABLE lote_postura_base DROP COLUMN IF EXISTS erp_create;");
        }
    }
}

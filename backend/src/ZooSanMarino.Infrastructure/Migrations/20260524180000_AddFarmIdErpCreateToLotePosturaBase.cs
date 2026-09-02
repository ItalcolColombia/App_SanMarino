using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega <c>lote_postura_base.farm_id</c> y <c>erp_create</c> mas su indice.
    /// </summary>
    /// <remarks>
    /// <b>Nació sin su <c>.Designer.cs</c></b>, o sea sin el atributo <c>[Migration]</c>:
    /// <c>MigrationsAssembly</c> descubre migraciones filtrando por ese atributo, así que para EF
    /// esta clase no existía —no salía en <c>migrations list</c> ni se aplicaba en ningún deploy—.
    /// El schema se aplicó a mano con <c>backend/sql/053_sync_produccion_traslados_prod.sql</c>, que
    /// además insertaba el id en <c>__EFMigrationsHistory</c>. El Designer se le escribió el
    /// 2-sep-2026; el SQL no se tocó porque ya era idempotente, que es justamente lo que hace que
    /// volverse visible sea seguro: donde el id no esté registrado, EF la corre y no pasa nada.
    /// </remarks>
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

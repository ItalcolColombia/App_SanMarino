using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos: clona el catálogo de ítems de Ecuador (company 3 / país 2) a Panamá
    /// (ItalcolPanama, company 5 / país 3) como BASE editable — Panamá no tenía ítems (0).
    /// IDEMPOTENTE (guard NOT EXISTS sobre la unique company_id+pais_id+codigo). No cambia schema
    /// (Up/Down generados vacíos por EF → sin drift de modelo). Fuente/spec:
    /// backend/sql/seed_item_inventario_panama_desde_ecuador.sql.
    /// Down = no-op a propósito: revertir borraría ítems que el usuario ya haya curado/creado en Panamá.
    /// </summary>
    public partial class SeedItemInventarioPanamaDesdeEcuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO item_inventario_ecuador
    (codigo, nombre, tipo_item, unidad, descripcion, activo,
     grupo, tipo_inventario_codigo, descripcion_tipo_inventario, referencia, descripcion_item, concepto,
     company_id, pais_id, created_at, updated_at)
SELECT
    src.codigo, src.nombre, src.tipo_item, src.unidad, src.descripcion, src.activo,
    src.grupo, src.tipo_inventario_codigo, src.descripcion_tipo_inventario, src.referencia, src.descripcion_item, src.concepto,
    5 AS company_id, 3 AS pais_id, now() AS created_at, now() AS updated_at
FROM item_inventario_ecuador AS src
WHERE src.company_id = 3
  AND src.pais_id = 2
  AND NOT EXISTS (
      SELECT 1
      FROM item_inventario_ecuador AS dst
      WHERE dst.company_id = 5
        AND dst.pais_id = 3
        AND dst.codigo = src.codigo
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seed de datos: no se revierte (borrar ítems de Panamá podría eliminar los ya curados/creados).
        }
    }
}

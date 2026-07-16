using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Alinea la BD con el código (el código manda): Farm.RegionalId es nullable en la entidad y
    /// FarmService.CreateAsync acepta null ("null OK"), pero en algunas BDs farms.regional_id sigue
    /// NOT NULL — la migración que lo relajaba (20251002005503) figura en __EFMigrationsHistory sin
    /// haberse ejecutado (incidente de historial) y el DROP NOT NULL quedó en un script manual
    /// (allow_null_regional_id_farms.sql) que no corre en el deploy. Idempotente: solo altera si la
    /// columna sigue NOT NULL. Necesario para el puente Panamá (crea granjas sin regional).
    /// </summary>
    public partial class FixFarmsRegionalIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'farms'
                          AND column_name = 'regional_id' AND is_nullable = 'NO'
                    ) THEN
                        ALTER TABLE public.farms ALTER COLUMN regional_id DROP NOT NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se restaura NOT NULL: podría haber filas con null legítimo (el código lo permite).
        }
    }
}

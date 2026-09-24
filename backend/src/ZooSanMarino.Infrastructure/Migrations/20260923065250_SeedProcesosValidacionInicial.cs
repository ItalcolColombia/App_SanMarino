using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: siembra el catálogo global de procesos soportados por el motor de
    /// flujos de validación (plan §11.2). Localiza por <c>key</c>, nunca por id fijo. NO siembra
    /// personas/roles de ninguna empresa: eso se configura desde la UI después del despliegue
    /// (decisión confirmada por el usuario, 23-sep-2026).
    /// </summary>
    public partial class SeedProcesosValidacionInicial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO public.validacion_procesos (key, nombre, descripcion, adapter_key, menu_route, is_active, orden)
SELECT v.key, v.nombre, v.descripcion, v.adapter_key, v.menu_route, true, v.orden
FROM (VALUES
    ('SEGUIMIENTO_LEVANTE', 'Seguimiento diario de Levante',
     'Validación secuencial del seguimiento diario de Levante antes de aplicar alimento y aves.',
     'SEGUIMIENTO_LEVANTE', NULL::text, 1),
    ('SEGUIMIENTO_PRODUCCION', 'Seguimiento diario de Producción',
     'Validación secuencial del seguimiento diario de Producción antes de aplicar alimento y aves.',
     'SEGUIMIENTO_PRODUCCION', NULL::text, 2)
) AS v(key, nombre, descripcion, adapter_key, menu_route, orden)
WHERE NOT EXISTS (SELECT 1 FROM public.validacion_procesos p WHERE p.key = v.key);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.validacion_procesos
WHERE key IN ('SEGUIMIENTO_LEVANTE', 'SEGUIMIENTO_PRODUCCION');
");
        }
    }
}

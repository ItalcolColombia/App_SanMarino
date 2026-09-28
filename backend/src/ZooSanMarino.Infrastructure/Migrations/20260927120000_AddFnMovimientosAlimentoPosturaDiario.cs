using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations;

/// <summary>
/// Lleva a PostgreSQL la selección por silo/galpón/granja y la agrupación diaria de ingresos y
/// traslados de alimento que consumen las grillas de Levante y Producción.
/// Espejo: backend/sql/fn_movimientos_alimento_postura_diario.sql.
/// </summary>
public partial class AddFnMovimientosAlimentoPosturaDiario : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(FnMovimientosAlimentoPosturaDiario);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_movimientos_alimento_postura_diario(int, int, int, text, text, date, date);");
}

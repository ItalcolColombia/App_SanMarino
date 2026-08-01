using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 2b del plan seguimiento_produccion_fn_canonica: las 3 fns semanales de producción
    /// (fn_indicadores_produccion_postura, fn_clasificacion_huevo_items_produccion,
    /// fn_resumen_semanal_ra_pesadas_produccion) dejan de repetir el bloque
    /// «UNION dual-fuente + DISTINCT ON día Bogotá» (copiado 3×) y se apoyan en
    /// fn_seguimiento_diario_produccion (única fórmula, filas seg_id IS NOT NULL).
    /// Su aritmética semanal NO cambia: salida verificada BYTE A BYTE contra la versión
    /// anterior en todas las empresas con producción (baselines congelados, ver tracker).
    /// Nota RA Pesadas: el espejo backend/sql traía un PARTITION BY fin_sem en `part` nunca
    /// migrado (part=1 con encasets distintos); quedó realineado a la ventana global desplegada.
    /// Down() restaura las 3 versiones anteriores verbatim.
    /// </summary>
    public partial class FnsSemanalesProduccionSobreFnDiaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnIndicadoresNueva);
            migrationBuilder.Sql(FnClasificacionNueva);
            migrationBuilder.Sql(FnResumenNueva);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnIndicadoresPrev);
            migrationBuilder.Sql(FnClasificacionPrev);
            migrationBuilder.Sql(FnResumenPrev);
        }
    }
}

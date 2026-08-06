using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Alinea el modelo con la columna: <c>seguimiento_diario_produccion.peso_huevo</c> YA era
    /// nullable en la base, pero la entidad la declaraba <c>decimal</c> no anulable.
    ///
    /// <para>
    /// Mientras nadie escribió un NULL ahí la desalineación pasó inadvertida. En cuanto un día llega
    /// sin pesaje de huevo —normal: no se pesa todos los días— EF revienta la consulta ENTERA con
    /// <c>Column 'PesoHuevo' is null</c> y el reporte técnico de producción devuelve 500. Sus
    /// columnas hermanas (<c>peso_h</c>, <c>peso_m</c>, <c>uniformidad</c>) siempre fueron anulables;
    /// <c>peso_huevo</c> era la única excepción.
    /// </para>
    ///
    /// <para>
    /// El DDL es un no-op en cualquier base donde la columna ya sea nullable — que es el caso
    /// esperado —; existe para que el modelo y el esquema queden declarados igual y EF deje de
    /// reportar cambios pendientes. Los consumidores que necesitan un valor usan <c>?? 0</c>, que es
    /// la convención que el resto del código ya aplicaba (<c>if (PesoHuevo &gt; 0)</c> = «hay pesaje»).
    /// </para>
    /// </summary>
    public partial class AlinearPesoHuevoProduccionANullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE public.seguimiento_diario_produccion ALTER COLUMN peso_huevo DROP NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver a NOT NULL exige que no queden nulos: se rellenan con 0, que es el valor que
            // el codigo interpreta como «sin pesaje».
            migrationBuilder.Sql(@"
UPDATE public.seguimiento_diario_produccion SET peso_huevo = 0 WHERE peso_huevo IS NULL;
ALTER TABLE public.seguimiento_diario_produccion ALTER COLUMN peso_huevo SET DEFAULT 0;
ALTER TABLE public.seguimiento_diario_produccion ALTER COLUMN peso_huevo SET NOT NULL;
");
        }
    }
}

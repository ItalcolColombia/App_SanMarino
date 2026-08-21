using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag tipado <c>companies.separa_lotes_postura_por_etapa</c>: el listado de lotes de postura
    /// suma las pestañas <b>Levante</b> y <b>Producción</b>, cada una con los lotes de esa etapa,
    /// además de la lista completa.
    ///
    /// <para>
    /// <b>Qué reactiva.</b> Esas dos pestañas existían y se comentaron en el HTML el 25 de mayo de
    /// 2026 (commit <c>cd9b1a7</c>). Vuelven detrás de un flag en vez de para todos, porque separar
    /// el listado en tres vistas es una decisión de cada empresa, no del producto.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué recién ahora se puede.</b> La etapa de cada lote se derivaba de su edad
    /// (≥ 26 semanas desde el encaset ⇒ Producción), así que las pestañas habrían repartido mal los
    /// lotes cargados con historia — medido: 8 de los 16 lotes de Sanmarino. Desde <c>d71fd7a</c> la
    /// etapa la decide el ESTADO (levante cerrado + lote de producción creado,
    /// <c>FaseLoteCalculos.ResolverFaseVisible</c>), que es lo que hace confiable la separación.
    /// </para>
    ///
    /// <para>
    /// <b>Nace apagado en TODAS las empresas</b> (fail-closed, igual que el resto de los flags): sin
    /// encenderlo, la pantalla queda exactamente como está hoy. Se administra desde Empresas, con
    /// los demás flags; esta migración no lo enciende para nadie.
    /// </para>
    ///
    /// Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>): re-ejecutable sin romper.
    /// </summary>
    public partial class AddFlagSeparaLotesPosturaPorEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS separa_lotes_postura_por_etapa boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS separa_lotes_postura_por_etapa;");
        }
    }
}

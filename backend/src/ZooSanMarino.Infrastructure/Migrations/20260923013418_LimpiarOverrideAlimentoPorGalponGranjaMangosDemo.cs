using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only, sin cambio de modelo). La granja MANGOS (farms.id = 111,
    /// empresa Demo, company_id = 4) tenía <c>maneja_alimento_por_galpon = TRUE</c> a nivel de
    /// GRANJA — un override que pisa el default de la empresa (patrón <c>farm ?? company</c>,
    /// <see cref="ZooSanMarino.Application.Calculos.AlimentoNivelResolver.ManejaPorGalpon"/>).
    /// <para>
    /// La empresa Demo tiene el flag apagado (comportamiento por GRANJA, igual que San Marino real,
    /// que es lo que Demo debe simular) — confirmado en la pantalla "Editar Empresa" (paso 2,
    /// "Manejar el alimento a nivel galpón" sin marcar). El override quedó puesto solo en MANGOS,
    /// probablemente de una prueba anterior de la función de Ecuador/Panamá (las únicas empresas que
    /// hoy manejan alimento por galpón). Mientras esa granja lo tenga en TRUE, cualquier carga de
    /// alimento sobre ella exige Núcleo y Galpón aunque el resto de la empresa no lo haga — el error
    /// real que salió al cargar el ejemplo de SeguimientoLevante (22-sep-2026): "Para ítem tipo
    /// alimento debe indicar Núcleo y Galpón" (<c>InventarioGestionService.Ingreso.cs</c>).
    /// </para>
    /// <para>
    /// No es un bug de la función de resolución del flag — el override por granja es un
    /// comportamiento DELIBERADO y documentado (permite que una granja puntual se aparte del
    /// default de su empresa). El problema es puramente de DATO: esta granja de práctica quedó con
    /// un override que ya no representa lo que tiene que simular. El fix es volver el override a
    /// <c>NULL</c> ("hereda de la empresa"), no tocar el flag de la empresa (que ya está correcto).
    /// </para>
    /// <para>
    /// <b>Idempotente</b> (el WHERE solo agarra la fila si sigue en TRUE) y <b>sin reversa</b>: volver
    /// a poner TRUE recrearía el defecto que motiva esta migración.
    /// </para>
    /// </summary>
    public partial class LimpiarOverrideAlimentoPorGalponGranjaMangosDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE public.farms
   SET maneja_alimento_por_galpon = NULL,
       updated_at = now()
 WHERE id = 111
   AND company_id = 4
   AND maneja_alimento_por_galpon IS TRUE;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa a propósito: volver a poner el override en TRUE recrearía el defecto
            // (la granja volvería a exigir Núcleo/Galpón contra el default correcto de la empresa).
        }
    }
}

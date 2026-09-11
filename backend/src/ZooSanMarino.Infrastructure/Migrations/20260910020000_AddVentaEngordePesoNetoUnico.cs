using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVentaEngordePesoNetoUnico : Migration
    {
        // Flag de empresa `venta_engorde_peso_neto_unico`: la empresa recibe de planta UNA sola cifra
        // de kilos por despacho —el peso NETO del pollo— en vez de las dos pesadas de báscula. Con el
        // flag encendido el formulario de venta de engorde OCULTA «Peso tara», rotula el campo
        // restante como los kilos del despacho y manda la tara en 0, así que el neto es lo digitado.
        //
        // Por qué existe. Pedir un campo que nunca se llena fue la causa del incidente del
        // 9-sep-2026: el operario de Panamá repetía el mismo número en bruto y en tara ⇒ neto 0 ⇒
        // 1.124.026 kg que no llegaban al seguimiento diario, al informe semanal ni a la liquidación
        // (ver la migración FixVentaEngordeNetoCeroBrutoIgualTara). El gate que ahora rechaza el
        // neto 0 evita el dato malo, pero no evita la fricción: quien solo tiene una cifra no
        // debería ver dos casillas.
        //
        // Por qué NO se reusó `venta_engorde_peso_diferido`. Son dos hechos distintos: CUÁNDO llega
        // el peso (al día siguiente) y CUÁNTAS cifras trae (una). Panamá tiene los dos, pero una
        // empresa podría tener báscula diferida con bruto y tara reales, y reusar el flag le
        // escondería un campo que sí usa. La regla del repo es una columna por comportamiento,
        // nombrada por el comportamiento y no por el tenant.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    ADD COLUMN IF NOT EXISTS venta_engorde_peso_neto_unico boolean NOT NULL DEFAULT false;");

            // Se enciende para ItalcolPanama, la empresa que motivó el flag. `IS DISTINCT FROM` para
            // no ensuciar la fila si ya estaba en true (idempotente), y match tolerante al espacio
            // porque el nombre se digita a mano en la pantalla de Empresas.
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET venta_engorde_peso_neto_unico = true
 WHERE lower(replace(name, ' ', '')) = 'italcolpanama'
   AND venta_engorde_peso_neto_unico IS DISTINCT FROM true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    DROP COLUMN IF EXISTS venta_engorde_peso_neto_unico;");
        }
    }
}

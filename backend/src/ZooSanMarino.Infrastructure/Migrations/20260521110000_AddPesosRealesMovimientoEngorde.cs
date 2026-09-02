using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega <c>movimiento_pollo_engorde.peso_bruto_real</c> y <c>peso_tara_real</c> (peso
    /// individual prorrateado por aves del lote).
    /// </summary>
    /// <remarks>
    /// <b>Por qué el <c>Up</c> pasó a ser SQL idempotente (2-sep-2026).</b> Esta migración nació
    /// <b>sin su <c>.Designer.cs</c></b>, o sea sin el atributo <c>[Migration]</c>:
    /// <c>MigrationsAssembly</c> descubre migraciones filtrando por ese atributo, así que para EF
    /// esta clase no existía —no salía en <c>migrations list</c> ni se aplicaba en ningún deploy—.
    /// Las columnas se aplicaron a mano con
    /// <c>backend/sql/apply_pesos_reales_movimiento_engorde.sql</c>, que además insertaba el id en
    /// <c>__EFMigrationsHistory</c>.
    ///
    /// Al escribirle el Designer que le faltaba, EF pasa a verla. En cualquier base donde ese id
    /// <b>no</b> esté registrado la va a ejecutar, y las columnas ya existen: los <c>AddColumn</c>
    /// originales —que EF escribe <b>sin</b> <c>IF NOT EXISTS</c>— habrían fallado y dejado el
    /// contenedor en crash-loop al arrancar. Con <c>ADD COLUMN IF NOT EXISTS</c> el peor caso es un
    /// no-op que solo registra el id.
    ///
    /// <b>Ojo con el tipo, que NO es el mismo en todos lados.</b> El modelo las declara
    /// <c>double?</c> ⇒ <c>double precision</c>, y eso es lo que crea esta migración. El script
    /// manual que se usó en su momento las creó <c>numeric(12,3)</c>, así que en las bases que
    /// pasaron por él —la local, medido— el peso <b>se redondea a 3 decimales</b> y en una base
    /// creada desde migraciones no. Queda anotado, no "arreglado" acá: alinear el tipo del peso en
    /// cualquiera de los dos sentidos es un cambio de comportamiento sobre datos de báscula y se
    /// decide aparte. El código manda ⇒ el tipo del modelo (<c>double precision</c>) es el que esta
    /// migración conserva.
    ///
    /// <b>El <c>Down</c> no se puede correr, y tampoco se podía antes.</b> Medido: el trigger
    /// <c>trg_movimiento_pollo_engorde_lote_hist</c> —uno de los que llenan
    /// <c>lote_registro_historico_unificado</c>— depende de <c>peso_tara_real</c>, así que el
    /// <c>DROP</c> falla con <i>«other objects depend on it»</i>, igual que el <c>DropColumn</c>
    /// original. No se le pone <c>CASCADE</c>: eso se llevaría puesto el trigger del histórico.
    /// </remarks>
    public partial class AddPesosRealesMovimientoEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.movimiento_pollo_engorde
    ADD COLUMN IF NOT EXISTS peso_bruto_real double precision;

ALTER TABLE public.movimiento_pollo_engorde
    ADD COLUMN IF NOT EXISTS peso_tara_real double precision;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.movimiento_pollo_engorde DROP COLUMN IF EXISTS peso_bruto_real;
ALTER TABLE public.movimiento_pollo_engorde DROP COLUMN IF EXISTS peso_tara_real;
");
        }
    }
}

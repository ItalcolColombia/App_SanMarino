using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega <c>lote_ave_engorde.fecha_alistamiento</c>.
    /// </summary>
    /// <remarks>
    /// <b>Por qué el <c>Up</c> pasó a ser SQL idempotente (2-sep-2026).</b> Esta migración nació
    /// <b>sin su <c>.Designer.cs</c></b>, o sea sin el atributo <c>[Migration]</c>:
    /// <c>MigrationsAssembly</c> descubre migraciones filtrando por ese atributo, así que para EF
    /// esta clase no existía —no salía en <c>migrations list</c> ni se aplicaba en ningún deploy—.
    /// La columna se aplicó a mano con <c>backend/sql/apply_fecha_alistamiento_lote_engorde.sql</c>,
    /// que además insertaba el id en <c>__EFMigrationsHistory</c>.
    ///
    /// Al escribirle el Designer que le faltaba, EF pasa a verla. En cualquier base donde ese id
    /// <b>no</b> esté registrado la va a ejecutar, y la columna ya existe: el <c>AddColumn</c>
    /// original —que EF escribe <b>sin</b> <c>IF NOT EXISTS</c>— habría fallado y dejado el
    /// contenedor en crash-loop al arrancar. Con <c>ADD COLUMN IF NOT EXISTS</c> el peor caso es un
    /// no-op que solo registra el id.
    ///
    /// El tipo se conserva tal cual estaba (<c>date</c>), que es lo que hay en la base. El modelo la
    /// declara <c>DateTime?</c> sin fijar tipo ⇒ <c>timestamp with time zone</c>: la divergencia es
    /// previa, funciona (Postgres castea al asignar) y no se toca acá.
    ///
    /// <b>El <c>Down</c> no se puede correr, y tampoco se podía antes.</b> Medido: la vista
    /// <c>vw_liquidacion_ecuador_pollo_engorde</c> depende de la columna, así que el <c>DROP</c>
    /// falla con <i>«other objects depend on it»</i> — igual que el <c>DropColumn</c> original. No se
    /// le pone <c>CASCADE</c>: eso borraría la vista en silencio.
    /// </remarks>
    public partial class AddFechaAlistamientoLoteEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.lote_ave_engorde
    ADD COLUMN IF NOT EXISTS fecha_alistamiento date;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.lote_ave_engorde DROP COLUMN IF EXISTS fecha_alistamiento;
");
        }
    }
}

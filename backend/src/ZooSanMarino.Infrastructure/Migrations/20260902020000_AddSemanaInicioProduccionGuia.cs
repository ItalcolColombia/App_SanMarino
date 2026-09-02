using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega <c>companies.semana_inicio_produccion_guia</c>: la primera semana de VIDA que la guía
    /// genética de cada empresa considera PRODUCCIÓN.
    /// </summary>
    /// <remarks>
    /// <b>Qué problema resuelve.</b> Los reportes decidían qué filas de la guía eran de postura con
    /// un <c>26</c> escrito a mano, que es el corte de la guía de <b>esquema completo</b> —la que
    /// cubre levante + postura y cuya primera edad con <c>prod_porcentaje</c> es la 25/26—. Pero hay
    /// empresas cuya guía <b>arranca directamente en producción</b>: medido en
    /// <c>guia_genetica_santa_reyes</c>, la primera edad es la <b>18</b> y ya trae producción ahí
    /// (7,70 % en Hy Line Brown, subiendo a 96,60 % en la semana 25). Con el corte fijo en 26 se
    /// perdían las semanas 18-25, que son justo la curva de arranque de la postura.
    ///
    /// <b>Por qué una columna y no un <c>if</c> por empresa.</b> Regla §🏢 de <c>CLAUDE.md</c>: la
    /// señal vive en <c>companies</c> como columna tipada nombrada por el <b>comportamiento</b>
    /// («desde qué semana la guía es de producción»), nunca por el tenant. Otra empresa que mañana
    /// cargue una guía que arranque en otra semana se resuelve con un <c>UPDATE</c>, sin tocar código.
    ///
    /// <b>Default neutro.</b> <c>26</c> es exactamente el número que estaba escrito en el código, así
    /// que toda empresa que no se toque explícitamente se comporta igual que antes, por construcción.
    ///
    /// <b>Idempotente.</b> <c>ADD COLUMN IF NOT EXISTS</c> y el <c>UPDATE</c> con
    /// <c>IS DISTINCT FROM</c>, que no ensucia <c>updated_at</c> en una segunda corrida ni pisa un
    /// valor que alguien haya ajustado a mano.
    ///
    /// <b>Orden.</b> El <c>UPDATE</c> localiza la empresa por <b>su guía</b>, no por su nombre: se
    /// aplica a quien tenga filas en la tabla de esquema simple. Así no depende de que el seed que
    /// crea esa empresa haya corrido antes (§🏢.7), y si la empresa todavía no existe simplemente no
    /// afecta ninguna fila.
    /// </remarks>
    public partial class AddSemanaInicioProduccionGuia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                  ADD COLUMN IF NOT EXISTS semana_inicio_produccion_guia INTEGER NOT NULL DEFAULT 26;
            ");

            // Quien tiene su guía en la tabla de esquema simple arranca la producción en la 18: esa
            // tabla no tiene filas de levante, su primera edad ya es de postura.
            migrationBuilder.Sql(@"
                UPDATE companies c
                   SET semana_inicio_produccion_guia = 18
                 WHERE c.semana_inicio_produccion_guia IS DISTINCT FROM 18
                   AND EXISTS (
                        SELECT 1
                          FROM guia_genetica_santa_reyes g
                         WHERE g.company_id = c.id
                           AND g.deleted_at IS NULL
                   );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies DROP COLUMN IF EXISTS semana_inicio_produccion_guia;
            ");
        }
    }
}

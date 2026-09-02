using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Elimina del modelo la entidad fantasma <c>SeguimientoDiarioAvesEngordeEcuador</c> y, si en
    /// algún entorno llegó a crearse su tabla <b>vacía</b>, la borra.
    /// </summary>
    /// <remarks>
    /// <b>Qué era.</b> <c>20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry</c> partió el
    /// seguimiento diario de engorde por país. El split <b>se abandonó</b> y nadie limpió el rastro:
    /// quedaron la entidad, su <c>Configuration</c> y el <c>DbSet</c> apuntando a
    /// <c>seguimiento_diario_aves_engorde_ecuador</c>, una tabla que <b>no existe</b>
    /// (<c>to_regclass</c> → NULL, verificado el 2026-09-02) aunque la migración figure aplicada en
    /// <c>__EFMigrationsHistory</c>. La <c>_panama</c> sí existe y <b>no se toca</b>.
    ///
    /// <b>Por qué importa borrarlo y no sólo ignorarlo.</b> El nombre hacía creer que había un
    /// camino de datos por país. No lo hay: los dos services escriben la <b>misma</b> tabla
    /// <c>seguimiento_diario_aves_engorde</c>, y el service «Ecuador» —que es el vivo para Ecuador,
    /// Panamá y Colombia— resuelve todo contra <c>_ctx.SeguimientoDiarioAvesEngorde</c>. Creer lo
    /// contrario ya costó el bug de la doble validación: las ramas <c>ENGORDE_EC</c> leían la tabla
    /// fantasma y guardar reventaba con 42P01 mientras validar marcaba <c>validado=true</c> sin
    /// descontar nada.
    ///
    /// <b>Por qué la migración existe si la tabla no existe.</b> Al sacar la entidad del modelo hay
    /// que sacarla también del <c>ModelSnapshot</c>, o la próxima migración que alguien genere
    /// traería un <c>DropTable</c> automático de una tabla inexistente y la app moriría al arrancar
    /// —el patrón de crash que ya se pagó una vez—. Esta migración es el vehículo de ese cambio de
    /// modelo; el <c>DROP</c> es la parte defensiva, para el entorno donde la tabla sí se haya creado.
    ///
    /// <b>No borra datos, por diseño.</b> El <c>DO</c> sólo dropea si la tabla existe <b>y está
    /// vacía</b>. Si tuviera filas, las deja donde están y sigue de largo: una tabla huérfana que
    /// nadie mapea es inofensiva, perder filas no. Tampoco aborta el arranque —un <c>RAISE</c> acá
    /// dejaría el deploy en un crash-loop por algo que no es urgente—.
    /// </remarks>
    public partial class EliminaSeguimientoEngordeEcuadorFantasma : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE v_filas bigint;
                BEGIN
                    IF to_regclass('public.seguimiento_diario_aves_engorde_ecuador') IS NULL THEN
                        RAISE NOTICE 'seguimiento_diario_aves_engorde_ecuador no existe: nada que hacer.';
                        RETURN;
                    END IF;

                    EXECUTE 'SELECT count(*) FROM public.seguimiento_diario_aves_engorde_ecuador'
                       INTO v_filas;

                    IF v_filas = 0 THEN
                        DROP TABLE public.seguimiento_diario_aves_engorde_ecuador;
                        RAISE NOTICE 'seguimiento_diario_aves_engorde_ecuador estaba vacia: eliminada.';
                    ELSE
                        RAISE NOTICE 'seguimiento_diario_aves_engorde_ecuador tiene % filas: NO se elimina, revisar a mano.', v_filas;
                    END IF;
                END $$;
            ");
        }

        /// <summary>
        /// No recrea la tabla: nunca tuvo datos ni lectores, y recrearla devolvería justamente la
        /// ambigüedad que esta migración elimina. El <c>Down</c> del modelo lo da el propio EF al
        /// volver el código a la revisión anterior.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

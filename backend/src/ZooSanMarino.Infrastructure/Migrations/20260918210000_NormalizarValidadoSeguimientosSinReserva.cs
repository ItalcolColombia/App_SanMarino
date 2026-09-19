using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only, sin cambio de modelo) de <c>validado</c> en los seguimientos
    /// diarios de levante, producción y engorde.
    ///
    /// <para>
    /// <c>validado</c> significa «su efecto ya se aplicó». Con la doble validación APAGADA el registro
    /// descuenta al guardar, así que tiene que estar validado; la rama «Colombia modelo B con consumo
    /// por ítems» de <c>ProduccionService.CrearSeguimientoAsync</c> hacía <c>return</c> antes de
    /// <c>entity.Validado = !separa</c> y los registros de Santa Reyes nacían en el default de la
    /// columna (<c>false</c>). Hoy nada los lee; el día que la empresa encienda el flag aparecerían
    /// pendientes, pasarían a EN RETRASO a las 24 h y bloquearían el alta de días nuevos del lote sin
    /// tener nada que validar. El servicio y las entidades ya nacen bien desde este mismo commit.
    /// </para>
    ///
    /// <para>
    /// <b>Criterio (las TRES condiciones a la vez):</b> (1) la empresa dueña del registro tiene
    /// <c>requiere_validacion_seguimiento_diario = false</c>; (2) el registro no tiene NINGUNA reserva,
    /// de alimento ni de aves, en ningún estado —una reserva prueba que se creó con el flag encendido,
    /// o sea que es un pendiente legítimo cuyo efecto NO se aplicó y marcarlo validado perdería ese
    /// consumo y esas bajas—; (3) no está borrado (producción). En la copia de producción del 18-sep
    /// afecta exactamente #687, #692, #696 y #697 (Santa Reyes), los cuatro con su <c>Consumo</c> ya
    /// aplicado en <c>inventario_gestion_movimiento</c>. Los pendientes con reserva (#676–#681) y todo
    /// lo de las empresas con el flag encendido (Panamá) quedan intactos.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente:</b> <c>validado = false</c> deja de cumplirse tras la primera pasada ⇒ re-ejecutarla
    /// da <c>UPDATE 0</c>. No toca <c>updated_at</c> ni <c>validado_at</c>/<c>validado_por</c> (nadie lo
    /// validó: nació aplicado). Medición y simulación con ROLLBACK:
    /// <c>backend/sql/verificar_validado_sin_reserva.sql</c>. Módulos de las reservas según
    /// <c>ModuloSeguimiento</c>: LEVANTE, PRODUCCION y ENGORDE (Ecuador colapsa a ENGORDE).
    /// </para>
    /// </summary>
    public partial class NormalizarValidadoSeguimientosSinReserva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.seguimiento_diario_produccion s
                   SET validado = true
                  FROM public.companies c
                 WHERE c.id = s.company_id
                   AND c.requiere_validacion_seguimiento_diario = false
                   AND s.validado = false
                   AND s.deleted_at IS NULL
                   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                                    WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id)
                   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                                    WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id);
            ");

            migrationBuilder.Sql(@"
                UPDATE public.seguimiento_diario_levante s
                   SET validado = true
                  FROM public.companies c
                 WHERE c.id = s.company_id
                   AND c.requiere_validacion_seguimiento_diario = false
                   AND s.validado = false
                   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                                    WHERE r.origen_modulo = 'LEVANTE' AND r.origen_seguimiento_id = s.id)
                   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                                    WHERE r.origen_modulo = 'LEVANTE' AND r.origen_seguimiento_id = s.id);
            ");

            // Engorde no lleva empresa en la fila: cuelga del lote.
            migrationBuilder.Sql(@"
                UPDATE public.seguimiento_diario_aves_engorde s
                   SET validado = true
                  FROM public.lote_ave_engorde l
                  JOIN public.companies c ON c.id = l.company_id
                 WHERE l.lote_ave_engorde_id = s.lote_ave_engorde_id
                   AND c.requiere_validacion_seguimiento_diario = false
                   AND s.validado = false
                   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                                    WHERE r.origen_modulo = 'ENGORDE' AND r.origen_seguimiento_id = s.id)
                   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                                    WHERE r.origen_modulo = 'ENGORDE' AND r.origen_seguimiento_id = s.id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa a propósito: volver a `false` esos registros recrearía el defecto (pendientes
            // sin nada que validar) y no hay forma de distinguirlos de los que ya nacen bien.
        }
    }
}

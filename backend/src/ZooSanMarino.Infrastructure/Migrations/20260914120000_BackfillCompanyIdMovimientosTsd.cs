using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only, sin cambio de modelo) de <c>movimiento_aves.company_id</c> en
    /// la auditoría de los traslados hechos desde Seguimiento Diario (números <c>TSD-*</c>).
    ///
    /// <para>
    /// <c>TrasladoAvesDesdeSegService</c> creaba esa fila sin <c>CompanyId</c> y <c>SetAuditFields</c>
    /// no lo completa, así que quedaba en <c>0</c>. Con eso el movimiento era invisible para todo
    /// <c>MovimientoAvesService</c> (filtra por la empresa del usuario) y quedaba fuera del saldo de
    /// <c>fn_seguimiento_diario_produccion</c>, que suma <c>movimiento_aves</c> con
    /// <c>company_id = lpp.company_id</c>. El servicio ya estampa la empresa desde el mismo commit.
    /// </para>
    ///
    /// <para>
    /// <b>Criterio:</b> empresa dueña de la granja del lote ORIGEN (<c>farms.company_id</c> por
    /// <c>granja_origen_id</c>), el mismo que usa el servicio. Filas sin granja origen o cuya granja no
    /// tenga empresa válida quedan como están (no se inventa una empresa).
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente:</b> <c>company_id = 0</c> deja de cumplirse tras la primera pasada ⇒ re-ejecutarla
    /// da <c>UPDATE 0</c>. No toca <c>updated_at</c> ni otras columnas. Medición y simulación con
    /// ROLLBACK: <c>backend/sql/verificar_backfill_company_id_tsd.sql</c>.
    /// </para>
    /// </summary>
    public partial class BackfillCompanyIdMovimientosTsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE movimiento_aves ma
                   SET company_id = f.company_id
                  FROM farms f
                 WHERE ma.numero_movimiento LIKE 'TSD-%'
                   AND ma.company_id = 0
                   AND ma.granja_origen_id = f.id
                   AND f.company_id > 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa a propósito: volver company_id a 0 alcanzaría también a los TSD nuevos, que
            // ya nacen con la empresa estampada por TrasladoAvesDesdeSegService, y el valor 0 era el bug.
        }
    }
}

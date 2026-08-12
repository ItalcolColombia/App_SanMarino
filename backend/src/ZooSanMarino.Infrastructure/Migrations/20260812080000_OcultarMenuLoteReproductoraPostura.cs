using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// El menú «Lote Reproductora» (id 9, grupo Seguimiento Diario) queda **definido pero sin
    /// asignar**, y con la etiqueta corregida.
    ///
    /// ## Los dos problemas que arregla
    ///
    /// 1. **Estaba visible sin que nadie lo habilitara.** No figura en `company_menus` para ninguna
    ///    empresa, pero sí en `role_menus` de 3 roles (Auxiliar de Granja, Líder técnico, Director
    ///    técnico). El sidebar se arma con `role_menus`, así que `company_menus` no lo ocultaba.
    /// 2. **No abría lo que su nombre decía.** Su ruta carga `SeguimientoLoteLevanteModule`, o sea
    ///    la pantalla de **Levante**: un técnico entraba por «Lote Reproductora» y cargaba un
    ///    seguimiento de levante sin darse cuenta.
    ///
    /// El módulo de reproductora de **postura** todavía no existe como pantalla de captura (el único
    /// `SeguimientoDiarioLoteReproductora` que hay es el de **pollo engorde**, menú 43, exclusivo de
    /// ItalcolPanama).
    ///
    /// ## Por qué se DESASIGNA y no se borra
    ///
    /// La fila del menú se conserva para cuando el módulo exista: borrarla obligaría a recrearla con
    /// otro id, y los ids de menú difieren entre local y prod, así que cualquier script que la
    /// localice por id se rompería. Lo que se quita es el **acceso** (`role_menus`), que es lo único
    /// que la hace visible.
    ///
    /// Idempotente: correrla dos veces deja el mismo estado.
    /// </summary>
    public partial class OcultarMenuLoteReproductoraPostura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La etiqueta pasa a nombrar la etapa, en paralelo con «Seguimiento Reproductora Pollo
            // Engorde» (menú 43). Se localiza por RUTA, nunca por id: los ids difieren local↔prod.
            migrationBuilder.Sql(@"
                UPDATE menus
                   SET label = 'Seguimiento Reproductora Postura'
                 WHERE route = '/daily-log/seguimiento-diario-lote-reproductora'
                   AND label IS DISTINCT FROM 'Seguimiento Reproductora Postura';
            ");

            // Se quita el acceso a TODOS los roles, no solo a los tres de la BD local: en prod puede
            // haber otros. La fila del menú se conserva.
            migrationBuilder.Sql(@"
                DELETE FROM role_menus
                 WHERE menu_id IN (
                    SELECT id FROM menus
                     WHERE route = '/daily-log/seguimiento-diario-lote-reproductora'
                 );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se revierte la etiqueta. El acceso NO se restaura: volver a asignar el menú a
            // roles que ya no lo tenían reintroduciría el defecto que esta migración arregla, y no
            // hay forma de saber cuáles lo tenían de verdad antes.
            migrationBuilder.Sql(@"
                UPDATE menus
                   SET label = 'Lote Reproductora'
                 WHERE route = '/daily-log/seguimiento-diario-lote-reproductora'
                   AND label IS DISTINCT FROM 'Lote Reproductora';
            ");
        }
    }
}

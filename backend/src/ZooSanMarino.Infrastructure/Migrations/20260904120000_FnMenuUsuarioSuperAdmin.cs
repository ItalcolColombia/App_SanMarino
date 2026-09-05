using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>fn_menu_usuario</c>: el gate por empresa (<c>company_menus</c>) deja de aplicarse al
    /// <b>super admin</b> (<c>users.is_super_admin</c>). Regla <b>D5</b>.
    ///
    /// <para>
    /// <b>El defecto.</b> Desde el 26-ago-2026 el menú respeta <c>company_menus</c> — que era el
    /// arreglo correcto— pero lo hace para todos por igual, incluido el único usuario que puede
    /// pararse en cualquier empresa. Medido sobre la copia de producción (4-sep-2026), los dos ítems
    /// que administran el sistema entero están habilitados en <b>una sola empresa</b>:
    /// <c>/config/companies</c> y <c>/config/db-studio</c> solo en Agroavicola Sanmarino. Quitarle
    /// esos menús a esa empresa —justamente lo que hay que hacer para que Sanmarino quede acotada
    /// como las demás— deja al super admin <b>sin el módulo Empresas en toda la aplicación</b>, y sin
    /// ruta de vuelta desde la UI: para volver a habilitarlo hay que entrar a
    /// Configuración → Empresas → Menús, que es precisamente el menú que desapareció. Salida: SQL a
    /// mano contra la base.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué D2 no lo cubría.</b> El fail-open existente aplica a la empresa que <b>no tiene
    /// ninguna fila</b> en <c>company_menus</c>; las cinco empresas reales tienen (49/27/25/23/25).
    /// </para>
    ///
    /// <para>
    /// <b>Alcance del bypass: el gate de EMPRESA y nada más.</b> Al super admin le siguen aplicando
    /// <c>role_menus</c>, <c>menus.is_active</c> y <c>menu_permissions</c> igual que a cualquiera —no
    /// ve el catálogo entero, ve lo que sus roles le dan. Para todo usuario sin la marca el resultado
    /// es <b>idéntico</b> al previo, que es lo que verifica
    /// <c>backend/sql/verificar_menu_usuario_paridad.sql</c>.
    /// </para>
    ///
    /// <para>
    /// La marca sale del <b>dato</b> (<c>users.is_super_admin</c>), nunca de un correo ni de un
    /// nombre de rol — ver <c>SuperAdminCalculos</c>. Usuario inexistente ⇒ <c>false</c>
    /// (fail-closed).
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/soporte_sanmarino_y_admin_global_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_menu_usuario.sql</c> — esta migración es el <b>vehículo</b>: nada de
    /// <c>backend/sql/</c> llega a producción por sí solo.
    /// Idempotente: <c>CREATE OR REPLACE</c>. Sin cambios de modelo (ModelSnapshot intacto).
    /// </summary>
    public partial class FnMenuUsuarioSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnMenuUsuarioSuperAdminSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir = volver a aplicarle el gate de empresa al super admin, o sea la versión que
            // dejó la migración 20260826120000_FnMenuUsuario. No se dropea la función: dejarla caída
            // rompería el sidebar de todos, no solo el del super admin.
            migrationBuilder.Sql(FnMenuUsuarioSinBypassSql);
        }
    }
}

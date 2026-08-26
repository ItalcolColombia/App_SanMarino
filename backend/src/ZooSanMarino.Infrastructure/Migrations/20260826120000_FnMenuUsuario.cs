using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// El menú del usuario pasa a respetar lo que la empresa tiene habilitado
    /// (<c>company_menus</c>), y se arma de una sola llamada a la BD.
    ///
    /// <para>
    /// <b>El defecto.</b> <c>RoleCompositeService.Menus_GetForUserAsync</c> —lo que alimenta
    /// <c>GET /api/Roles/menus/me</c>, <c>GET /api/Auth/menu</c> y el bootstrap del login, o sea
    /// TODO el sidebar— arma el menú desde <c>role_menus</c> ∩ <c>menus.is_active</c> ∩
    /// <c>menu_permissions</c>. <b><c>company_menus</c> no aparece en ninguna parte:</b> esa tabla la
    /// lee un solo servicio, el que alimenta la pantalla de administración «Menús por empresa». El
    /// switch existe, la UI existe, y al runtime no le llega. Quitarle un módulo a una empresa no
    /// cambiaba nada.
    /// </para>
    ///
    /// <para>
    /// <b>Medido en la copia de producción (26-ago-2026):</b> 51 pares (usuario, empresa, menú) se
    /// colaban. A ItalcolPanamá —la empresa que lo reportó— le entraban 7 menús que no tiene
    /// asignados: ItalJira entero (Backlog, Tablero, Roadmap, Panel de control), Guía Genética y
    /// Bandeja de gestión. También Ecuador (4), Sanmarino (2), Demo (1) y Santa Reyes (1).
    /// </para>
    ///
    /// <para>
    /// <b>Por qué en la BD y no en el backend.</b> El método hacía cuatro viajes a Postgres (roles →
    /// keys de permiso → catálogo con su subquery de permisos → menús asignados) y armaba el árbol en
    /// memoria; con el gate por empresa serían cinco. <c>fn_menu_usuario</c> resuelve la relación
    /// entre las siete tablas donde viven los índices y devuelve el árbol <b>ya construido</b> como
    /// <c>jsonb</c>: una sola llamada.
    /// </para>
    ///
    /// <para>
    /// <b>El gate es fail-open sobre la empresa sin configurar</b>, a propósito:
    /// <c>CompanyService.CreateAsync</c> siembra <c>company_permissions</c> pero NO
    /// <c>company_menus</c>, así que fail-closed dejaría a toda empresa nueva con el menú vacío y sin
    /// forma de arreglarlo desde la app —para asignar menús hay que entrar a Configuración, que es un
    /// ítem del menú que no se vería—. Las cinco empresas reales sí tienen filas (46/24/23/25/34), así
    /// que el gate queda activo donde importa. Ver <c>MenuVisibilidadCalculos</c> (D2).
    /// </para>
    ///
    /// <para>
    /// <b>Verificado por paridad fila a fila</b> (<c>backend/sql/verificar_menu_usuario_paridad.sql</c>,
    /// 56 pares usuario-empresa): <b>0 regresiones</b> (la función no hace aparecer ningún menú que la
    /// regla anterior no mostrara) y <b>0 colaterales</b> (los 51 que dejan de verse son exactamente
    /// los que su empresa no habilita).
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/menu_efectivo_por_empresa_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_menu_usuario.sql</c> — esta migración es el <b>vehículo</b>: nada de
    /// <c>backend/sql/</c> llega a producción por sí solo.
    /// Idempotente: <c>CREATE OR REPLACE</c>. Sin cambios de modelo (ModelSnapshot intacto).
    /// </summary>
    public partial class FnMenuUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnMenuUsuarioSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El backend vuelve a resolver el menú en C# si se revierte el código, así que dropear la
            // función es seguro: nadie más la llama.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_menu_usuario(uuid, integer);");
        }
    }
}

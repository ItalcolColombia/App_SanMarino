using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Lote base engorde, fase 2: campos Código ERP y Línea genética + seed de los 4 permisos
    /// del módulo (convención "modulo.accion", igual que movimientos_pollo_engorde.*):
    ///   lote_base_pollo_engorde.ver      → botón "Lotes base" + campo en el form del lote
    ///   lote_base_pollo_engorde.crear    → crear (modal de gestión y crear rápido inline)
    ///   lote_base_pollo_engorde.editar   → editar en el modal de gestión
    ///   lote_base_pollo_engorde.eliminar → eliminar en el modal de gestión
    /// El gate es 100% frontend (*appHasPermission); quedan disponibles en Roles y Permisos.
    /// La asignación a roles NO se hace aquí. Todo idempotente (IF NOT EXISTS / NOT EXISTS).
    /// </summary>
    public partial class AddLoteBaseEngordeCamposYPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) Campos nuevos del catálogo (idempotente)
ALTER TABLE public.lote_base_engorde
    ADD COLUMN IF NOT EXISTS codigo_erp     character varying(80)  NULL;
ALTER TABLE public.lote_base_engorde
    ADD COLUMN IF NOT EXISTS linea_genetica character varying(120) NULL;

-- 2) Permisos del módulo Lote Base Pollo Engorde
INSERT INTO public.permissions (key, description)
SELECT 'lote_base_pollo_engorde.ver',
       'Lote Base Pollo Engorde: ver el botón/modal de lotes base y el campo Lote base en el form de lote engorde'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'lote_base_pollo_engorde.ver');

INSERT INTO public.permissions (key, description)
SELECT 'lote_base_pollo_engorde.crear',
       'Lote Base Pollo Engorde: crear lotes base (modal de gestión y crear rápido en el form de lote)'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'lote_base_pollo_engorde.crear');

INSERT INTO public.permissions (key, description)
SELECT 'lote_base_pollo_engorde.editar',
       'Lote Base Pollo Engorde: editar lotes base existentes (nombre, código ERP, línea genética, descripción)'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'lote_base_pollo_engorde.editar');

INSERT INTO public.permissions (key, description)
SELECT 'lote_base_pollo_engorde.eliminar',
       'Lote Base Pollo Engorde: eliminar lotes base (bloqueado si tienen lotes de engorde amarrados)'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'lote_base_pollo_engorde.eliminar');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'lote_base_pollo_engorde.%');
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'lote_base_pollo_engorde.%');
DELETE FROM public.permissions WHERE key LIKE 'lote_base_pollo_engorde.%';

ALTER TABLE public.lote_base_engorde DROP COLUMN IF EXISTS codigo_erp;
ALTER TABLE public.lote_base_engorde DROP COLUMN IF EXISTS linea_genetica;
");
        }
    }
}

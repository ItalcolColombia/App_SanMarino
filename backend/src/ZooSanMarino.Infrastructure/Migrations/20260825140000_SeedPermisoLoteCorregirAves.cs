using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Permiso <c>lote.corregir_aves</c>: corregir el ENCASETAMIENTO de un lote que ya tiene
    /// seguimiento cargado (engorde y postura).
    ///
    /// <para>
    /// <b>Qué habilita.</b> El ajuste de encasetamiento del commit <c>a9fd721</c>: el inicial se
    /// reemplaza y el saldo vivo se corre por el delta, propagando a <c>aves_encasetadas</c>, al
    /// registro <c>Inicio</c> del historial y al maestro en engorde, y a <c>lote_etapa_levante</c> +
    /// <c>lote_postura_produccion</c> en postura. No es un botón cosmético: <b>reescribe toda la
    /// serie diaria</b> del lote —saldo, % de mortalidad, conversión, ave-día—, los reportes y la
    /// liquidación, porque <c>fn_seguimiento_diario_engorde</c> lee <c>aves_encasetadas</c> en vivo.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué hace falta una key nueva.</b> Hasta hoy el único gate era <c>editar_registro</c>,
    /// que es TRANSVERSAL: darlo para que alguien corrija las aves de un lote le habilita al mismo
    /// tiempo editar filas del seguimiento diario, movimientos y ventas de pollo engorde y
    /// movimientos de inventario. No había forma de dar solo lo del lote. Y en POSTURA no había
    /// ningún gate: alcanzaba con ver el módulo en el menú.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Se hereda de <c>editar_registro</c> — nadie gana ni pierde el día del deploy.</b> Es el
    /// patrón de <c>20260817200000_AddPermisosYMenuVacunacionPlantillas</c>. Importa especialmente
    /// por postura, que hoy está abierto: sin la herencia, agregarle el gate le sacaría de golpe a
    /// alguien algo que hoy hace todos los días. Como el rol «Ecuador Administrador» ya tiene
    /// <c>editar_registro</c>, la herencia también resuelve el pedido que originó esta migración.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>El permiso solo, sin el gate del backend, no cierra nada.</b> Hasta el 25-ago-2026
    /// <c>LoteAveEngordeController</c> y <c>LoteController</c> no validaban ningún permiso en el
    /// <c>PUT</c>: el <c>*appHasPermission</c> del front solo escondía el botón y cualquiera con
    /// acceso a la granja podía aplicar el ajuste con curl. El enforcement va en el service, y solo
    /// cuando el delta de aves es distinto de cero — editar el técnico o la regional no es corregir
    /// aves y no debe pedir este permiso.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md</c> §2.
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot vigente, ModelSnapshot intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c>), localizando por <c>permissions.key</c>.
    /// </summary>
    public partial class SeedPermisoLoteCorregirAves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string UP_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) La key. Nombrada por el COMPORTAMIENTO (corregir las aves de un lote), no por el modulo ni
--    por el tenant que la pidio.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT 'lote.corregir_aves',
       'Gestion de Lotes: corregir el encasetamiento (hembras/machos/mixtas) de un lote que ya tiene seguimiento cargado. La correccion baja en cascada a la serie diaria, los reportes y la liquidacion.'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'lote.corregir_aves');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Asignable en TODAS las empresas: los lotes son un modulo base que toda empresa ya tiene.
--    company_permissions es fail-closed: sin esta fila el permiso no viaja en el JWT.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
FROM public.companies c
CROSS JOIN public.permissions p
WHERE p.key = 'lote.corregir_aves'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_permissions x
        WHERE x.company_id = c.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) HERENCIA desde editar_registro: quien hoy puede editar un lote lo sigue pudiendo.
--    Sin esto, el gate nuevo de POSTURA (que hoy no tiene ninguno) le quitaria la funcion a los
--    roles que la usan a diario.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, nuevo.id
FROM public.role_permissions rp
JOIN public.permissions origen ON origen.id = rp.permission_id AND origen.key = 'editar_registro'
CROSS JOIN LATERAL (SELECT id FROM public.permissions WHERE key = 'lote.corregir_aves') AS nuevo
WHERE NOT EXISTS (
        SELECT 1 FROM public.role_permissions x
        WHERE x.role_id = rp.role_id AND x.permission_id = nuevo.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) Y al rol Admin, que puede no tener editar_registro cableado y igual tiene que poder.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
FROM public.permissions p
WHERE p.key = 'lote.corregir_aves'
  AND EXISTS (SELECT 1 FROM public.roles r WHERE r.id = 1)
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = 1 AND rp.permission_id = p.id);
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'lote.corregir_aves');
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'lote.corregir_aves');
DELETE FROM public.permissions         WHERE key = 'lote.corregir_aves';
";
    }
}

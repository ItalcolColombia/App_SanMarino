using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Permiso <c>lote.corregir_fecha_encaset</c>: corregir la <b>FECHA / HORA de encasetamiento</b> de
    /// un lote de pollo engorde que ya tiene registros, con su recálculo en cascada.
    ///
    /// <para>
    /// <b>Qué habilita.</b> Guardar una fecha u hora de encasetamiento distinta en un lote con
    /// seguimiento cargado. No es un campo más del formulario: el cambio propaga a los lotes
    /// reproductora hijos, re-fecha el cruce reproductora → pollo engorde, corre el primer día con
    /// registro y reescribe el saldo de alimento de toda la serie.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué una key nueva y no <c>lote.corregir_aves</c>.</b> Pedido explícito de operación: son
    /// dos correcciones distintas —CUÁNTAS aves entraron y CUÁNDO entraron— y quieren poder darlas por
    /// separado. Hasta hoy la fecha no tenía <b>ningún</b> gate: el formulario la ofrecía a cualquiera
    /// con acceso a la granja y el <c>PUT</c> la guardaba sin preguntar.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Se hereda de <c>lote.corregir_aves</c> (+ el rol 1) — nadie queda trabado el día del
    /// deploy.</b> Es su padre natural: quien hoy corrige el encasetamiento de un lote es quien
    /// corrige su fecha. El permiso <b>invierte el default</b> (antes abierto, ahora cerrado), así que
    /// sin la herencia el gate nuevo le sacaría de golpe a alguien algo que hoy hace. Mismo patrón
    /// anti-lockout de <c>20260825140000_SeedPermisoLoteCorregirAves</c>, que a su vez heredó de
    /// <c>editar_registro</c>.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Los permisos viajan dentro de la sesión cifrada</b>, no se consultan por acción: quien
    /// reciba el permiso después del deploy tiene que cerrar sesión y volver a entrar (o cambiar de
    /// empresa) para que le aparezca.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/fecha_encaset_recalculo_cascada_plan.md</c> §4.1.
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot vigente, ModelSnapshot intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c>), localizando por <c>permissions.key</c>.
    /// </summary>
    public partial class SeedPermisoLoteCorregirFechaEncaset : Migration
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
-- 1) La key. Nombrada por el COMPORTAMIENTO, no por el modulo ni por el tenant que la pidio.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT 'lote.corregir_fecha_encaset',
       'Gestion de Lotes: corregir la fecha u hora de encasetamiento de un lote que ya tiene registros. El cambio se propaga a los lotes reproductora, re-fecha el cruce hacia el seguimiento de pollo engorde y recalcula el saldo de alimento de toda la serie.'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'lote.corregir_fecha_encaset');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Asignable en TODAS las empresas: los lotes son un modulo base que toda empresa ya tiene.
--    company_permissions es fail-closed: sin esta fila el permiso no viaja en el JWT.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
FROM public.companies c
CROSS JOIN public.permissions p
WHERE p.key = 'lote.corregir_fecha_encaset'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_permissions x
        WHERE x.company_id = c.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) HERENCIA desde lote.corregir_aves: su padre natural. Hasta hoy la fecha no tenia gate
--    ninguno, asi que sin esta herencia el permiso le quitaria de golpe la funcion a quien la usa.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, nuevo.id
FROM public.role_permissions rp
JOIN public.permissions origen ON origen.id = rp.permission_id AND origen.key = 'lote.corregir_aves'
CROSS JOIN LATERAL (SELECT id FROM public.permissions WHERE key = 'lote.corregir_fecha_encaset') AS nuevo
WHERE NOT EXISTS (
        SELECT 1 FROM public.role_permissions x
        WHERE x.role_id = rp.role_id AND x.permission_id = nuevo.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) Y al rol Admin, que puede no tener lote.corregir_aves cableado y igual tiene que poder.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
FROM public.permissions p
WHERE p.key = 'lote.corregir_fecha_encaset'
  AND EXISTS (SELECT 1 FROM public.roles r WHERE r.id = 1)
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = 1 AND rp.permission_id = p.id);
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'lote.corregir_fecha_encaset');
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'lote.corregir_fecha_encaset');
DELETE FROM public.permissions         WHERE key = 'lote.corregir_fecha_encaset';
";
    }
}

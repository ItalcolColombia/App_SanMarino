using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// B10 — el Super Admin deja de ser un correo hardcodeado en 14 sitios del código y pasa a ser un
    /// dato: <c>users.is_super_admin</c>.
    ///
    /// <para>
    /// <b>Equivalencia día uno.</b> La marca se siembra en <c>true</c> para EXACTAMENTE quien hoy la
    /// tiene por código, buscándolo por su correo de login —nunca por un guid fijo: los ids difieren
    /// local↔prod, misma convención que los seeds de ItalJira—. Si ese correo no existe en el entorno
    /// se deja un <c>NOTICE</c> y la migración NO falla.
    /// </para>
    ///
    /// <para>
    /// A partir de acá, conceder o revocar el privilegio más grande del sistema es un <c>UPDATE</c>,
    /// no un despliegue.
    /// </para>
    ///
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c> + <c>IS DISTINCT FROM</c> en el update.
    /// </summary>
    public partial class SuperAdminPorDato : Migration
    {
        /// <summary>Correo que hasta hoy decidía el privilegio dentro del código.</summary>
        private const string CorreoSuperAdminLegado = "moiesbbuga@gmail.com";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se escribe a mano (en vez del AddColumn que genera EF) para que sea idempotente:
            // la BD compartida de desarrollo la tocan varias sesiones en paralelo.
            migrationBuilder.Sql(@"
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_super_admin boolean NOT NULL DEFAULT false;
");

            migrationBuilder.Sql($@"
DO $$
DECLARE
    v_marcados int;
BEGIN
    UPDATE users u
       SET is_super_admin = true
     WHERE u.id IN (
               SELECT ul.user_id
                 FROM user_logins ul
                 JOIN logins l ON l.id = ul.login_id
                WHERE lower(l.email) = '{CorreoSuperAdminLegado}'
           )
       AND u.is_super_admin IS DISTINCT FROM true;

    GET DIAGNOSTICS v_marcados = ROW_COUNT;

    IF v_marcados = 0 AND NOT EXISTS (SELECT 1 FROM users WHERE is_super_admin) THEN
        RAISE NOTICE 'B10: no existe {CorreoSuperAdminLegado} en este entorno y nadie quedo marcado como super admin.';
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE users DROP COLUMN IF EXISTS is_super_admin;
");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Panamá: agrega columna zona a users (filtra granjas visibles por zona del usuario logueado).
    /// Idempotente.
    /// </summary>
    public partial class AddZonaToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.users
                    ADD COLUMN IF NOT EXISTS zona VARCHAR(20) NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_users_zona ON public.users(zona);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_users_zona;");
            migrationBuilder.Sql(@"
                ALTER TABLE public.users
                    DROP COLUMN IF EXISTS zona;
            ");
        }
    }
}

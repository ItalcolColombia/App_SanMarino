using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Infraestructura de <b>códigos ERP avícolas</b> (Santa Reyes, Fase 1 — solo SCHEMA; los datos
    /// van en la migración de seed posterior):
    /// <list type="bullet">
    ///   <item><c>companies.maneja_codigos_erp_avicola</c>: flag tipado por comportamiento (default
    ///   <c>false</c> = todas las empresas actuales siguen igual; el front muestra los campos ERP
    ///   solo cuando está en <c>true</c>).</item>
    ///   <item><c>farms</c>: bodega, centro de operación e instalación (código + descripción).</item>
    ///   <item><c>nucleos</c>: bodega (código + descripción).</item>
    ///   <item><c>galpones</c>: ubicación ERP (código + descripción).</item>
    ///   <item><c>lotes</c>: centro de costo (código + descripción).</item>
    ///   <item><c>farm_silos</c>: catálogo de silos de alimento / bodegas de insumos por granja (no
    ///   son galpones: no deben aparecer al crear lotes). Sin endpoints todavía.</item>
    ///   <item><c>menus</c>: columnas defensivas (<c>key</c>, <c>sort_order</c>, <c>is_group</c>,
    ///   <c>created_at</c>, <c>updated_at</c>) que existen en prod fuera de banda y NO están mapeadas
    ///   en el modelo EF; la migración de seed de Santa Reyes las necesita para enlazar menús.</item>
    /// </list>
    /// Todo aditivo (columnas nullable + tabla nueva) e <b>idempotente</b> (IF NOT EXISTS), para poder
    /// re-ejecutarse sin efectos. Cero impacto sobre empresas existentes (flag en <c>false</c>).
    /// </summary>
    public partial class AddInfraErpAvicolaSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────────────
            // companies — flag de comportamiento (nunca "if empresa == X" en código)
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    ADD COLUMN IF NOT EXISTS maneja_codigos_erp_avicola boolean NOT NULL DEFAULT false;
");

            // ─────────────────────────────────────────────────────────────────────
            // farms — bodega / centro de operación / instalación
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS codigo_bodega character varying(20) NULL;
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS descripcion_bodega character varying(200) NULL;
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS centro_operacion character varying(20) NULL;
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS descripcion_centro_operacion character varying(200) NULL;
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS codigo_instalacion character varying(20) NULL;
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS descripcion_instalacion character varying(200) NULL;
");

            // ─────────────────────────────────────────────────────────────────────
            // nucleos — bodega
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.nucleos
    ADD COLUMN IF NOT EXISTS codigo_bodega character varying(20) NULL;
ALTER TABLE public.nucleos
    ADD COLUMN IF NOT EXISTS descripcion_bodega character varying(200) NULL;
");

            // ─────────────────────────────────────────────────────────────────────
            // galpones — ubicación ERP
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.galpones
    ADD COLUMN IF NOT EXISTS codigo_erp_ubicacion character varying(20) NULL;
ALTER TABLE public.galpones
    ADD COLUMN IF NOT EXISTS descripcion_erp_ubicacion character varying(200) NULL;
");

            // ─────────────────────────────────────────────────────────────────────
            // lotes — centro de costo
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.lotes
    ADD COLUMN IF NOT EXISTS codigo_centro_costo character varying(20) NULL;
ALTER TABLE public.lotes
    ADD COLUMN IF NOT EXISTS descripcion_centro_costo character varying(200) NULL;
");

            // ─────────────────────────────────────────────────────────────────────
            // farm_silos — catálogo de silos / bodegas de insumos por granja
            // (mismo DDL que generaría EF: identity by default + PK/FK con los
            //  nombres del modelo, para que schema y snapshot queden alineados)
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.farm_silos (
    id                   integer                  GENERATED BY DEFAULT AS IDENTITY,
    company_id           integer                  NOT NULL,
    granja_id            integer                  NOT NULL,
    nombre               character varying(120)   NOT NULL,
    tipo                 character varying(20)    NOT NULL,
    codigo_erp_ubicacion character varying(20)    NULL,
    descripcion          character varying(200)   NULL,
    centro_operacion     character varying(20)    NULL,
    codigo_bodega        character varying(20)    NULL,
    activo               boolean                  NOT NULL DEFAULT TRUE,
    created_at           timestamp with time zone NOT NULL,
    CONSTRAINT pk_farm_silos PRIMARY KEY (id),
    CONSTRAINT fk_farm_silos_farm FOREIGN KEY (granja_id)
        REFERENCES public.farms (id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_farm_silos_granja
    ON public.farm_silos (granja_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_farm_silos_granja_nombre
    ON public.farm_silos (granja_id, nombre);
");

            // ─────────────────────────────────────────────────────────────────────
            // menus — columnas DEFENSIVAS: existen en prod fuera de banda y no están
            // mapeadas en el modelo EF (MenuConfiguration ignora created_at/updated_at
            // y no conoce key/sort_order/is_group). La migración de seed de Santa Reyes
            // enlaza menús existentes y necesita que estén presentes en cualquier BD.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.menus
    ADD COLUMN IF NOT EXISTS key character varying(100);
ALTER TABLE public.menus
    ADD COLUMN IF NOT EXISTS sort_order integer NOT NULL DEFAULT 0;
ALTER TABLE public.menus
    ADD COLUMN IF NOT EXISTS is_group boolean NOT NULL DEFAULT false;
ALTER TABLE public.menus
    ADD COLUMN IF NOT EXISTS created_at timestamp with time zone;
ALTER TABLE public.menus
    ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simétrico y idempotente. Las columnas defensivas de `menus` NO se tocan: no las creó
            // esta funcionalidad (existen fuera de banda en prod) y borrarlas sería destructivo.
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS public.farm_silos;

ALTER TABLE public.lotes
    DROP COLUMN IF EXISTS codigo_centro_costo,
    DROP COLUMN IF EXISTS descripcion_centro_costo;

ALTER TABLE public.galpones
    DROP COLUMN IF EXISTS codigo_erp_ubicacion,
    DROP COLUMN IF EXISTS descripcion_erp_ubicacion;

ALTER TABLE public.nucleos
    DROP COLUMN IF EXISTS codigo_bodega,
    DROP COLUMN IF EXISTS descripcion_bodega;

ALTER TABLE public.farms
    DROP COLUMN IF EXISTS codigo_bodega,
    DROP COLUMN IF EXISTS descripcion_bodega,
    DROP COLUMN IF EXISTS centro_operacion,
    DROP COLUMN IF EXISTS descripcion_centro_operacion,
    DROP COLUMN IF EXISTS codigo_instalacion,
    DROP COLUMN IF EXISTS descripcion_instalacion;

ALTER TABLE public.companies
    DROP COLUMN IF EXISTS maneja_codigos_erp_avicola;
");
        }
    }
}

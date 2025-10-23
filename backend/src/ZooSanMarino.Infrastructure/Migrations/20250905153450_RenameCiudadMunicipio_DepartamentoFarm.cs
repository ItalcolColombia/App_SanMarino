using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCiudadMunicipio_DepartamentoFarm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farms_companies_company_id",
                table: "farms");

            migrationBuilder.DropForeignKey(
                name: "fk_lote_reproductoras_lotes_lote_id",
                table: "lote_reproductoras");

            migrationBuilder.DropForeignKey(
                name: "fk_municipios_departamentos_departamento_id",
                table: "municipios");

            migrationBuilder.DropPrimaryKey(
                name: "pk_municipio",
                table: "municipios");

            migrationBuilder.DropIndex(
                name: "ix_municipios_departamento_id",
                table: "municipios");

            migrationBuilder.DropIndex(
                name: "ix_farms_company_id",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ix_nucleos_granja_id",
                table: "nucleos");

            migrationBuilder.DropIndex(
                name: "ix_lote_seguimientos_lote_id_reproductora_id",
                table: "lote_seguimientos");

            migrationBuilder.DropIndex(
                name: "ix_galpones_granja_id",
                table: "galpones");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "nucleos",
                newName: "nucleos",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "lotes",
                newName: "lotes",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "lote_seguimientos",
                newName: "lote_seguimientos",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "lote_reproductoras",
                newName: "lote_reproductoras",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "lote_galpones",
                newName: "lote_galpones",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "galpones",
                newName: "galpones",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "catalogo_items",
                newName: "catalogo_items",
                newSchema: "public");

            migrationBuilder.RenameColumn(
                name: "municipio_nombre",
                table: "municipios",
                newName: "nombre");

            // zone_id -> municipio_id (lo correcto según el modelo destino)
            migrationBuilder.RenameColumn(
                name: "zone_id",
                table: "farms",
                newName: "municipio_id");

            migrationBuilder.RenameIndex(
                name: "ix_lotes_granja_id",
                schema: "public",
                table: "lotes",
                newName: "ix_lote_granja");

            migrationBuilder.RenameIndex(
                name: "ix_lotes_galpon_id",
                schema: "public",
                table: "lotes",
                newName: "ix_lote_galpon");

            migrationBuilder.RenameColumn(
                name: "macho",
                schema: "public",
                table: "lote_galpones",
                newName: "m");

            migrationBuilder.RenameColumn(
                name: "hembra",
                schema: "public",
                table: "lote_galpones",
                newName: "h");

            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "municipios",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            // ===== FARMS: agregar y poblar departamento_id; asegurar municipio_id =====
            migrationBuilder.AddColumn<int>(
                name: "departamento_id",
                table: "farms",
                type: "integer",
                nullable: true);

            // Si existía ciudad_id, copiar a municipio_id y eliminar
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_name = 'farms' AND column_name = 'ciudad_id'
  ) THEN
    UPDATE farms
       SET municipio_id = ciudad_id
     WHERE municipio_id IS NULL
       AND ciudad_id    IS NOT NULL;

    ALTER TABLE farms DROP COLUMN ciudad_id;
  END IF;
END $$;
");

            // Asegurar que cada departamento tenga al menos un municipio (placeholder)
            migrationBuilder.Sql(@"
WITH deps_sin_mun AS (
  SELECT d.departamento_id
    FROM departamentos d
    LEFT JOIN municipios m
           ON m.departamento_id = d.departamento_id
   WHERE m.municipio_id IS NULL
)
INSERT INTO municipios (departamento_id, nombre)
SELECT departamento_id, 'SIN MUNICIPIO'
  FROM deps_sin_mun;
");

            // Poblar departamento_id desde municipio_id
            migrationBuilder.Sql(@"
UPDATE farms f
   SET departamento_id = m.departamento_id
  FROM municipios m
 WHERE m.municipio_id = f.municipio_id
   AND f.departamento_id IS NULL;
");

            // Si hay dep pero no municipio, asignar algún municipio del mismo dep
            migrationBuilder.Sql(@"
UPDATE farms f
   SET municipio_id = mm.municipio_id
  FROM (
        SELECT departamento_id, MIN(municipio_id) AS municipio_id
          FROM municipios
         GROUP BY departamento_id
       ) mm
 WHERE f.municipio_id  IS NULL
   AND f.departamento_id = mm.departamento_id;
");

            // Verificación defensiva
            migrationBuilder.Sql(@"
DO $$
DECLARE v_mun int;
DECLARE v_dep int;
BEGIN
  SELECT COUNT(*) INTO v_mun FROM farms WHERE municipio_id IS NULL;
  SELECT COUNT(*) INTO v_dep FROM farms WHERE departamento_id IS NULL;
  IF v_mun > 0 OR v_dep > 0 THEN
    RAISE EXCEPTION 'No se pudo poblar municipio_id (% filas) o departamento_id (% filas) en farms. Revise datos antes de continuar.', v_mun, v_dep;
  END IF;
END $$;
");

            // NOT NULL finales
            migrationBuilder.Sql(@"ALTER TABLE farms ALTER COLUMN municipio_id   SET NOT NULL;");
            migrationBuilder.Sql(@"ALTER TABLE farms ALTER COLUMN departamento_id SET NOT NULL;");

            // Otros cambios en farms
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "farms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1,
                oldDefaultValue: "A");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "farms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "farms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            // Cambios en otras tablas
            migrationBuilder.AlterColumn<string>(
                name: "nucleo_id",
                schema: "public",
                table: "nucleos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "regional",
                schema: "public",
                table: "lotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_id",
                schema: "public",
                table: "lotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_nombre",
                schema: "public",
                table: "lotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_id",
                schema: "public",
                table: "lotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "reproductora_id",
                schema: "public",
                table: "lote_seguimientos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_inicial",
                schema: "public",
                table: "lote_seguimientos",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_final",
                schema: "public",
                table: "lote_seguimientos",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lote_seguimientos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "consumo_alimento",
                schema: "public",
                table: "lote_seguimientos",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "public",
                table: "lote_seguimientos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_mixto",
                schema: "public",
                table: "lote_reproductoras",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_inicial_m",
                schema: "public",
                table: "lote_reproductoras",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_inicial_h",
                schema: "public",
                table: "lote_reproductoras",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nombre_lote",
                schema: "public",
                table: "lote_reproductoras",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "reproductora_id",
                schema: "public",
                table: "lote_reproductoras",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lote_reproductoras",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_id",
                schema: "public",
                table: "lote_galpones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "reproductora_id",
                schema: "public",
                table: "lote_galpones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lote_galpones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "tipo_galpon",
                schema: "public",
                table: "galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_id",
                schema: "public",
                table: "galpones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "largo",
                schema: "public",
                table: "galpones",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_nombre",
                schema: "public",
                table: "galpones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "ancho",
                schema: "public",
                table: "galpones",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_id",
                schema: "public",
                table: "galpones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "public",
                table: "catalogo_items",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                schema: "public",
                table: "catalogo_items",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            // PK municipios (nuevo nombre)
            migrationBuilder.AddPrimaryKey(
                name: "pk_municipios",
                table: "municipios",
                column: "municipio_id");

            // Renombrar PK de nucleos sin romper FKs
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
      FROM pg_constraint
     WHERE conname = 'pk_nucleos'
       AND conrelid = 'public.nucleos'::regclass
  ) THEN
    ALTER TABLE public.nucleos
      RENAME CONSTRAINT pk_nucleos TO ak_nucleos_nucleo_id_granja_id;
  END IF;
END $$;
");

            // ========= Tablas de inventario (condicionales) =========
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.farm_inventory_movements (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    farm_id integer NOT NULL,
    catalog_item_id integer NOT NULL,
    quantity numeric(18,3) NOT NULL,
    movement_type varchar(20) NOT NULL,
    unit varchar(20) NOT NULL DEFAULT 'kg',
    reference varchar(50),
    reason varchar(200),
    transfer_group_id uuid,
    metadata jsonb NOT NULL,
    responsible_user_id varchar(128),
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_farm_inventory_movements PRIMARY KEY (id),
    CONSTRAINT fk_farm_inventory_movements_catalog_items_catalog_item_id FOREIGN KEY (catalog_item_id) REFERENCES public.catalogo_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_farm_inventory_movements_farms_farm_id FOREIGN KEY (farm_id) REFERENCES farms (id) ON DELETE RESTRICT
);
");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.farm_product_inventory (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    farm_id integer NOT NULL,
    catalog_item_id integer NOT NULL,
    quantity numeric NOT NULL,
    unit text NOT NULL,
    location text NULL,
    lot_number text NULL,
    expiration_date timestamptz NULL,
    unit_cost numeric NULL,
    metadata jsonb NOT NULL,
    active boolean NOT NULL,
    responsible_user_id text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_farm_product_inventory PRIMARY KEY (id),
    CONSTRAINT fk_farm_product_inventory_catalog_items_catalog_item_id FOREIGN KEY (catalog_item_id) REFERENCES public.catalogo_items (id) ON DELETE CASCADE,
    CONSTRAINT fk_farm_product_inventory_farms_farm_id FOREIGN KEY (farm_id) REFERENCES farms (id) ON DELETE CASCADE
);
");

            // Índices de inventario (condicionales)
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_catalog_item_id ON public.farm_inventory_movements (catalog_item_id);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_fim_farm_item ON public.farm_inventory_movements (farm_id, catalog_item_id);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_fim_transfer_group ON public.farm_inventory_movements (transfer_group_id);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_fim_type ON public.farm_inventory_movements (movement_type);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_farm_product_inventory_catalog_item_id ON public.farm_product_inventory (catalog_item_id);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_farm_product_inventory_farm_id ON public.farm_product_inventory (farm_id);");

            // ===== Resto de índices y checks =====
            migrationBuilder.CreateIndex(
                name: "ix_municipios_dep_nombre",
                table: "municipios",
                columns: new[] { "departamento_id", "nombre" });

            // Columna auxiliar opcional y su índice
            migrationBuilder.AddColumn<int>(
                name: "departamento_id1",
                table: "municipios",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipios_departamento_id1",
                table: "municipios",
                column: "departamento_id1");

            migrationBuilder.CreateIndex(
                name: "ix_farms_company_name",
                table: "farms",
                columns: new[] { "company_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_farms_departamento_id",
                table: "farms",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_farms_municipio_id",
                table: "farms",
                column: "municipio_id");

            migrationBuilder.CreateIndex(
                name: "ix_companies_identifier",
                table: "companies",
                column: "identifier");

            migrationBuilder.CreateIndex(
                name: "ix_nucleo_granja_nombre",
                schema: "public",
                table: "nucleos",
                columns: new[] { "granja_id", "nucleo_nombre" });

            migrationBuilder.CreateIndex(
                name: "ix_lote_nucleo",
                schema: "public",
                table: "lotes",
                column: "nucleo_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_l_nonneg_counts",
                schema: "public",
                table: "lotes",
                sql: "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_l_nonneg_pesos",
                schema: "public",
                table: "lotes",
                sql: "(peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_ls_lote_rep_fecha",
                schema: "public",
                table: "lote_seguimientos",
                columns: new[] { "lote_id", "reproductora_id", "fecha" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_ls_nonneg_counts",
                schema: "public",
                table: "lote_seguimientos",
                sql: "(mortalidad_m >= 0 OR mortalidad_m IS NULL) AND (mortalidad_h >= 0 OR mortalidad_h IS NULL) AND (sel_m >= 0 OR sel_m IS NULL) AND (sel_h >= 0 OR sel_h IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ls_nonneg_pesos",
                schema: "public",
                table: "lote_seguimientos",
                sql: "(peso_inicial >= 0 OR peso_inicial IS NULL) AND (peso_final >= 0 OR peso_final IS NULL) AND (consumo_alimento >= 0 OR consumo_alimento IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_lote_reproductora_fecha",
                schema: "public",
                table: "lote_reproductoras",
                column: "fecha_encasetamiento");

            migrationBuilder.CreateIndex(
                name: "ix_lote_reproductora_lote",
                schema: "public",
                table: "lote_reproductoras",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_reproductora_rep",
                schema: "public",
                table: "lote_reproductoras",
                column: "reproductora_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lr_nonneg_counts",
                schema: "public",
                table: "lote_reproductoras",
                sql: "(m >= 0 OR m IS NULL) AND (h >= 0 OR h IS NULL) AND (mixtas >= 0 OR mixtas IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lr_nonneg_pesos",
                schema: "public",
                table: "lote_reproductoras",
                sql: "(peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lg_nonneg_counts",
                schema: "public",
                table: "lote_galpones",
                sql: "(m >= 0 OR m IS NULL) AND (h >= 0 OR h IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_galpon_granja_nucleo",
                schema: "public",
                table: "galpones",
                columns: new[] { "granja_id", "nucleo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_galpon_nombre",
                schema: "public",
                table: "galpones",
                column: "galpon_nombre");

            migrationBuilder.CreateIndex(
                name: "ux_galpon_company_galpon",
                schema: "public",
                table: "galpones",
                columns: new[] { "company_id", "galpon_id" },
                unique: true);

            // ===== Re-crear FKs de tablas principales =====
            migrationBuilder.AddForeignKey(
                name: "fk_farms_companies_company_id",
                table: "farms",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_farms_departamento",
                table: "farms",
                column: "departamento_id",
                principalTable: "departamentos",
                principalColumn: "departamento_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_farms_municipio",
                table: "farms",
                column: "municipio_id",
                principalTable: "municipios",
                principalColumn: "municipio_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_galpones_companies_company_id",
                schema: "public",
                table: "galpones",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lote_reproductoras_lotes_lote_id",
                schema: "public",
                table: "lote_reproductoras",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_municipios_departamento",
                table: "municipios",
                column: "departamento_id",
                principalTable: "departamentos",
                principalColumn: "departamento_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_municipios_departamentos_departamento_id1",
                table: "municipios",
                column: "departamento_id1",
                principalTable: "departamentos",
                principalColumn: "departamento_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farms_companies_company_id",
                table: "farms");

            migrationBuilder.DropForeignKey(
                name: "fk_farms_departamento",
                table: "farms");

            migrationBuilder.DropForeignKey(
                name: "fk_farms_municipio",
                table: "farms");

            migrationBuilder.DropForeignKey(
                name: "fk_galpones_companies_company_id",
                schema: "public",
                table: "galpones");

            migrationBuilder.DropForeignKey(
                name: "fk_lote_reproductoras_lotes_lote_id",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropForeignKey(
                name: "fk_municipios_departamento",
                table: "municipios");

            migrationBuilder.DropForeignKey(
                name: "fk_municipios_departamentos_departamento_id1",
                table: "municipios");

            // Tablas de inventario
            migrationBuilder.DropTable(
                name: "farm_inventory_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "farm_product_inventory");

            migrationBuilder.DropPrimaryKey(
                name: "pk_municipios",
                table: "municipios");

            migrationBuilder.DropIndex(
                name: "ix_municipios_dep_nombre",
                table: "municipios");

            migrationBuilder.DropIndex(
                name: "ix_municipios_departamento_id1",
                table: "municipios");

            migrationBuilder.DropIndex(
                name: "ix_farms_company_name",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ix_farms_departamento_id",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ix_farms_municipio_id",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ix_companies_identifier",
                table: "companies");

            migrationBuilder.DropPrimaryKey(
                name: "ak_nucleos_nucleo_id_granja_id",
                schema: "public",
                table: "nucleos");

            migrationBuilder.DropIndex(
                name: "ix_nucleo_granja_nombre",
                schema: "public",
                table: "nucleos");

            migrationBuilder.DropIndex(
                name: "ix_lote_nucleo",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_l_nonneg_counts",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_l_nonneg_pesos",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropIndex(
                name: "ix_ls_lote_rep_fecha",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ls_nonneg_counts",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ls_nonneg_pesos",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropIndex(
                name: "ix_lote_reproductora_fecha",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropIndex(
                name: "ix_lote_reproductora_lote",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropIndex(
                name: "ix_lote_reproductora_rep",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lr_nonneg_counts",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lr_nonneg_pesos",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lg_nonneg_counts",
                schema: "public",
                table: "lote_galpones");

            migrationBuilder.DropIndex(
                name: "ix_galpon_granja_nucleo",
                schema: "public",
                table: "galpones");

            migrationBuilder.DropIndex(
                name: "ix_galpon_nombre",
                schema: "public",
                table: "galpones");

            migrationBuilder.DropIndex(
                name: "ux_galpon_company_galpon",
                schema: "public",
                table: "galpones");

            migrationBuilder.DropColumn(
                name: "departamento_id1",
                table: "municipios");

            migrationBuilder.DropColumn(
                name: "departamento_id",
                table: "farms");

            migrationBuilder.RenameTable(
                name: "nucleos",
                schema: "public",
                newName: "nucleos");

            migrationBuilder.RenameTable(
                name: "lotes",
                schema: "public",
                newName: "lotes");

            migrationBuilder.RenameTable(
                name: "lote_seguimientos",
                schema: "public",
                newName: "lote_seguimientos");

            migrationBuilder.RenameTable(
                name: "lote_reproductoras",
                schema: "public",
                newName: "lote_reproductoras");

            migrationBuilder.RenameTable(
                name: "lote_galpones",
                schema: "public",
                newName: "lote_galpones");

            migrationBuilder.RenameTable(
                name: "galpones",
                schema: "public",
                newName: "galpones");

            migrationBuilder.RenameTable(
                name: "catalogo_items",
                schema: "public",
                newName: "catalogo_items");

            migrationBuilder.RenameColumn(
                name: "nombre",
                table: "municipios",
                newName: "municipio_nombre");

            migrationBuilder.RenameColumn(
                name: "municipio_id",
                table: "farms",
                newName: "zone_id");

            migrationBuilder.RenameIndex(
                name: "ix_lote_granja",
                table: "lotes",
                newName: "ix_lotes_granja_id");

            migrationBuilder.RenameIndex(
                name: "ix_lote_galpon",
                table: "lotes",
                newName: "ix_lotes_galpon_id");

            migrationBuilder.RenameColumn(
                name: "m",
                table: "lote_galpones",
                newName: "macho");

            migrationBuilder.RenameColumn(
                name: "h",
                table: "lote_galpones",
                newName: "hembra");

            migrationBuilder.AlterColumn<string>(
                name: "municipio_nombre",
                table: "municipios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "farms",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "A",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "farms",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "farms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_id",
                table: "nucleos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "regional",
                table: "lotes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_id",
                table: "lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_nombre",
                table: "lotes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_id",
                table: "lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                table: "lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "reproductora_id",
                table: "lote_seguimientos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_inicial",
                table: "lote_seguimientos",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_final",
                table: "lote_seguimientos",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                table: "lote_seguimientos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<decimal>(
                name: "consumo_alimento",
                table: "lote_seguimientos",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "lote_seguimientos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_mixto",
                table: "lote_reproductoras",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_inicial_m",
                table: "lote_reproductoras",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "peso_inicial_h",
                table: "lote_reproductoras",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nombre_lote",
                table: "lote_reproductoras",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "reproductora_id",
                table: "lote_reproductoras",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                table: "lote_reproductoras",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_id",
                table: "lote_galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "reproductora_id",
                table: "lote_galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                table: "lote_galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "tipo_galpon",
                table: "galpones",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_id",
                table: "galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "largo",
                table: "galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_nombre",
                table: "galpones",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ancho",
                table: "galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "galpon_id",
                table: "galpones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "catalogo_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "catalogo_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddPrimaryKey(
                name: "pk_municipio",
                table: "municipios",
                column: "municipio_id");

            migrationBuilder.CreateIndex(
                name: "ix_municipios_departamento_id",
                table: "municipios",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_farms_company_id",
                table: "farms",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_nucleos_granja_id",
                table: "nucleos",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_seguimientos_lote_id_reproductora_id",
                table: "lote_seguimientos",
                columns: new[] { "lote_id", "reproductora_id" });

            migrationBuilder.CreateIndex(
                name: "ix_galpones_granja_id",
                table: "galpones",
                column: "granja_id");

            migrationBuilder.AddForeignKey(
                name: "fk_farms_companies_company_id",
                table: "farms",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lote_reproductoras_lotes_lote_id",
                table: "lote_reproductoras",
                column: "lote_id",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_municipios_departamentos_departamento_id",
                table: "municipios",
                column: "departamento_id",
                principalTable: "departamentos",
                principalColumn: "departamento_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

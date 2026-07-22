using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración de <b>solo datos</b> (sin cambios de schema): asigna a <b>Panamá</b>
    /// (<c>company_id = 5</c>, <c>pais_id = 3</c>) la tabla genética oficial
    /// <b>Aviagen "Yield Plus × Ross 308 AP · Objetivos de Rendimiento · 2022"</b>, sexo
    /// <b>mixto</b> (57 filas, día 0-56), reutilizando el módulo que ya usa Ecuador
    /// (<c>guia_genetica_ecuador_header</c> / <c>_detalle</c>).
    ///
    /// Reemplaza la guía que había cargada en Panamá (año 2026, con todos los pesos/ganancia/CA
    /// en 0 → inservible) y <b>repunta los lotes de engorde de Panamá</b> de
    /// <c>ano_tabla_genetica</c> 2026 → 2022, porque los indicadores resuelven la guía por
    /// <c>lote.raza</c> + <c>lote.anoTablaGenetica</c> + <c>sexo='mixto'</c>.
    ///
    /// <para>Datos: extraídos por coordenadas del PDF oficial (columnas Día / Peso Corporal g /
    /// Ganancia Diaria g / Promedio Ganancia Diaria g / Cantidad Alimento Diario g / Alimento
    /// Acumulado g / CA). <c>mortalidad_seleccion_diaria = 0</c> (el PDF no trae esa columna).
    /// Celdas vacías del PDF en días tempranos → 0 (misma convención que la guía de Ecuador).</para>
    ///
    /// <para><b>Idempotente:</b> borra la guía de Panamá (raza ROSS 308 AP, años 2022/2026) y la
    /// re-inserta; el repunte de lotes solo afecta filas aún en 2026. Re-ejecutarla converge al
    /// mismo estado. <b>Scope estricto</b> por empresa 5 + país 3 + raza → no toca Ecuador ni
    /// Colombia. <c>created_by_user_id</c> no tiene FK (audit); <c>company_id</c> = 5 existe en
    /// <c>companies</c>.</para>
    /// </summary>
    public partial class SeedGuiaGeneticaPanamaRoss308AP2022 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) Eliminar la guía genética de Panamá para esa raza (la mala 2026 y, si existiera, la 2022
--    de una corrida previa de esta migración). La cascada borra el detalle. Scope estricto.
DELETE FROM public.guia_genetica_ecuador_header
WHERE company_id = 5
  AND pais_id = 3
  AND upper(btrim(raza)) = 'ROSS 308 AP'
  AND anio_guia IN (2022, 2026);

-- 2) Insertar el header 2022 + las 57 filas mixto (día 0-56) en una sola sentencia.
WITH nuevo AS (
    INSERT INTO public.guia_genetica_ecuador_header
        (pais_id, raza, anio_guia, estado, company_id, created_by_user_id, created_at)
    VALUES (3, 'ROSS 308 AP', 2022, 'active', 5, 1369984321, now())
    RETURNING id
)
INSERT INTO public.guia_genetica_ecuador_detalle
    (guia_genetica_ecuador_header_id, sexo, dia,
     peso_corporal_g, ganancia_diaria_g, promedio_ganancia_diaria_g,
     cantidad_alimento_diario_g, alimento_acumulado_g, ca, mortalidad_seleccion_diaria,
     company_id, created_by_user_id, created_at)
SELECT n.id, 'mixto', v.dia,
       v.peso, v.gan, v.prom, v.ad, v.aac, v.ca, 0,
       5, 1369984321, now()
FROM nuevo n
CROSS JOIN (VALUES
    (0, 44, 0, 0, 0, 0, 0.0),
    (1, 61, 17, 0, 0, 12, 0.193),
    (2, 79, 18, 0, 16, 27, 0.348),
    (3, 99, 21, 0, 19, 47, 0.471),
    (4, 123, 23, 0, 23, 70, 0.570),
    (5, 149, 26, 0, 27, 97, 0.650),
    (6, 178, 29, 0, 31, 127, 0.716),
    (7, 210, 32, 24, 35, 162, 0.771),
    (8, 246, 36, 25, 39, 201, 0.816),
    (9, 285, 39, 27, 43, 244, 0.856),
    (10, 327, 42, 28, 47, 291, 0.889),
    (11, 373, 46, 30, 52, 343, 0.919),
    (12, 422, 49, 32, 57, 399, 0.946),
    (13, 475, 53, 33, 62, 461, 0.970),
    (14, 531, 56, 35, 67, 528, 0.993),
    (15, 591, 60, 37, 72, 600, 1.014),
    (16, 654, 63, 38, 77, 677, 1.035),
    (17, 720, 66, 40, 83, 759, 1.054),
    (18, 790, 69, 41, 88, 848, 1.073),
    (19, 862, 73, 43, 94, 941, 1.092),
    (20, 938, 75, 45, 100, 1041, 1.110),
    (21, 1016, 78, 46, 105, 1146, 1.128),
    (22, 1097, 81, 48, 111, 1258, 1.147),
    (23, 1180, 83, 49, 117, 1375, 1.165),
    (24, 1266, 86, 51, 123, 1497, 1.183),
    (25, 1354, 88, 52, 129, 1626, 1.201),
    (26, 1445, 90, 54, 134, 1760, 1.219),
    (27, 1537, 92, 55, 140, 1900, 1.237),
    (28, 1630, 94, 57, 145, 2046, 1.255),
    (29, 1726, 95, 58, 151, 2197, 1.273),
    (30, 1823, 97, 59, 156, 2353, 1.291),
    (31, 1921, 98, 61, 162, 2514, 1.310),
    (32, 2020, 99, 62, 167, 2681, 1.328),
    (33, 2120, 100, 63, 172, 2853, 1.346),
    (34, 2220, 101, 64, 176, 3029, 1.365),
    (35, 2322, 101, 65, 181, 3210, 1.383),
    (36, 2423, 102, 66, 185, 3396, 1.402),
    (37, 2525, 102, 67, 190, 3585, 1.420),
    (38, 2628, 102, 68, 194, 3779, 1.439),
    (39, 2730, 102, 69, 198, 3977, 1.458),
    (40, 2832, 102, 70, 201, 4178, 1.476),
    (41, 2934, 102, 71, 205, 4383, 1.495),
    (42, 3036, 102, 71, 208, 4591, 1.513),
    (43, 3137, 101, 72, 211, 4802, 1.532),
    (44, 3238, 101, 73, 214, 5017, 1.551),
    (45, 3338, 100, 73, 217, 5233, 1.569),
    (46, 3437, 99, 74, 219, 5453, 1.588),
    (47, 3535, 98, 74, 222, 5675, 1.607),
    (48, 3633, 98, 75, 224, 5898, 1.625),
    (49, 3730, 97, 75, 226, 6124, 1.644),
    (50, 3825, 96, 76, 228, 6352, 1.662),
    (51, 3920, 94, 76, 229, 6581, 1.681),
    (52, 4013, 93, 76, 230, 6811, 1.699),
    (53, 4105, 92, 77, 232, 7043, 1.718),
    (54, 4196, 91, 77, 233, 7275, 1.736),
    (55, 4286, 90, 77, 234, 7509, 1.754),
    (56, 4374, 88, 77, 234, 7743, 1.773)
) AS v(dia, peso, gan, prom, ad, aac, ca);

-- 3) Repuntar los lotes de engorde de Panamá al año 2022 (para que enlacen con la guía nueva).
UPDATE public.lote_ave_engorde
SET ano_tabla_genetica = 2022
WHERE company_id = 5
  AND pais_id = 3
  AND ano_tabla_genetica = 2026
  AND upper(btrim(raza)) = 'ROSS 308 AP';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverso de una sola vía: elimina la guía 2022 que agregó esta migración. No restaura la
            // guía vieja (año 2026, datos rotos, no se preservaron) ni revierte el año de los lotes
            // (no se puede distinguir con certeza cuáles eran 2026 antes de esta migración).
            migrationBuilder.Sql(@"
DELETE FROM public.guia_genetica_ecuador_header
WHERE company_id = 5
  AND pais_id = 3
  AND upper(btrim(raza)) = 'ROSS 308 AP'
  AND anio_guia = 2022;
");
        }
    }
}

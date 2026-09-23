using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Backfill de <c>item_inventario</c> para Santa Reyes: los 244 ítems no-alimento (combustibles,
    /// desinfectantes, vacunas, medicamentos, empaques, mantenimiento, materia prima, insumos varios)
    /// que el usuario confirmó contra el ERP (Excel <c>Items.xlsx</c>, hoja "Items Insumos") ya estaban
    /// en <c>catalogo_items</c> desde el alta de la empresa, pero nunca se copiaron a
    /// <c>item_inventario</c> — la tabla que alimenta stock/movimientos de Gestión de Inventario y el
    /// módulo Gastos de Inventario (que filtra <c>tipo_item &lt;&gt; 'alimento'</c>). Por eso Gastos de
    /// Inventario le mostraba 0 ítems a Santa Reyes y no se podía registrar entrada/consumo de nada que
    /// no fuera alimento. Los 45 ítems de alimento YA estaban completos en las dos tablas; no se tocan.
    /// </summary>
    /// <remarks>
    /// Ver <c>fase_de_desarrollo/catalogo_items_santa_reyes_erp_plan.md</c> (auditoría completa).
    ///
    /// <b>Unidad de medida.</b> El Excel fuente no trae columna de unidad para los ítems de insumos.
    /// Cada una de las 244 se infirió del texto de "Desc. item" del ERP
    /// (<c>InferenciaUnidadInventarioCalculos</c>, con tests xUnit) y viaja PRECALCULADA en el
    /// <c>VALUES</c> de abajo: es dato, no lógica, así queda auditable en el diff de esta migración.
    /// La unidad es una etiqueta (no participa de ninguna aritmética de saldo), corregible después a
    /// mano desde la pantalla de catálogo si algún caso puntual no encajó bien.
    ///
    /// <b>Identidad y fail-open.</b> La empresa se resuelve por nombre (Santa Reyes), nunca por id; el
    /// país se infiere de un ítem de alimento ya existente de esa empresa (mismo patrón que
    /// <c>SeedProductosNoConformesSantaReyes</c>). Sin empresa o sin poder inferir país, no se siembra
    /// nada y la app arranca igual en cualquier otro entorno.
    ///
    /// <b>Idempotencia.</b> <c>ON CONFLICT (company_id, pais_id, codigo) DO NOTHING</c> sobre el índice
    /// único <c>uq_item_inv_company_pais_codigo</c>: correr la migración dos veces no duplica ninguna
    /// fila. El <c>Down</c> borra exactamente esos 244 códigos (nunca alimento ni huevo).
    /// </remarks>
    public partial class AddItemInventarioInsumosSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string SEED_SQL = @"
DO $$
DECLARE
    v_company integer;
    v_pais    integer;
    v_ahora   timestamptz := timezone('utc', now());
BEGIN
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    ORDER BY c.id
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'Insumos item_inventario Santa Reyes: no existe la empresa en este entorno; omitido.';
        RETURN;
    END IF;

    SELECT ci.pais_id INTO v_pais
    FROM public.catalogo_items ci
    WHERE ci.company_id = v_company AND ci.item_type = 'alimento' AND ci.pais_id IS NOT NULL
    LIMIT 1;

    IF v_pais IS NULL THEN
        RAISE NOTICE 'Insumos item_inventario Santa Reyes: no hay ningun item de alimento existente para inferir el pais; omitido.';
        RETURN;
    END IF;

    -- Backfill: 244 items no-alimento (combustibles, desinfectantes, vacunas, medicamentos, empaques,
    -- mantenimiento, materia prima, insumos) que YA estan en catalogo_items (alimentan el catalogo de
    -- alimento admin) pero nunca se copiaron a item_inventario (alimenta stock/movimientos de Gestion
    -- de Inventario y el modulo Gastos de Inventario, que filtra tipo_item <> 'alimento'). Sin esta
    -- fila, Gastos de Inventario le mostraba 0 items a Santa Reyes.
    --
    -- La unidad de cada codigo se infirio del texto de su descripcion en el ERP
    -- (InferenciaUnidadInventarioCalculos, con tests) y viaja precalculada en este VALUES: es dato,
    -- no logica, asi que queda auditable en el diff de esta migracion.
    INSERT INTO public.item_inventario
        (codigo, nombre, tipo_item, unidad, activo, company_id, pais_id,
         grupo, tipo_inventario_codigo, descripcion_tipo_inventario, referencia, descripcion_item, concepto,
         created_at, updated_at)
    SELECT
        ci.codigo, ci.nombre, ci.item_type, u.unidad, ci.activo, v_company, v_pais,
        NULL, NULL, NULL, ci.metadata->>'referencia', NULL, ci.metadata->>'categoria',
        v_ahora, v_ahora
    FROM public.catalogo_items ci
    JOIN (VALUES
        ('615','und'),
        ('623','und'),
        ('625','und'),
        ('643','und'),
        ('644','und'),
        ('646','kg'),
        ('647','und'),
        ('670','und'),
        ('677','und'),
        ('678','und'),
        ('700','und'),
        ('702','und'),
        ('703','und'),
        ('706','und'),
        ('708','und'),
        ('709','und'),
        ('772','l'),
        ('774','l'),
        ('775','g'),
        ('785','l'),
        ('786','l'),
        ('787','kg'),
        ('788','l'),
        ('789','l'),
        ('790','kg'),
        ('792','l'),
        ('793','l'),
        ('794','kg'),
        ('796','kg'),
        ('797','l'),
        ('799','l'),
        ('800','l'),
        ('801','l'),
        ('802','l'),
        ('803','l'),
        ('804','ml'),
        ('805','dosis'),
        ('806','dosis'),
        ('808','dosis'),
        ('809','dosis'),
        ('810','ml'),
        ('811','und'),
        ('812','und'),
        ('813','ml'),
        ('814','ml'),
        ('815','dosis'),
        ('816','ml'),
        ('817','ml'),
        ('820','ml'),
        ('821','ml'),
        ('822','dosis'),
        ('823','ml'),
        ('824','dosis'),
        ('825','dosis'),
        ('826','ml'),
        ('827','dosis'),
        ('831','l'),
        ('832','l'),
        ('835','dosis'),
        ('839','dosis'),
        ('840','dosis'),
        ('842','dosis'),
        ('843','dosis'),
        ('845','l'),
        ('846','dosis'),
        ('848','dosis'),
        ('849','dosis'),
        ('850','dosis'),
        ('851','dosis'),
        ('852','dosis'),
        ('853','dosis'),
        ('855','dosis'),
        ('856','dosis'),
        ('862','dosis'),
        ('863','l'),
        ('864','l'),
        ('866','l'),
        ('867','kg'),
        ('868','l'),
        ('869','kg'),
        ('870','und'),
        ('871','kg'),
        ('874','l'),
        ('880','kg'),
        ('881','kg'),
        ('884','kg'),
        ('885','g'),
        ('886','ml'),
        ('887','ml'),
        ('890','und'),
        ('893','kg'),
        ('900','ml'),
        ('906','kg'),
        ('912','kg'),
        ('915','l'),
        ('916','und'),
        ('917','g'),
        ('918','kg'),
        ('920','ml'),
        ('923','kg'),
        ('924','kg'),
        ('926','ml'),
        ('928','l'),
        ('930','und'),
        ('931','l'),
        ('932','l'),
        ('933','und'),
        ('934','kg'),
        ('935','l'),
        ('936','ml'),
        ('938','l'),
        ('939','kg'),
        ('940','kg'),
        ('941','kg'),
        ('943','l'),
        ('946','g'),
        ('947','ml'),
        ('951','g'),
        ('954','l'),
        ('955','l'),
        ('1194','und'),
        ('1196','lb'),
        ('1200','l'),
        ('1202','kg'),
        ('1205','kg'),
        ('1208','und'),
        ('1236','l'),
        ('1239','l'),
        ('1241','l'),
        ('1246','kg'),
        ('1248','kg'),
        ('1468','und'),
        ('1471','l'),
        ('1472','l'),
        ('1473','und'),
        ('1474','kg'),
        ('1476','l'),
        ('1477','l'),
        ('1509','gal'),
        ('1510','ml'),
        ('1512','ml'),
        ('1520','und'),
        ('1521','und'),
        ('1675','kg'),
        ('1689','kg'),
        ('1706','kg'),
        ('1727','kg'),
        ('1741','dosis'),
        ('1854','und'),
        ('1855','gal'),
        ('1856','ml'),
        ('1857','ml'),
        ('1863','l'),
        ('1891','und'),
        ('1892','l'),
        ('1893','g'),
        ('1895','kg'),
        ('1896','l'),
        ('1901','gal'),
        ('1904','dosis'),
        ('1905','dosis'),
        ('1906','l'),
        ('1907','dosis'),
        ('1908','dosis'),
        ('1909','l'),
        ('1910','dosis'),
        ('1911','und'),
        ('1914','l'),
        ('1915','und'),
        ('1916','gal'),
        ('1948','dosis'),
        ('1949','gal'),
        ('1950','gal'),
        ('1951','gal'),
        ('1953','gal'),
        ('1955','l'),
        ('1956','l'),
        ('1958','dosis'),
        ('1960','l'),
        ('1961','dosis'),
        ('1963','l'),
        ('1964','kg'),
        ('1967','l'),
        ('1968','dosis'),
        ('1969','l'),
        ('1970','l'),
        ('1971','kg'),
        ('1973','gal'),
        ('1975','dosis'),
        ('1978','dosis'),
        ('1979','kg'),
        ('1981','dosis'),
        ('1983','dosis'),
        ('1984','l'),
        ('1988','kg'),
        ('1989','kg'),
        ('1992','kg'),
        ('1994','kg'),
        ('1996','l'),
        ('2014','ml'),
        ('2020','und'),
        ('2181','kg'),
        ('2197','kg'),
        ('2223','kg'),
        ('2381','und'),
        ('2390','und'),
        ('2408','und'),
        ('2446','und'),
        ('2457','kg'),
        ('2465','l'),
        ('2466','l'),
        ('2471','l'),
        ('2476','l'),
        ('2477','l'),
        ('2479','g'),
        ('2480','l'),
        ('2484','dosis'),
        ('2486','kg'),
        ('2490','ml'),
        ('2500','l'),
        ('2503','l'),
        ('2504','g'),
        ('2509','dosis'),
        ('2511','ml'),
        ('2519','l'),
        ('2529','kg'),
        ('2530','l'),
        ('2533','und'),
        ('2534','l'),
        ('2559','l'),
        ('2579','und'),
        ('2587','kg'),
        ('2600','l'),
        ('2609','kg'),
        ('2621','l'),
        ('2668','l'),
        ('2678','l'),
        ('2743','dosis'),
        ('2747','ml'),
        ('2806','dosis'),
        ('2811','g'),
        ('5335','kg'),
        ('5390','dosis'),
        ('5391','dosis')
    ) AS u(codigo, unidad) ON u.codigo = ci.codigo
    WHERE ci.company_id = v_company AND ci.pais_id = v_pais
      AND ci.item_type NOT IN ('alimento', 'huevo')
    ON CONFLICT (company_id, pais_id, codigo) DO NOTHING;

    RAISE NOTICE 'Insumos item_inventario Santa Reyes sembrados: empresa % / pais %', v_company, v_pais;
END $$;
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    v_company integer;
BEGIN
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    LIMIT 1;

    IF v_company IS NULL THEN
        RETURN;
    END IF;

    DELETE FROM public.item_inventario
    WHERE company_id = v_company
      AND tipo_item NOT IN ('alimento', 'huevo')
      AND codigo IN (
        '615', '623', '625', '643', '644', '646', '647', '670', '677', '678',
        '700', '702', '703', '706', '708', '709', '772', '774', '775', '785',
        '786', '787', '788', '789', '790', '792', '793', '794', '796', '797',
        '799', '800', '801', '802', '803', '804', '805', '806', '808', '809',
        '810', '811', '812', '813', '814', '815', '816', '817', '820', '821',
        '822', '823', '824', '825', '826', '827', '831', '832', '835', '839',
        '840', '842', '843', '845', '846', '848', '849', '850', '851', '852',
        '853', '855', '856', '862', '863', '864', '866', '867', '868', '869',
        '870', '871', '874', '880', '881', '884', '885', '886', '887', '890',
        '893', '900', '906', '912', '915', '916', '917', '918', '920', '923',
        '924', '926', '928', '930', '931', '932', '933', '934', '935', '936',
        '938', '939', '940', '941', '943', '946', '947', '951', '954', '955',
        '1194', '1196', '1200', '1202', '1205', '1208', '1236', '1239', '1241', '1246',
        '1248', '1468', '1471', '1472', '1473', '1474', '1476', '1477', '1509', '1510',
        '1512', '1520', '1521', '1675', '1689', '1706', '1727', '1741', '1854', '1855',
        '1856', '1857', '1863', '1891', '1892', '1893', '1895', '1896', '1901', '1904',
        '1905', '1906', '1907', '1908', '1909', '1910', '1911', '1914', '1915', '1916',
        '1948', '1949', '1950', '1951', '1953', '1955', '1956', '1958', '1960', '1961',
        '1963', '1964', '1967', '1968', '1969', '1970', '1971', '1973', '1975', '1978',
        '1979', '1981', '1983', '1984', '1988', '1989', '1992', '1994', '1996', '2014',
        '2020', '2181', '2197', '2223', '2381', '2390', '2408', '2446', '2457', '2465',
        '2466', '2471', '2476', '2477', '2479', '2480', '2484', '2486', '2490', '2500',
        '2503', '2504', '2509', '2511', '2519', '2529', '2530', '2533', '2534', '2559',
        '2579', '2587', '2600', '2609', '2621', '2668', '2678', '2743', '2747', '2806',
        '2811', '5335', '5390', '5391'
      );
END $$;
";
    }
}

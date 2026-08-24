using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// F8.1 — completa 7 ítems de producto no conforme (PNC) que faltaban en el catálogo de huevo
    /// de Santa Reyes, <b>sin código ERP</b> (columna ya opcional desde
    /// <c>CatalogoItemsCodigoOpcional</c>): <c>Decolorado</c> para Blanco/Azur/Criollo (Rojo ya
    /// existía) y <c>Enyemado</c> para las 4 razas (no existía ninguno).
    /// </summary>
    /// <remarks>
    /// <b>Por qué SOLO estas 4 razas y no las 7 líneas de Primera.</b> El catálogo real (verificado
    /// en BD el 21-ago-2026) ya tenía <c>Manchado</c> y <c>Picado</c> completos para exactamente
    /// Rojo/Blanco/Azur/Criollo — ninguna de las dos categorías cubre Gallina Feliz/Bonegg/Libre de
    /// Jaula Certificado. Esta migración completa el patrón que YA ESTABA, no inventa uno nuevo: es
    /// una simetría, no una decisión de negocio sobre razas nuevas.
    ///
    /// <b>Qué queda sin decidir a propósito.</b> <c>Fárfara</c> (hoy un único ítem genérico, sin
    /// raza) NO se toca: dividirlo en 4 ítems por raza cambiaría el ítem que otras filas ya puedan
    /// referenciar, y ese es un cambio de alcance real, no una completitud de patrón. Sigue
    /// documentado en el caso <c>TK-2026-000180</c> / <c>SR-DEF-3</c>.
    ///
    /// <b>Por qué sin código.</b> Los códigos del catálogo son códigos del ERP del cliente
    /// (537, 538, 539, 1944, 2124…) — inventar uno nuevo crea un ítem que el ERP no reconoce y la
    /// conciliación falla en silencio recién cuando se cargue producción real. Con
    /// <c>codigo</c> ya opcional, el ítem se puede crear, usar y clasificar hoy; el código se
    /// completa después desde la pantalla de catálogo (una sola vez, ver
    /// <c>CatalogItemService.UpdateAsync</c>) apenas Santa Reyes lo confirme.
    ///
    /// <b>Nombres.</b> Mismo patrón de palabras que ya usa el catálogo: Criollo va ANTES de la
    /// categoría (<c>HUEVO CRIOLLO MANCHADO</c>), Rojo/Blanco/Azur van DESPUÉS
    /// (<c>HUEVO MANCHADO ROJO</c>) — se repite tal cual para no introducir un tercer orden de
    /// palabras en el mismo catálogo.
    ///
    /// <b>Identidad y fail-open.</b> La empresa se resuelve por nombre (Santa Reyes), nunca por id.
    /// Sin la empresa no se siembra nada (<c>RAISE NOTICE</c> + <c>RETURN</c>) y la app arranca igual.
    ///
    /// <b>Idempotencia.</b> Cada ítem se busca por <c>(company_id, pais_id, nombre, item_type)</c> —
    /// no por <c>codigo</c>, que acá es <c>NULL</c> en las 7 filas y un <c>NULL</c> nunca iguala a
    /// otro en un <c>WHERE codigo = ...</c>, así que buscar por código habría duplicado en la
    /// segunda corrida. Correr la migración dos veces no duplica ninguna fila.
    /// </remarks>
    public partial class SeedProductosNoConformesSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>Borra exactamente los 7 ítems sembrados, localizados por su nombre exacto.</summary>
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
    v_meta    jsonb := '{""um"": ""UND"", ""categoria"": ""HUEVO"", ""tipoHuevo"": ""Pnc""}'::jsonb;
BEGIN
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    ORDER BY c.id
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'PNC Santa Reyes: no existe la empresa en este entorno; omitido.';
        RETURN;
    END IF;

    SELECT ci.pais_id INTO v_pais
    FROM public.catalogo_items ci
    WHERE ci.company_id = v_company AND ci.item_type = 'huevo' AND ci.pais_id IS NOT NULL
    LIMIT 1;

    IF v_pais IS NULL THEN
        RAISE NOTICE 'PNC Santa Reyes: no hay ningun item de huevo existente para inferir el pais; omitido.';
        RETURN;
    END IF;

    INSERT INTO public.catalogo_items
        (codigo, nombre, item_type, metadata, activo, company_id, pais_id, created_at, updated_at)
    SELECT NULL, t.nombre, 'huevo', v_meta, true, v_company, v_pais, v_ahora, v_ahora
    FROM (VALUES
        ('HUEVO DECOLORADO BLANCO'),
        ('HUEVO DECOLORADO AZUR'),
        ('HUEVO CRIOLLO DECOLORADO'),
        ('HUEVO ENYEMADO ROJO'),
        ('HUEVO ENYEMADO BLANCO'),
        ('HUEVO ENYEMADO AZUR'),
        ('HUEVO CRIOLLO ENYEMADO')
    ) AS t(nombre)
    WHERE NOT EXISTS (
        SELECT 1 FROM public.catalogo_items x
        WHERE x.company_id = v_company AND x.pais_id = v_pais
          AND x.item_type = 'huevo' AND x.nombre = t.nombre);

    RAISE NOTICE 'PNC Santa Reyes sembrados: empresa % / pais %', v_company, v_pais;
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

    DELETE FROM public.catalogo_items
    WHERE company_id = v_company AND item_type = 'huevo'
      AND nombre IN (
        'HUEVO DECOLORADO BLANCO', 'HUEVO DECOLORADO AZUR', 'HUEVO CRIOLLO DECOLORADO',
        'HUEVO ENYEMADO ROJO', 'HUEVO ENYEMADO BLANCO', 'HUEVO ENYEMADO AZUR', 'HUEVO CRIOLLO ENYEMADO'
      );
END $$;
";
    }
}

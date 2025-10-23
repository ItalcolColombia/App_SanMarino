// file: backend/src/ZooSanMarino.Infrastructure/Seed/CatalogItemSeed.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Seed;

public static class CatalogItemSeed
{
    private static readonly (string codigo, string nombre)[] _data = new[]
    {
        // === PASTE DE TUS CÓDIGOS Y NOMBRES ===
        ("040475","GR PRODUCCION I REPROD PESA H GRANE"),
        ("041062","GR PRODUCCION II REPROD PESA H GRAN"),
        ("002962","HUEVO PREPICO REPROD.FI A GRANEL"),
        ("018835","HUEVO PREPICO FASE III PESADA A GRA"),
        ("003037","ITAL POLLA LEVANTE REPRODUCTORA LIV"),
        ("024607","ITAL POLLITA PREIN REPRODUCTORA LIV"),
        ("024608","ITAL POLLITA INICI REPRODUCTORA LIV"),
        ("002974","ITAL PREPICO REPRODUCTORA LIVIANA"),
        ("024609","ITAL POLLITA PREPOSTURA REPRODUCTORA LIVIAN"),
        ("001953","MACHOS REPRODUCTORES ABUELAS"),
        ("003401","MACHOS REPRODUCTORES"),
        ("026658","MACHOS REPRODUCTORES MED H"),
        ("030727","MACHOS REPRODUCTORES SASSO Q"),
        ("029721","PAVA MANTENIMIENTO PLL R6"),
        ("029717","PAVA REPROD CRECIMIEN 1 R2"),
        ("029718","PAVA REPROD CRECIMIEN 2 R3"),
        ("029720","PAVA REPROD PRODUCCIN R5"),
        ("029122","PAVO DESARROLLO"),
        ("030504","PAVO FINALIZACION"),
        ("028882","PAVO INICIADOR"),
        ("028962","PAVO LEVANTE"),
        ("022408","POLLA CRECIMIENTO LEV"),
        ("025010","POLLA CRECIMIENTO REPRODUCTORA LIVI"),
        ("000464","POLLA LEVANTE REPRODUCTORA PESADA"),
        ("000565","POLLA LEVANTE"),
        ("003428","POLLA LEVANTE REPRODUCTORA ABUELA"),
        ("000566","POLLITA INICIACION"),
        ("000691","POLLITA INICIACION REPRODUCTORA PES"),
        ("003671","POLLITA INICIACION REPRODUCTORA ABU"),
        ("000567","POLLITA PREINICIADOR"),
        ("038099","POLLITA PREINICIADOR Q LEV"),
        ("005943","POLLITO PREINICIADOR A GRANEL"),
        ("022070","POLLITO PREINICIADOR"),
        ("035884","POLLITO PREINICIADOR LEV"),
        ("003498","POLLO ENGORDE ABUELAS"),
        ("026657","PREPICO REPRODUCTORA PESADA MED H"),
        ("001560","PREPOSTURA REPRODUCTORA PESADA"),
        ("003362","PREPOSTURA REPRODUCTORA ABUELAS"),
        ("000522","PRODUCCION 1 REPRODUCTORA PESADA"),
        ("003361","PRODUCCION I ABUELAS"),
        ("036181","PRODUCCION I ABUELAS LM"),
        ("040584","PRODUCCION I REPROD PESADA GR"),
        ("000490","PRODUCCION II REPRODUCTORA PESADA"),
        ("006417","PRODUCCION III REPRODUCTORA PESADA"),
        ("011578","PRODUCCION II ABUELASN  ABUELAS"),
        ("011760","PRODUCCION III ABUELAS"),
        ("018541","PRODUCCION III REPRODUCTORA PESADA"),
        ("026663","PRODUCCION III REPRODUCTORA PESADA"),
        ("036182","PRODUCCION II ABUELAS LM"),
        ("036183","PRODUCCION III ABUELAS LM"),
        ("030728","PRODUCCION REPROD SASSO"),
        ("026659","S.HUEVO INCUBACION FASE II S ESP ME"),
        ("041120","S.HUEVO INCUBACION FASE II GR PN"),
        ("041121","S.HUEVO INCUBAC FASE II GR MED PN"),
        ("005337","SUPER POLLITO INICIACION"),
        ("005354","SUPER POLLITO INICIACION A GRANEL"),
        ("035784","SUPER POLLITO INICIACION DORADO"),
        ("005338","SUPER POLLO ENGORDE GANA POLLO P"),
        ("005356","SUPER POLLO ENGORDE AVIAN A GRANEL"),
        ("035261","SUPER POLLO ENGORDE DORADO P"),
    };

    public static async Task EnsureAsync(ZooSanMarinoContext ctx, CancellationToken ct = default)
    {
        // Garantiza existencia de la tabla
        var exists = await ctx.Database
            .SqlQueryRaw<int>("SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'catalogo_items' LIMIT 1")
            .AnyAsync(ct);
        if (!exists) return;

        var now = DateTimeOffset.UtcNow;

        // Trae existentes por código
        var codigos = _data.Select(x => x.codigo).ToArray();
        var existentes = await ctx.CatalogItems
            .Where(x => codigos.Contains(x.Codigo))
            .ToListAsync(ct);

        // Actualiza (upsert parte 1)
        foreach (var e in existentes)
        {
            var d = _data.First(x => x.codigo == e.Codigo);
            e.Nombre = d.nombre;
            e.Activo = true;
            e.UpdatedAt = now;
        }

        // Inserta faltantes (upsert parte 2)
        var existentesSet = existentes.Select(x => x.Codigo).ToHashSet();
        var nuevos = _data
            .Where(d => !existentesSet.Contains(d.codigo))
            .Select(d => new CatalogItem
            {
                Codigo    = d.codigo,
                Nombre    = d.nombre,
                Activo    = true,
                Metadata  = JsonDocument.Parse("{}"),
                CreatedAt = now,
                UpdatedAt = now
            });

        await ctx.CatalogItems.AddRangeAsync(nuevos, ct);
        await ctx.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

// Namespace PLANO aunque el archivo esté en subcarpeta (convención de CLAUDE.md):
// la clase sigue siendo UNA, así que DI, interfaz y comportamiento quedan intactos.
namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Resolución del <b>silo</b> como ubicación del inventario (Fase B del plan
/// <c>fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md</c>).
///
/// <para>
/// Este archivo es la única puerta por la que un <c>siloId</c> del request llega al stock. Todo lo
/// que decide está en <see cref="InventarioUbicacionSiloCalculos"/> (puro, con tests); acá solo se
/// resuelven datos: el flag de la empresa dueña de la granja y la pertenencia del silo.
/// </para>
/// </summary>
public partial class InventarioGestionService
{
    /// <summary>
    /// Modo de ubicación de la granja: lo decide la empresa <b>dueña de la granja</b>
    /// (<c>farms.company_id</c>), NUNCA la empresa activa del token.
    ///
    /// <para>
    /// Fail-closed: si la granja no existe o está borrada, se devuelve el modo clásico. Es el modo
    /// que no habilita nada nuevo, y de todos modos el llamador va a fallar más adelante al resolver
    /// la granja (<c>GetFarmCompanyAndPaisAsync</c> lanza). Elegir el modo por silo ante la duda
    /// abriría la puerta a escribir en el silo de otra empresa.
    /// </para>
    /// </summary>
    private async Task<ModoUbicacionInventario> ResolverModoUbicacionAsync(int farmId, CancellationToken ct)
    {
        var manejaPorSilo = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId && f.DeletedAt == null)
            .Join(_db.Companies.AsNoTracking(), f => f.CompanyId, c => c.Id, (_, c) => c.ManejaInventarioPorSilo)
            .FirstOrDefaultAsync(ct);

        return InventarioUbicacionSiloCalculos.ResolverModo(manejaPorSilo);
    }

    /// <summary>
    /// El silo tiene que ser <b>de esa granja</b>, estar activo y no borrado. Devuelve el nombre para
    /// poder nombrarlo en los mensajes.
    ///
    /// <para>
    /// Regla 4 del plan: un silo de otra granja se rechaza, <b>nunca</b> se descuenta en silencio.
    /// Es la misma clase de fuga que ya costó una migración de fix cuando un galpón cambiaba de
    /// núcleo y sus silos quedaban cruzados.
    /// </para>
    /// </summary>
    private async Task<string> ValidarSiloDeGranjaAsync(int farmId, int siloId, CancellationToken ct)
    {
        var silo = await _db.FarmSilos.AsNoTracking()
            .Where(fs => fs.Id == siloId && fs.DeletedAt == null)
            .Select(fs => new { fs.GranjaId, fs.Nombre, fs.Activo })
            .FirstOrDefaultAsync(ct);

        if (silo is null)
            throw new InvalidOperationException($"El silo {siloId} no existe.");
        if (silo.GranjaId != farmId)
            throw new InvalidOperationException(
                $"El silo «{silo.Nombre}» no pertenece a la granja del movimiento.");
        if (!silo.Activo)
            throw new InvalidOperationException(
                $"El silo «{silo.Nombre}» está inactivo: no admite movimientos nuevos.");

        return silo.Nombre;
    }

    /// <summary>
    /// Ubicación con la que se va a persistir un movimiento, ya validada.
    ///
    /// <para>
    /// Es el punto único que usan ingreso, traslado, recepción y consumo: si cada camino resolviera
    /// el silo por su cuenta, en algún momento uno se olvidaría de validar la pertenencia y el saldo
    /// se movería en la granja equivocada.
    /// </para>
    /// </summary>
    private async Task<(string? NucleoId, string? GalponId, int? SiloId)> ResolverUbicacionMovimientoAsync(
        int farmId,
        string? nucleoId,
        string? galponId,
        int? siloId,
        bool esAlimento,
        CancellationToken ct)
    {
        var modo = await ResolverModoUbicacionAsync(farmId, ct);

        var error = InventarioUbicacionSiloCalculos.ValidarUbicacion(modo, siloId, galponId, esAlimento);
        if (error is not null) throw new InvalidOperationException(error);

        if (modo == ModoUbicacionInventario.PorSilo)
            await ValidarSiloDeGranjaAsync(farmId, siloId!.Value, ct);

        return InventarioUbicacionSiloCalculos.NormalizarUbicacion(modo, nucleoId, galponId, siloId);
    }

    /// <summary>
    /// Nombres de los silos referidos por un conjunto de filas, para no hacer N+1 al proyectar.
    /// Devuelve un diccionario vacío si ninguna fila tiene silo, que es el caso de toda empresa con
    /// el flag apagado: cero consultas extra.
    /// </summary>
    private async Task<Dictionary<int, string>> NombresDeSilosAsync(
        IEnumerable<int?> siloIds,
        CancellationToken ct)
    {
        var ids = siloIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _db.FarmSilos.AsNoTracking()
            .Where(fs => ids.Contains(fs.Id))
            .ToDictionaryAsync(fs => fs.Id, fs => fs.Nombre, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventarioGestionSiloDto>> GetSilosElegiblesAsync(
        int farmId,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        // Fail-closed por dato: la granja tiene que ser de la empresa activa. Sin esto, un farmId
        // tipeado a mano listaría los silos de otra empresa.
        var companyIdGranja = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId && f.DeletedAt == null)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyIdGranja is null) return [];
        if (_current?.CompanyId > 0 && _current.CompanyId != companyIdGranja.Value) return [];

        var q = _db.FarmSilos.AsNoTracking()
            .Where(fs => fs.GranjaId == farmId && fs.DeletedAt == null && fs.Activo);

        // El galpón NO es la ubicación: solo acota qué silos se ofrecen. La bodega de la granja se
        // ofrece siempre —guarda alimento e insumos y es el origen del traslado bodega→silo—, aunque
        // no cuelgue de ningún galpón.
        if (!string.IsNullOrWhiteSpace(galponId))
        {
            var g = galponId.Trim();
            var n = nucleoId?.Trim();
            q = q.Where(fs =>
                fs.Tipo == Domain.Entities.FarmSilo.TipoBodega ||
                _db.GalponSilos.Any(gs =>
                    gs.FarmSiloId == fs.Id &&
                    gs.Activo &&
                    gs.GranjaId == farmId &&
                    gs.GalponId == g &&
                    (n == null || gs.NucleoId == n)));
        }

        // Orden NUMÉRICO por el número del catálogo (1, 2, 3… 38), no alfabético: por nombre el
        // selector saldría «Silo 1, Silo 10, Silo 11, …, Silo 2», que con 38 silos es inusable.
        // La bodega va al final; los silos sin catálogo caen detrás de los numerados, por nombre.
        return await q
            .OrderBy(fs => fs.Tipo == Domain.Entities.FarmSilo.TipoBodega ? 1 : 0)
            .ThenBy(fs => _db.SiloCatalogo
                .Where(sc => sc.Id == fs.SiloCatalogoId)
                .Select(sc => (int?)sc.Numero)
                .FirstOrDefault() ?? int.MaxValue)
            .ThenBy(fs => fs.Nombre)
            .Select(fs => new InventarioGestionSiloDto(
                fs.Id, fs.GranjaId, fs.Nombre, fs.Tipo, fs.CodigoErpUbicacion, fs.CodigoBodega))
            .ToListAsync(ct);
    }
}

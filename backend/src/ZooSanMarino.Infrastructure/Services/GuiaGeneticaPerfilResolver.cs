// src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaPerfilResolver.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Lee <c>companies.guia_genetica_perfil</c> de la empresa efectiva de la request.
///
/// <para>
/// La empresa se resuelve <b>por datos</b>, con el mismo <c>GetEffectiveCompanyIdAsync</c> que usan
/// <c>ProduccionAvicolaRawService</c>, <c>GuiaGeneticaService</c> y <c>ExcelImportService</c>:
/// nombre de la empresa activa —ya validado por <c>ActiveCompanyMiddleware</c>, nunca el header
/// crudo— y, si no resuelve, el <c>CompanyId</c> del token.
/// </para>
///
/// <para>
/// <b>Empresa inexistente o columna vacía ⇒ <c>'sanmarino'</c></b>, el default neutro. Es a
/// propósito y es lo que mantiene el <b>delta cero</b> de la tabla compartida: el guard de
/// <c>ProduccionAvicolaRawController</c> sólo rechaza cuando la empresa declara explícitamente el
/// perfil reducido, así que ninguna empresa que hoy escribe ahí puede quedar bloqueada por un
/// dato que falte. Del otro lado el default es fail-closed igual de fuerte: el módulo reducido
/// exige <c>'reducida'</c>, y un default <c>'sanmarino'</c> le cierra la puerta.
/// </para>
/// </summary>
public sealed class GuiaGeneticaPerfilResolver : IGuiaGeneticaPerfilResolver
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public GuiaGeneticaPerfilResolver(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    /// <inheritdoc />
    public async Task<string> PerfilEmpresaActivaAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var crudo = await _ctx.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.GuiaGeneticaPerfil)
            .FirstOrDefaultAsync(ct);

        // Resolver() LANZA ante un valor desconocido, por decisión explícita de F1: caer al default
        // en silencio dejaría escribir en la tabla equivocada sin un solo síntoma visible.
        return GuiaGeneticaPerfilCalculos.Resolver(crudo);
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (cid.HasValue) return cid.Value;
        }

        return _currentUser.CompanyId;
    }
}

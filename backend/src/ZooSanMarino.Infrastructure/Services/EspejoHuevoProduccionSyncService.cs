using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Sincroniza espejo_huevo_produccion desde produccion_diaria (SeguimientoProduccion) y traslado_huevos (Completado).
/// </summary>
public sealed class EspejoHuevoProduccionSyncService : IEspejoHuevoProduccionSyncService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public EspejoHuevoProduccionSyncService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task RecalcularEspejoHuevoProduccionAsync(int lotePosturaProduccionId, CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;

        var lpp = await _context.LotePosturaProduccion
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.LotePosturaProduccionId == lotePosturaProduccionId
                     && l.CompanyId == companyId
                     && l.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (lpp == null)
            return;

        var pq = _context.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId);

        var prodTot = await pq.SumAsync(s => (long)s.HuevoTot, cancellationToken).ConfigureAwait(false);
        var prodInc = await pq.SumAsync(s => (long)s.HuevoInc, cancellationToken).ConfigureAwait(false);
        var pLimpio = await pq.SumAsync(s => (long)s.HuevoLimpio, cancellationToken).ConfigureAwait(false);
        var pTrat = await pq.SumAsync(s => (long)s.HuevoTratado, cancellationToken).ConfigureAwait(false);
        var pSucio = await pq.SumAsync(s => (long)s.HuevoSucio, cancellationToken).ConfigureAwait(false);
        var pDef = await pq.SumAsync(s => (long)s.HuevoDeforme, cancellationToken).ConfigureAwait(false);
        var pBlanco = await pq.SumAsync(s => (long)s.HuevoBlanco, cancellationToken).ConfigureAwait(false);
        var pDy = await pq.SumAsync(s => (long)s.HuevoDobleYema, cancellationToken).ConfigureAwait(false);
        var pPiso = await pq.SumAsync(s => (long)s.HuevoPiso, cancellationToken).ConfigureAwait(false);
        var pPeq = await pq.SumAsync(s => (long)s.HuevoPequeno, cancellationToken).ConfigureAwait(false);
        var pRoto = await pq.SumAsync(s => (long)s.HuevoRoto, cancellationToken).ConfigureAwait(false);
        var pDes = await pq.SumAsync(s => (long)s.HuevoDesecho, cancellationToken).ConfigureAwait(false);
        var pOtro = await pq.SumAsync(s => (long)s.HuevoOtro, cancellationToken).ConfigureAwait(false);

        var tq = _context.TrasladoHuevos.AsNoTracking()
            .Where(t =>
                t.LotePosturaProduccionId == lotePosturaProduccionId
                && t.CompanyId == companyId
                && t.DeletedAt == null
                && t.Estado == "Completado");

        var tLimpio = await tq.SumAsync(t => (long)t.CantidadLimpio, cancellationToken).ConfigureAwait(false);
        var tTrat = await tq.SumAsync(t => (long)t.CantidadTratado, cancellationToken).ConfigureAwait(false);
        var tSucio = await tq.SumAsync(t => (long)t.CantidadSucio, cancellationToken).ConfigureAwait(false);
        var tDef = await tq.SumAsync(t => (long)t.CantidadDeforme, cancellationToken).ConfigureAwait(false);
        var tBlanco = await tq.SumAsync(t => (long)t.CantidadBlanco, cancellationToken).ConfigureAwait(false);
        var tDy = await tq.SumAsync(t => (long)t.CantidadDobleYema, cancellationToken).ConfigureAwait(false);
        var tPiso = await tq.SumAsync(t => (long)t.CantidadPiso, cancellationToken).ConfigureAwait(false);
        var tPeq = await tq.SumAsync(t => (long)t.CantidadPequeno, cancellationToken).ConfigureAwait(false);
        var tRoto = await tq.SumAsync(t => (long)t.CantidadRoto, cancellationToken).ConfigureAwait(false);
        var tDes = await tq.SumAsync(t => (long)t.CantidadDesecho, cancellationToken).ConfigureAwait(false);
        var tOtro = await tq.SumAsync(t => (long)t.CantidadOtro, cancellationToken).ConfigureAwait(false);

        var tInc = tLimpio + tTrat;
        var movTot = tLimpio + tTrat + tSucio + tDef + tBlanco + tDy + tPiso + tPeq + tRoto + tDes + tOtro;

        static int H(long historico, long mov)
        {
            var d = historico - mov;
            if (d < 0) return 0;
            if (d > int.MaxValue) return int.MaxValue;
            return (int)d;
        }

        static int ToInt(long v)
        {
            if (v < 0) return 0;
            if (v > int.MaxValue) return int.MaxValue;
            return (int)v;
        }

        var hTot = ToInt(prodTot);
        var hInc = ToInt(prodInc);
        var hLimpio = ToInt(pLimpio);
        var hTrat = ToInt(pTrat);
        var hSucio = ToInt(pSucio);
        var hDef = ToInt(pDef);
        var hBlanco = ToInt(pBlanco);
        var hDy = ToInt(pDy);
        var hPiso = ToInt(pPiso);
        var hPeq = ToInt(pPeq);
        var hRoto = ToInt(pRoto);
        var hDes = ToInt(pDes);
        var hOtro = ToInt(pOtro);

        var espejo = await _context.EspejoHuevoProduccion
            .FirstOrDefaultAsync(e => e.LotePosturaProduccionId == lotePosturaProduccionId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (espejo == null)
        {
            espejo = new EspejoHuevoProduccion
            {
                LotePosturaProduccionId = lotePosturaProduccionId,
                CompanyId = companyId,
                CreatedAt = now
            };
            _context.EspejoHuevoProduccion.Add(espejo);
        }
        else if (espejo.CompanyId != companyId)
            return;

        espejo.HuevoTotHistorico = hTot;
        espejo.HuevoIncHistorico = hInc;
        espejo.HuevoLimpioHistorico = hLimpio;
        espejo.HuevoTratadoHistorico = hTrat;
        espejo.HuevoSucioHistorico = hSucio;
        espejo.HuevoDeformeHistorico = hDef;
        espejo.HuevoBlancoHistorico = hBlanco;
        espejo.HuevoDobleYemaHistorico = hDy;
        espejo.HuevoPisoHistorico = hPiso;
        espejo.HuevoPequenoHistorico = hPeq;
        espejo.HuevoRotoHistorico = hRoto;
        espejo.HuevoDesechoHistorico = hDes;
        espejo.HuevoOtroHistorico = hOtro;

        espejo.HuevoTotDinamico = H(prodTot, movTot);
        espejo.HuevoIncDinamico = H(prodInc, tInc);
        espejo.HuevoLimpioDinamico = H(pLimpio, tLimpio);
        espejo.HuevoTratadoDinamico = H(pTrat, tTrat);
        espejo.HuevoSucioDinamico = H(pSucio, tSucio);
        espejo.HuevoDeformeDinamico = H(pDef, tDef);
        espejo.HuevoBlancoDinamico = H(pBlanco, tBlanco);
        espejo.HuevoDobleYemaDinamico = H(pDy, tDy);
        espejo.HuevoPisoDinamico = H(pPiso, tPiso);
        espejo.HuevoPequenoDinamico = H(pPeq, tPeq);
        espejo.HuevoRotoDinamico = H(pRoto, tRoto);
        espejo.HuevoDesechoDinamico = H(pDes, tDes);
        espejo.HuevoOtroDinamico = H(pOtro, tOtro);

        espejo.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Sincroniza espejo_huevo_produccion desde seguimiento_diario_produccion (SeguimientoProduccion)
/// y traslado_huevos (Completado). Recalculo ABSOLUTO e idempotente: pisa todos los contadores
/// desde las fuentes — es el ÚNICO dueño del espejo (D1; el trigger legacy fue retirado por la
/// migración 20260801071000).
/// <para>
/// La empresa se resuelve POR DATOS del LPP (fail-closed multi-tenant del repo), no por
/// ICurrentUser: antes, un contexto de empresa activa distinto al del lote hacía que el recálculo
/// se salteara EN SILENCIO y el espejo quedara desactualizado. Las 24 SumAsync originales
/// (24 round-trips por cada alta/edición/borrado) quedaron colapsadas en 2 agregaciones GroupBy.
/// </para>
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
        // Empresa POR DATOS: el dueño del espejo es la empresa del LPP, no la del token.
        var lpp = await _context.LotePosturaProduccion
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.LotePosturaProduccionId == lotePosturaProduccionId
                     && l.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (lpp == null)
            return;

        var companyId = lpp.CompanyId;

        // 1 sola agregación por tabla (antes: 13 + 11 SumAsync separados, misma cifra).
        var prod = await _context.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Tot = g.Sum(s => (long)s.HuevoTot),
                Inc = g.Sum(s => (long)s.HuevoInc),
                Limpio = g.Sum(s => (long)s.HuevoLimpio),
                Trat = g.Sum(s => (long)s.HuevoTratado),
                Sucio = g.Sum(s => (long)s.HuevoSucio),
                Def = g.Sum(s => (long)s.HuevoDeforme),
                Blanco = g.Sum(s => (long)s.HuevoBlanco),
                Dy = g.Sum(s => (long)s.HuevoDobleYema),
                Piso = g.Sum(s => (long)s.HuevoPiso),
                Peq = g.Sum(s => (long)s.HuevoPequeno),
                Roto = g.Sum(s => (long)s.HuevoRoto),
                Des = g.Sum(s => (long)s.HuevoDesecho),
                Otro = g.Sum(s => (long)s.HuevoOtro)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var mov = await _context.TrasladoHuevos.AsNoTracking()
            .Where(t =>
                t.LotePosturaProduccionId == lotePosturaProduccionId
                && t.CompanyId == companyId
                && t.DeletedAt == null
                && t.Estado == "Completado")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                // Total: SIEMPRE desde TotalHuevos (columna real que el service fija en los dos
                // flujos — legacy y por ítems, ver F10 §9). Las 11 de abajo solo alimentan el
                // desglose legacy (*Dinamico por tipo); un traslado por ítems las deja en 0 a
                // propósito, así que NO deben usarse para el total o el descuento quedaría corto.
                Total = g.Sum(t => (long)t.TotalHuevos),
                Limpio = g.Sum(t => (long)t.CantidadLimpio),
                Trat = g.Sum(t => (long)t.CantidadTratado),
                Sucio = g.Sum(t => (long)t.CantidadSucio),
                Def = g.Sum(t => (long)t.CantidadDeforme),
                Blanco = g.Sum(t => (long)t.CantidadBlanco),
                Dy = g.Sum(t => (long)t.CantidadDobleYema),
                Piso = g.Sum(t => (long)t.CantidadPiso),
                Peq = g.Sum(t => (long)t.CantidadPequeno),
                Roto = g.Sum(t => (long)t.CantidadRoto),
                Des = g.Sum(t => (long)t.CantidadDesecho),
                Otro = g.Sum(t => (long)t.CantidadOtro)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var prodTot = prod?.Tot ?? 0L;
        var prodInc = prod?.Inc ?? 0L;
        var pLimpio = prod?.Limpio ?? 0L;
        var pTrat = prod?.Trat ?? 0L;
        var pSucio = prod?.Sucio ?? 0L;
        var pDef = prod?.Def ?? 0L;
        var pBlanco = prod?.Blanco ?? 0L;
        var pDy = prod?.Dy ?? 0L;
        var pPiso = prod?.Piso ?? 0L;
        var pPeq = prod?.Peq ?? 0L;
        var pRoto = prod?.Roto ?? 0L;
        var pDes = prod?.Des ?? 0L;
        var pOtro = prod?.Otro ?? 0L;

        var tLimpio = mov?.Limpio ?? 0L;
        var tTrat = mov?.Trat ?? 0L;
        var tSucio = mov?.Sucio ?? 0L;
        var tDef = mov?.Def ?? 0L;
        var tBlanco = mov?.Blanco ?? 0L;
        var tDy = mov?.Dy ?? 0L;
        var tPiso = mov?.Piso ?? 0L;
        var tPeq = mov?.Peq ?? 0L;
        var tRoto = mov?.Roto ?? 0L;
        var tDes = mov?.Des ?? 0L;
        var tOtro = mov?.Otro ?? 0L;

        var tInc = tLimpio + tTrat;
        var movTot = mov?.Total ?? 0L;

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

        espejo.HuevoTotHistorico = ToInt(prodTot);
        espejo.HuevoIncHistorico = ToInt(prodInc);
        espejo.HuevoLimpioHistorico = ToInt(pLimpio);
        espejo.HuevoTratadoHistorico = ToInt(pTrat);
        espejo.HuevoSucioHistorico = ToInt(pSucio);
        espejo.HuevoDeformeHistorico = ToInt(pDef);
        espejo.HuevoBlancoHistorico = ToInt(pBlanco);
        espejo.HuevoDobleYemaHistorico = ToInt(pDy);
        espejo.HuevoPisoHistorico = ToInt(pPiso);
        espejo.HuevoPequenoHistorico = ToInt(pPeq);
        espejo.HuevoRotoHistorico = ToInt(pRoto);
        espejo.HuevoDesechoHistorico = ToInt(pDes);
        espejo.HuevoOtroHistorico = ToInt(pOtro);

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

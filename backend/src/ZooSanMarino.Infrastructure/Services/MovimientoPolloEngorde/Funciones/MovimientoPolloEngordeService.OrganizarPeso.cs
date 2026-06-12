// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.OrganizarPeso.cs
// Organizar/recalcular peso prorrateado por lote en despachos (backfill de históricos G4/G5).
//
// Poblaciones encontradas en la auditoría de BD (2026-06-11):
//   * CLON GLOBAL: todas las líneas del despacho comparten el MISMO peso_bruto/peso_tara (el peso
//     del camión clonado). El individual correcto es el prorrateo por aves. Sub-casos:
//       - suma de netos ≈ bruto−tara  ⇒ ya prorrateado: solo se normalizan los *_global.
//       - neto nulo o cada línea con el global completo ⇒ clon SIN prorratear (sobreconteo n×):
//         se re-prorratea con el algoritmo de la creación (idempotente).
//       - netos prorrateados contra OTRO global ≠ bruto−tara ⇒ datos contradictorios:
//         RevisionManual, sin tocar (un humano decide cuál peso es el verdadero).
//   * PESO POR LÍNEA: cada línea tiene su PROPIO bruto/tara (pesaje por galpón/viaje). El
//     individual correcto es SIEMPRE su propio bruto−tara. Incluye las líneas que la versión
//     ANTERIOR de esta herramienta corrompió (prorrateó despachos de pesaje propio tomando el
//     peso de la primera línea como "global") ⇒ aquí se RESTAURA peso_neto = bruto−tara.
//     Los *_global del grupo se corrigen a la SUMA de las líneas (antes guardaban el de una línea).
//   * Líneas sin bruto o sin tara dentro de un grupo de pesaje propio ⇒ no se tocan (omitidas).
//   * HUÉRFANOS sospechosos (sin factura ni número, misma granja+fecha+bruto+placa) ⇒
//     RevisionManual, sin tocar.
//
// Agrupación: factura_id (nuevos) → numero_despacho+granja (legacy; SIN fecha, porque hay
// despachos reales cuyas líneas cruzan fechas — el clon se confirma por identidad de pesos,
// no por la fecha) → resto, línea a línea.
//
// NOTA: para corregir datos ya marcados con PesoNetoGlobal por la versión anterior se debe
// invocar con ReprocesarTodo=true (el modo pendiente solo toma grupos con PesoNetoGlobal nulo).
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.MovimientoPolloEngordeDto>;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoPolloEngordeService
{
    private const double PesoEpsilon = 0.001;
    private const double NetoTolerancia = 0.01;

    /// <inheritdoc />
    public async Task<OrganizarPesoResponse> OrganizarPesoAsync(OrganizarPesoRequest request)
    {
        var companyId = _currentUser.CompanyId;

        // Todas las ventas con peso registrado del scope. No se filtra por PesoNetoGlobal aquí:
        // un grupo (factura/despacho) debe procesarse COMPLETO aunque solo una línea esté pendiente.
        var query = _ctx.MovimientoPolloEngorde
            .Where(m =>
                m.CompanyId == companyId
                && m.DeletedAt == null
                && m.Estado != "Cancelado"
                && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                && (m.PesoBruto != null || m.PesoTara != null));

        if (request.GranjaId.HasValue)
        {
            var gId = request.GranjaId.Value;
            query = query.Where(m => m.GranjaOrigenId == gId);
        }

        var movimientos = await query
            .OrderBy(m => m.NumeroDespacho ?? "")
            .ThenBy(m => m.Id)
            .ToListAsync();

        var grupos = movimientos
            .GroupBy(m =>
                m.FacturaId.HasValue
                    ? $"F:{m.FacturaId.Value}"
                    : !string.IsNullOrWhiteSpace(m.NumeroDespacho)
                        ? $"D:{m.NumeroDespacho!.Trim()}|G:{m.GranjaOrigenId}"
                        : $"S:{m.Id}")
            .ToList();

        // Heurística de huérfanos multi-lote (sin factura ni número): misma granja + fecha + peso
        // bruto + placa ⇒ probablemente un mismo camión clonado. Solo se REPORTAN (RevisionManual):
        // el agrupador no es confiable y un falso positivo pisaría pesos correctos.
        var huerfanosSospechosos = movimientos
            .Where(m => !m.FacturaId.HasValue && string.IsNullOrWhiteSpace(m.NumeroDespacho) && m.PesoBruto.HasValue)
            .GroupBy(m => (m.GranjaOrigenId, Fecha: m.FechaMovimiento.Date, m.PesoBruto, m.Placa))
            .Where(g => g.Count() > 1)
            .ToList();
        var idsRevisionManual = huerfanosSospechosos.SelectMany(g => g.Select(m => m.Id)).ToHashSet();

        if (!request.ReprocesarTodo)
            grupos = grupos.Where(g => g.Any(m => m.PesoNetoGlobal == null)).ToList();

        if (grupos.Count == 0 && huerfanosSospechosos.Count == 0)
        {
            return new OrganizarPesoResponse
            {
                DryRun = request.DryRun,
                Mensaje = request.ReprocesarTodo
                    ? "No hay ventas con peso registrado en el scope indicado."
                    : "No hay ventas pendientes de organizar (todas ya tienen PesoNetoGlobal asignado). Use ReprocesarTodo=true para forzar.",
                DespachosProcesados = 0,
                MovimientosActualizados = 0,
                MovimientosOmitidos = 0
            };
        }

        var despachos = new List<OrganizarPesoDespachoDetalle>();
        var revisionManual = new List<OrganizarPesoDespachoDetalle>();
        var totalActualizados = 0;
        var totalOmitidos = 0;

        foreach (var grupo in grupos)
        {
            // Huérfanos sospechosos de multi-lote: se excluyen del auto-fix (irían con el global).
            var items = grupo.Where(m => !idsRevisionManual.Contains(m.Id)).ToList();
            if (items.Count == 0) continue;

            var conPesoPropio = items.Where(m => m.PesoBruto.HasValue && m.PesoTara.HasValue).ToList();
            var esClonGlobal = items.Count > 1
                && conPesoPropio.Count == items.Count
                && items.All(m => Math.Abs(m.PesoBruto!.Value - items[0].PesoBruto!.Value) < PesoEpsilon)
                && items.All(m => Math.Abs(m.PesoTara!.Value - items[0].PesoTara!.Value) < PesoEpsilon);

            if (esClonGlobal)
            {
                var procesado = ProcesarGrupoClon(items, request.DryRun, despachos, revisionManual);
                if (procesado) totalActualizados += items.Count;
                else totalOmitidos += items.Count;
            }
            else
            {
                // Pesaje propio por línea (incluye grupos de una sola línea): el individual correcto
                // es SIEMPRE el propio bruto−tara; el global del grupo es la suma de las líneas.
                if (conPesoPropio.Count == 0)
                {
                    totalOmitidos += items.Count;
                    continue;
                }
                ProcesarGrupoPesoPropio(conPesoPropio, request.DryRun, despachos);
                totalActualizados += conPesoPropio.Count;
                totalOmitidos += items.Count - conPesoPropio.Count;
            }
        }

        foreach (var g in huerfanosSospechosos)
        {
            var items = g.ToList();
            var kg = items.Sum(NetoEfectivo);
            revisionManual.Add(DetalleGrupo(items, items.Sum(m => m.TotalAves), kg, kg));
        }

        if (!request.DryRun && totalActualizados > 0)
            await _ctx.SaveChangesAsync();

        var modo = request.DryRun ? "Simulación" : "Aplicado";
        var notaRevision = revisionManual.Count > 0
            ? $" {revisionManual.Count} grupo(s) requieren revisión manual (pesos contradictorios o despacho multi-lote sin identificador); no se tocaron."
            : "";
        return new OrganizarPesoResponse
        {
            DryRun = request.DryRun,
            DespachosProcesados = despachos.Count,
            MovimientosActualizados = totalActualizados,
            MovimientosOmitidos = totalOmitidos,
            Mensaje = $"{modo}: {despachos.Count} despacho(s), {totalActualizados} movimiento(s) {(request.DryRun ? "a procesar" : "actualizados")}.{notaRevision}",
            Despachos = despachos,
            RevisionManual = revisionManual
        };
    }

    /// <summary>Grupo con el peso del camión clonado en todas las líneas. Devuelve false si quedó en revisión manual.</summary>
    private bool ProcesarGrupoClon(
        List<MovimientoPolloEngorde> items, bool dryRun,
        List<OrganizarPesoDespachoDetalle> despachos, List<OrganizarPesoDespachoDetalle> revisionManual)
    {
        var pesoBrutoGlobal = items[0].PesoBruto!.Value;
        var pesoTaraGlobal = items[0].PesoTara!.Value;
        if (pesoBrutoGlobal < pesoTaraGlobal)
            pesoTaraGlobal = pesoBrutoGlobal; // evitar neto negativo en datos corruptos; registrar sin error.
        var pesoNetoGlobal = pesoBrutoGlobal - pesoTaraGlobal;

        var totalAves = items.Sum(m => m.TotalAves);
        var kgAntes = items.Sum(NetoEfectivo);

        MovimientoPolloEngordeCalculos.PesoLineaProrrateado[]? individual = null;
        if (Math.Abs(kgAntes - pesoNetoGlobal) <= NetoTolerancia)
        {
            // Ya prorrateado correcto: solo normalizar los *_global (individual intacto).
        }
        else if (totalAves > 0 &&
                 items.Any(m => m.PesoNeto == null || Math.Abs(m.PesoNeto.Value - pesoNetoGlobal) < NetoTolerancia))
        {
            // Clon sin prorratear (neto nulo o cada línea con el global completo ⇒ sobreconteo n×).
            individual = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(
                pesoBrutoGlobal, pesoTaraGlobal, items.Select(m => m.TotalAves).ToList());
        }
        else
        {
            // Netos prorrateados contra OTRO global ≠ bruto−tara (o sin aves): contradicción.
            revisionManual.Add(DetalleGrupo(items, totalAves, kgAntes, kgAntes,
                pesoBrutoGlobal, pesoTaraGlobal, pesoNetoGlobal));
            return false;
        }

        var kgDespues = individual != null ? individual.Sum(p => p.Neto ?? 0d) : kgAntes;
        despachos.Add(DetalleGrupo(items, totalAves, kgAntes, kgDespues,
            pesoBrutoGlobal, pesoTaraGlobal, pesoNetoGlobal,
            totalAves > 0 ? pesoNetoGlobal / totalAves : null));

        if (!dryRun)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var m = items[i];
                m.PesoBrutoGlobal = pesoBrutoGlobal > 0 ? pesoBrutoGlobal : null;
                m.PesoTaraGlobal = pesoTaraGlobal > 0 ? pesoTaraGlobal : null;
                m.PesoNetoGlobal = pesoNetoGlobal;
                if (individual != null)
                {
                    m.PesoBrutoReal = individual[i].Bruto;
                    m.PesoTaraReal = individual[i].Tara;
                    m.PesoNeto = individual[i].Neto;
                    m.PromedioPesoAve = individual[i].Promedio;
                }
                else if (!m.PromedioPesoAve.HasValue && m.PesoNeto.HasValue && m.TotalAves > 0)
                {
                    m.PromedioPesoAve = m.PesoNeto.Value / m.TotalAves;
                }
                m.UpdatedByUserId = _currentUser.UserId;
                m.UpdatedAt = DateTime.UtcNow;
            }
        }
        return true;
    }

    /// <summary>Grupo con pesaje propio por línea: restaura neto = bruto−tara y global = suma del grupo.</summary>
    private void ProcesarGrupoPesoPropio(
        List<MovimientoPolloEngorde> lineas, bool dryRun, List<OrganizarPesoDespachoDetalle> despachos)
    {
        var pesoBrutoGlobal = lineas.Sum(m => m.PesoBruto!.Value);
        var pesoTaraGlobal = lineas.Sum(m => m.PesoTara!.Value);
        if (pesoBrutoGlobal < pesoTaraGlobal)
            pesoTaraGlobal = pesoBrutoGlobal;
        var pesoNetoGlobal = pesoBrutoGlobal - pesoTaraGlobal;

        var totalAves = lineas.Sum(m => m.TotalAves);
        var kgAntes = lineas.Sum(NetoEfectivo);
        var kgDespues = lineas.Sum(m => m.PesoBruto!.Value - m.PesoTara!.Value);

        despachos.Add(DetalleGrupo(lineas, totalAves, kgAntes, kgDespues,
            pesoBrutoGlobal, pesoTaraGlobal, pesoNetoGlobal,
            totalAves > 0 ? pesoNetoGlobal / totalAves : null));

        if (dryRun) return;

        foreach (var m in lineas)
        {
            var neto = m.PesoBruto!.Value - m.PesoTara!.Value;
            m.PesoBrutoGlobal = pesoBrutoGlobal > 0 ? pesoBrutoGlobal : null;
            m.PesoTaraGlobal = pesoTaraGlobal > 0 ? pesoTaraGlobal : null;
            m.PesoNetoGlobal = pesoNetoGlobal;
            m.PesoBrutoReal = m.PesoBruto;
            m.PesoTaraReal = m.PesoTara;
            m.PesoNeto = neto;
            m.PromedioPesoAve = m.TotalAves > 0 ? neto / m.TotalAves : null;
            m.UpdatedByUserId = _currentUser.UserId;
            m.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Neto individual vigente de una línea (mismo fallback que la fn de liquidación).</summary>
    private static double NetoEfectivo(MovimientoPolloEngorde m) =>
        m.PesoNeto ?? ((m.PesoBruto.HasValue && m.PesoTara.HasValue) ? m.PesoBruto.Value - m.PesoTara.Value : 0d);

    private static OrganizarPesoDespachoDetalle DetalleGrupo(
        List<MovimientoPolloEngorde> items, int totalAves, double kgAntes, double kgDespues,
        double? brutoGlobal = null, double? taraGlobal = null, double? netoGlobal = null, double? pesoPorAve = null)
        => new()
        {
            NumeroDespacho = items[0].NumeroDespacho,
            FacturaId = items[0].FacturaId,
            CantidadMovimientos = items.Count,
            TotalAves = totalAves,
            PesoBrutoGlobal = brutoGlobal is > 0 ? brutoGlobal : null,
            PesoTaraGlobal = taraGlobal is > 0 ? taraGlobal : null,
            PesoNetoGlobal = netoGlobal,
            PesoPorAve = pesoPorAve is > 0 ? pesoPorAve : null,
            KgAntes = kgAntes,
            KgDespues = kgDespues,
            MovimientoIds = items.Select(m => m.Id).ToList()
        };
}

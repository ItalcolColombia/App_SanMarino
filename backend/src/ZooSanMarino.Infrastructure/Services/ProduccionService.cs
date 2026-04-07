// src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionService : IProduccionService
{
    private const string TipoProduccion = "produccion";

    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ISeguimientoDiarioService _seguimientoDiarioService;
    private readonly ILoteService _loteService;

    /// <summary>
    /// Seguimiento diario postura (producción) no aplica consumo/devolución sobre inventario-gestion.
    /// El inventario nuevo (inventario-gestion / item_inventario_ecuador) es exclusivo del módulo Seguimiento diario pollo de engorde.
    /// </summary>
    public ProduccionService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        ISeguimientoDiarioService seguimientoDiarioService,
        ILoteService loteService)
    {
        _context = context;
        _currentUser = currentUser;
        _seguimientoDiarioService = seguimientoDiarioService;
        _loteService = loteService;
    }

    /// <summary>
    /// True solo si el lote tiene registro inicial de producción con datos llenos (tabla unificada lotes).
    /// No basta con Fase = Produccion; debe tener campos de producción llenos (HembrasInicialesProd o FechaInicioProduccion).
    /// 1) Lote hijo con LotePadreId = loteId, Fase = Produccion y datos llenos, o
    /// 2) El mismo lote con Fase = Produccion y datos llenos.
    /// </summary>
    public async Task<bool> ExisteProduccionLoteAsync(int loteId)
    {
        try
        {
            var companyId = _currentUser.CompanyId;
            // Caso 1: lote hijo en fase Producción con datos de registro inicial
            var tieneHijoProd = await _context.Lotes.AsNoTracking()
                .AnyAsync(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null && l.CompanyId == companyId
                    && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null));
            if (tieneHijoProd) return true;
            // Caso 2: mismo lote en producción con datos de registro inicial
            var mismoLoteEnProd = await _context.Lotes.AsNoTracking()
                .AnyAsync(l => l.LoteId == loteId && l.Fase == "Produccion" && l.DeletedAt == null && l.CompanyId == companyId
                    && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null));
            return mismoLoteEnProd;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking production lote existence: {ex.Message}");
            return false;
        }
    }

    public async Task<int> CrearProduccionLoteAsync(CrearProduccionLoteRequest request)
    {
        // Validar que el lote existe y pertenece a la empresa del usuario
        var lote = await _context.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == request.LoteId && l.CompanyId == _currentUser.CompanyId);

        if (lote == null)
        {
            throw new ArgumentException("El lote especificado no existe o no pertenece a su empresa.");
        }

        // Validar que no existe ya un registro inicial para este lote
        var existe = await ExisteProduccionLoteAsync(request.LoteId);
        if (existe)
        {
            throw new InvalidOperationException("Ya existe un registro inicial de producción para este lote.");
        }

        // Validar que la fecha no sea en el futuro
        if (request.FechaInicio.Date > DateTime.Today)
        {
            throw new ArgumentException("La fecha de inicio no puede ser en el futuro.");
        }

        // Cerrar etapa Levante: registrar con cuántas aves termina (saldos actuales)
        var resumenLevante = await _loteService.GetMortalidadResumenAsync(request.LoteId);
        var etapaLevante = await _context.LoteEtapaLevante
            .FirstOrDefaultAsync(el => el.LoteId == request.LoteId);
        if (etapaLevante != null)
        {
            etapaLevante.FechaFin = request.FechaInicio.ToUniversalTime();
            etapaLevante.AvesFinHembras = resumenLevante?.SaldoHembras ?? request.AvesInicialesH;
            etapaLevante.AvesFinMachos = resumenLevante?.SaldoMachos ?? request.AvesInicialesM;
            etapaLevante.UpdatedAt = DateTime.UtcNow;
        }

        // Crear lote hijo en fase Producción (Opción B: unificado en lotes).
        // Copiar Raza y AnoTablaGenetica del padre para que indicadores y guía genética tengan datos.
        var loteProd = new Lote
        {
            LoteNombre = (lote.LoteNombre ?? "").Trim() + " - Prod",
            GranjaId = lote.GranjaId,
            NucleoId = lote.NucleoId ?? "",
            GalponId = lote.GalponId,
            Fase = "Produccion",
            LotePadreId = request.LoteId,
            FechaInicioProduccion = request.FechaInicio,
            HembrasInicialesProd = request.AvesInicialesH,
            MachosInicialesProd = request.AvesInicialesM >= 0 ? request.AvesInicialesM : 0,
            HuevosIniciales = request.HuevosIniciales,
            TipoNido = request.TipoNido,
            NucleoP = request.NucleoP,
            CicloProduccion = request.Ciclo,
            CompanyId = lote.CompanyId,
            Raza = lote.Raza,
            AnoTablaGenetica = lote.AnoTablaGenetica,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Lotes.Add(loteProd);
        await _context.SaveChangesAsync();

        return loteProd.LoteId.GetValueOrDefault();
    }

    /// <summary>
    /// Obtiene el lote en fase Producción para el lote dado (tabla unificada lotes).
    /// 1) Si existe hijo con Fase = Produccion, devuelve el hijo (Id = LoteId del hijo).
    /// 2) Si el mismo lote tiene Fase = Produccion y campos de producción llenos, devuelve ese lote (Id = loteId).
    /// </summary>
    public async Task<ProduccionLoteDetalleDto?> ObtenerProduccionLoteAsync(int loteId)
    {
        var companyId = _currentUser.CompanyId;

        // 1) Buscar lote hijo en fase Producción con datos de registro inicial
        var hijo = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null && l.CompanyId == companyId
                && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null))
            .OrderBy(l => l.LoteId)
            .Select(l => new ProduccionLoteDetalleDto(
                l.LoteId ?? 0,
                loteId,
                l.FechaInicioProduccion ?? DateTime.MinValue,
                l.HembrasInicialesProd ?? 0,
                l.MachosInicialesProd ?? 0,
                l.HuevosIniciales ?? 0,
                l.TipoNido ?? "Manual",
                l.CicloProduccion ?? "normal",
                l.CreatedAt,
                l.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        if (hijo != null) return hijo;

        // 2) Mismo lote en producción (creado directo, sin hijo)
        var mismo = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == companyId && l.DeletedAt == null
                && l.Fase == "Produccion"
                && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null))
            .Select(l => new ProduccionLoteDetalleDto(
                l.LoteId ?? 0,
                loteId,
                l.FechaInicioProduccion ?? DateTime.MinValue,
                l.HembrasInicialesProd ?? 0,
                l.MachosInicialesProd ?? 0,
                l.HuevosIniciales ?? 0,
                l.TipoNido ?? "Manual",
                l.CicloProduccion ?? "normal",
                l.CreatedAt,
                l.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        return mismo;
    }

    public async Task<int> CrearSeguimientoAsync(CrearSeguimientoRequest request)
    {
        if (!request.LotePosturaProduccionId.HasValue && !request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Debe especificar ProduccionLoteId o LotePosturaProduccionId.");
        if (request.LotePosturaProduccionId.HasValue && request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Especifique solo ProduccionLoteId o LotePosturaProduccionId, no ambos.");

        string loteId;
        int? lotePosturaProduccionId = request.LotePosturaProduccionId;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = lpp.LoteId?.ToString() ?? lpp.LotePosturaProduccionId?.ToString() ?? lpp.LoteNombre ?? lotePosturaProduccionId.Value.ToString();

            var existeSeguimientoLpp = await _context.SeguimientoDiario.AsNoTracking()
                .AnyAsync(s => s.TipoSeguimiento == TipoProduccion && s.LotePosturaProduccionId == lotePosturaProduccionId
                    && s.Fecha.Date == request.FechaRegistro.Date);
            if (existeSeguimientoLpp)
                throw new InvalidOperationException("Ya existe un seguimiento para esta fecha y lote.");
        }
        else
        {
            var loteProd = await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion" && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId?.ToString() ?? request.ProduccionLoteId!.Value.ToString();

            var existeSeguimiento = await _context.SeguimientoDiario.AsNoTracking()
                .AnyAsync(s => s.TipoSeguimiento == TipoProduccion && s.LoteId == loteId && s.Fecha.Date == request.FechaRegistro.Date);
            if (existeSeguimiento)
                throw new InvalidOperationException("Ya existe un seguimiento para esta fecha.");
        }

        // Validar que la fecha no sea en el futuro
        if (request.FechaRegistro.Date > DateTime.Today)
        {
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");
        }

        decimal consumoKgH;
        decimal consumoKgM;
        JsonDocument? metadata;
        JsonDocument? itemsAdicionales = null;
        var tipoAlimento = request.TipoAlimento ?? string.Empty;

        var useItems = (request.ItemsHembras != null && request.ItemsHembras.Count > 0) ||
                       (request.ItemsMachos != null && request.ItemsMachos.Count > 0);

        if (useItems)
        {
            var (alimentosHembras, otrosHembras) = SepararAlimentosYOtrosItems(request.ItemsHembras);
            var (alimentosMachos, otrosMachos) = SepararAlimentosYOtrosItems(request.ItemsMachos);
            consumoKgH = (decimal)CalcularConsumoTotalAlimentos(alimentosHembras);
            consumoKgM = (decimal)CalcularConsumoTotalAlimentos(alimentosMachos);
            if (string.IsNullOrWhiteSpace(tipoAlimento))
                tipoAlimento = ConstruirTipoAlimentoString(request.ItemsHembras, request.ItemsMachos);
            metadata = BuildMetadataFromItems(request.ItemsHembras, request.ItemsMachos,
                request.ConsumoH, request.UnidadConsumoH, request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos);
            itemsAdicionales = BuildItemsAdicionales(otrosHembras, otrosMachos);
        }
        else
        {
            consumoKgH = 0;
            if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
            {
                var unidadH = (request.UnidadConsumoH ?? "kg").ToLower().Trim();
                consumoKgH = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                    ? (decimal)(request.ConsumoH.Value / 1000.0)
                    : (decimal)request.ConsumoH.Value;
            }
            consumoKgM = 0;
            if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
            {
                var unidadM = (request.UnidadConsumoM ?? "kg").ToLower().Trim();
                consumoKgM = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                    ? (decimal)(request.ConsumoM.Value / 1000.0)
                    : (decimal)request.ConsumoM.Value;
            }
            metadata = BuildMetadata(
                request.ConsumoH, request.UnidadConsumoH,
                request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos
            );
        }

        var createdByUserId = request.CreatedByUserId
            ?? _currentUser.UserGuid?.ToString()
            ?? _currentUser.UserId.ToString();

        var dto = new CreateSeguimientoDiarioDto(
            TipoSeguimiento: TipoProduccion,
            LoteId: loteId,
            LotePosturaLevanteId: null,
            LotePosturaProduccionId: lotePosturaProduccionId,
            ReproductoraId: null,
            Fecha: request.FechaRegistro,
            MortalidadHembras: request.MortalidadH,
            MortalidadMachos: request.MortalidadM,
            SelH: request.SelH,
            SelM: request.SelM,
            ErrorSexajeHembras: request.ErrorSexajeHembras,
            ErrorSexajeMachos: request.ErrorSexajeMachos,
            ConsumoKgHembras: consumoKgH,
            ConsumoKgMachos: consumoKgM,
            TipoAlimento: tipoAlimento,
            Observaciones: request.Observaciones,
            Ciclo: request.Ciclo,
            PesoPromHembras: request.PesoH.HasValue ? (double)request.PesoH.Value : null,
            PesoPromMachos: request.PesoM.HasValue ? (double)request.PesoM.Value : null,
            UniformidadHembras: request.UniformidadHembras ?? (request.Uniformidad.HasValue ? (double)request.Uniformidad.Value : null),
            UniformidadMachos: request.UniformidadMachos ?? (request.Uniformidad.HasValue ? (double)request.Uniformidad.Value : null),
            CvHembras: request.CvHembras ?? (request.CoeficienteVariacion.HasValue ? (double)request.CoeficienteVariacion.Value : null),
            CvMachos: request.CvMachos ?? (request.CoeficienteVariacion.HasValue ? (double)request.CoeficienteVariacion.Value : null),
            ConsumoAguaDiario: request.ConsumoAguaDiario,
            ConsumoAguaPh: request.ConsumoAguaPh,
            ConsumoAguaOrp: request.ConsumoAguaOrp,
            ConsumoAguaTemperatura: request.ConsumoAguaTemperatura,
            Metadata: metadata,
            ItemsAdicionales: itemsAdicionales,
            PesoInicial: null,
            PesoFinal: null,
            KcalAlH: null,
            ProtAlH: null,
            KcalAveH: null,
            ProtAveH: null,
            HuevoTot: request.HuevosTotales,
            HuevoInc: request.HuevosIncubables,
            HuevoLimpio: request.HuevoLimpio,
            HuevoTratado: request.HuevoTratado,
            HuevoSucio: request.HuevoSucio,
            HuevoDeforme: request.HuevoDeforme,
            HuevoBlanco: request.HuevoBlanco,
            HuevoDobleYema: request.HuevoDobleYema,
            HuevoPiso: request.HuevoPiso,
            HuevoPequeno: request.HuevoPequeno,
            HuevoRoto: request.HuevoRoto,
            HuevoDesecho: request.HuevoDesecho,
            HuevoOtro: request.HuevoOtro,
            PesoHuevo: (double)request.PesoHuevo,
            Etapa: request.Etapa,
            PesoH: request.PesoH,
            PesoM: request.PesoM,
            Uniformidad: request.Uniformidad,
            CoeficienteVariacion: request.CoeficienteVariacion,
            ObservacionesPesaje: request.ObservacionesPesaje,
            CreatedByUserId: createdByUserId
        );

        var created = await _seguimientoDiarioService.CreateAsync(dto);
        if (created == null) throw new InvalidOperationException("No se pudo crear el seguimiento.");
        return (int)created.Id;
    }

    public async Task ActualizarSeguimientoAsync(int id, CrearSeguimientoRequest request)
    {
        if (!request.LotePosturaProduccionId.HasValue && !request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Debe especificar ProduccionLoteId o LotePosturaProduccionId.");
        if (request.LotePosturaProduccionId.HasValue && request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Especifique solo ProduccionLoteId o LotePosturaProduccionId, no ambos.");

        string loteId;
        int? lotePosturaProduccionId = request.LotePosturaProduccionId;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = lpp.LoteId?.ToString() ?? lpp.LotePosturaProduccionId?.ToString() ?? lpp.LoteNombre ?? lotePosturaProduccionId.Value.ToString();
        }
        else
        {
            var loteProd = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion" && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId?.ToString() ?? request.ProduccionLoteId!.Value.ToString();
        }

        if (request.FechaRegistro.Date > DateTime.Today)
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");

        decimal consumoKgH;
        decimal consumoKgM;
        JsonDocument? metadata;
        JsonDocument? itemsAdicionales = null;
        var tipoAlimento = request.TipoAlimento ?? string.Empty;

        var useItems = (request.ItemsHembras != null && request.ItemsHembras.Count > 0) ||
                       (request.ItemsMachos != null && request.ItemsMachos.Count > 0);

        if (useItems)
        {
            var (alimentosHembras, otrosHembras) = SepararAlimentosYOtrosItems(request.ItemsHembras);
            var (alimentosMachos, otrosMachos) = SepararAlimentosYOtrosItems(request.ItemsMachos);
            consumoKgH = (decimal)CalcularConsumoTotalAlimentos(alimentosHembras);
            consumoKgM = (decimal)CalcularConsumoTotalAlimentos(alimentosMachos);
            if (string.IsNullOrWhiteSpace(tipoAlimento))
                tipoAlimento = ConstruirTipoAlimentoString(request.ItemsHembras, request.ItemsMachos);
            metadata = BuildMetadataFromItems(request.ItemsHembras, request.ItemsMachos,
                request.ConsumoH, request.UnidadConsumoH, request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos);
            itemsAdicionales = BuildItemsAdicionales(otrosHembras, otrosMachos);
        }
        else
        {
            consumoKgH = 0;
            if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
            {
                var unidadH = (request.UnidadConsumoH ?? "kg").ToLowerInvariant().Trim();
                consumoKgH = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                    ? (decimal)(request.ConsumoH.Value / 1000.0)
                    : (decimal)request.ConsumoH.Value;
            }
            consumoKgM = 0;
            if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
            {
                var unidadM = (request.UnidadConsumoM ?? "kg").ToLowerInvariant().Trim();
                consumoKgM = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                    ? (decimal)(request.ConsumoM.Value / 1000.0)
                    : (decimal)request.ConsumoM.Value;
            }
            metadata = BuildMetadata(
                request.ConsumoH, request.UnidadConsumoH,
                request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos
            );
        }

        var updateDto = new UpdateSeguimientoDiarioDto(
            Id: id,
            TipoSeguimiento: TipoProduccion,
            LoteId: loteId,
            LotePosturaLevanteId: null,
            LotePosturaProduccionId: lotePosturaProduccionId,
            ReproductoraId: null,
            Fecha: request.FechaRegistro,
            MortalidadHembras: request.MortalidadH,
            MortalidadMachos: request.MortalidadM,
            SelH: request.SelH,
            SelM: request.SelM,
            ErrorSexajeHembras: request.ErrorSexajeHembras,
            ErrorSexajeMachos: request.ErrorSexajeMachos,
            ConsumoKgHembras: consumoKgH,
            ConsumoKgMachos: consumoKgM,
            TipoAlimento: tipoAlimento,
            Observaciones: request.Observaciones,
            Ciclo: request.Ciclo,
            PesoPromHembras: request.PesoH.HasValue ? (double)request.PesoH.Value : null,
            PesoPromMachos: request.PesoM.HasValue ? (double)request.PesoM.Value : null,
            UniformidadHembras: request.UniformidadHembras ?? (request.Uniformidad.HasValue ? (double)request.Uniformidad.Value : null),
            UniformidadMachos: request.UniformidadMachos ?? (request.Uniformidad.HasValue ? (double)request.Uniformidad.Value : null),
            CvHembras: request.CvHembras ?? (request.CoeficienteVariacion.HasValue ? (double)request.CoeficienteVariacion.Value : null),
            CvMachos: request.CvMachos ?? (request.CoeficienteVariacion.HasValue ? (double)request.CoeficienteVariacion.Value : null),
            ConsumoAguaDiario: request.ConsumoAguaDiario,
            ConsumoAguaPh: request.ConsumoAguaPh,
            ConsumoAguaOrp: request.ConsumoAguaOrp,
            ConsumoAguaTemperatura: request.ConsumoAguaTemperatura,
            Metadata: metadata,
            ItemsAdicionales: itemsAdicionales,
            PesoInicial: null,
            PesoFinal: null,
            KcalAlH: null,
            ProtAlH: null,
            KcalAveH: null,
            ProtAveH: null,
            HuevoTot: request.HuevosTotales,
            HuevoInc: request.HuevosIncubables,
            HuevoLimpio: request.HuevoLimpio,
            HuevoTratado: request.HuevoTratado,
            HuevoSucio: request.HuevoSucio,
            HuevoDeforme: request.HuevoDeforme,
            HuevoBlanco: request.HuevoBlanco,
            HuevoDobleYema: request.HuevoDobleYema,
            HuevoPiso: request.HuevoPiso,
            HuevoPequeno: request.HuevoPequeno,
            HuevoRoto: request.HuevoRoto,
            HuevoDesecho: request.HuevoDesecho,
            HuevoOtro: request.HuevoOtro,
            PesoHuevo: (double)request.PesoHuevo,
            Etapa: request.Etapa,
            PesoH: request.PesoH,
            PesoM: request.PesoM,
            Uniformidad: request.Uniformidad,
            CoeficienteVariacion: request.CoeficienteVariacion,
            ObservacionesPesaje: request.ObservacionesPesaje
        );

        var updated = await _seguimientoDiarioService.UpdateAsync(updateDto);
        if (updated == null)
            throw new InvalidOperationException("No se encontró el registro o no tiene permisos para actualizarlo.");
    }

    public async Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int? loteId, int? lotePosturaProduccionId, DateTime? desde, DateTime? hasta, int page, int size)
    {
        if (!lotePosturaProduccionId.HasValue && !loteId.HasValue)
            throw new ArgumentException("Debe especificar loteId o lotePosturaProduccionId.");

        SeguimientoDiarioFilterRequest filter;
        int produccionLoteId;

        if (lotePosturaProduccionId.HasValue)
        {
            filter = new SeguimientoDiarioFilterRequest
            {
                TipoSeguimiento = TipoProduccion,
                LotePosturaProduccionId = lotePosturaProduccionId.Value,
                FechaDesde = desde,
                FechaHasta = hasta,
                Page = page,
                PageSize = size,
                OrderBy = "Fecha",
                OrderAsc = false
            };
            produccionLoteId = 0;
        }
        else
        {
            var companyId = _currentUser.CompanyId;
            var lid = loteId!.Value;
            Lote? loteProd = await _context.Lotes.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.Fase == "Produccion" && l.LotePadreId == lid)
                .OrderBy(l => l.LoteId)
                .FirstOrDefaultAsync();
            if (loteProd == null)
                loteProd = await _context.Lotes.AsNoTracking()
                    .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.Fase == "Produccion" && l.LoteId == lid)
                    .FirstOrDefaultAsync();
            var loteIdStr = loteProd?.LoteId?.ToString() ?? lid.ToString();
            produccionLoteId = loteProd?.LoteId ?? lid;

            filter = new SeguimientoDiarioFilterRequest
            {
                TipoSeguimiento = TipoProduccion,
                LoteId = loteIdStr,
                FechaDesde = desde,
                FechaHasta = hasta,
                Page = page,
                PageSize = size,
                OrderBy = "Fecha",
                OrderAsc = false
            };
        }

        var paged = await _seguimientoDiarioService.GetFilteredAsync(filter);
        var items = paged.Items.Select(u => MapToSeguimientoItemDto(u, produccionLoteId, u.LotePosturaProduccionId)).ToList();
        return new ListaSeguimientoResponse(items, (int)paged.Total);
    }

    private static object? MetadataFromJsonDocument(System.Text.Json.JsonDocument? doc)
    {
        if (doc == null) return null;
        try
        {
            return JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    private static SeguimientoItemDto MapToSeguimientoItemDto(SeguimientoDiarioDto u, int produccionLoteId, int? lotePosturaProduccionId = null)
    {
        var consKgH = (decimal)(u.ConsumoKgHembras ?? 0);
        var consKgM = (decimal)(u.ConsumoKgMachos ?? 0);
        return new SeguimientoItemDto(
            (int)u.Id,
            produccionLoteId,
            u.Fecha,
            u.MortalidadHembras ?? 0,
            u.MortalidadMachos ?? 0,
            u.SelH ?? 0,
            u.SelM ?? 0,
            consKgH,
            consKgM,
            consKgH + consKgM,
            u.HuevoTot ?? 0,
            u.HuevoInc ?? 0,
            u.TipoAlimento ?? "",
            (decimal)(u.PesoHuevo ?? 0),
            u.Etapa ?? 0,
            u.Observaciones,
            u.CreatedAt,
            u.UpdatedAt,
            u.HuevoLimpio ?? 0,
            u.HuevoTratado ?? 0,
            u.HuevoSucio ?? 0,
            u.HuevoDeforme ?? 0,
            u.HuevoBlanco ?? 0,
            u.HuevoDobleYema ?? 0,
            u.HuevoPiso ?? 0,
            u.HuevoPequeno ?? 0,
            u.HuevoRoto ?? 0,
            u.HuevoDesecho ?? 0,
            u.HuevoOtro ?? 0,
            u.PesoH,
            u.PesoM,
            u.Uniformidad,
            u.CoeficienteVariacion,
            u.ObservacionesPesaje,
            u.ConsumoAguaDiario,
            u.ConsumoAguaPh,
            u.ConsumoAguaOrp,
            u.ConsumoAguaTemperatura,
            lotePosturaProduccionId ?? u.LotePosturaProduccionId,
            Metadata: MetadataFromJsonDocument(u.Metadata)
        );
    }

    public async Task<SeguimientoItemDto?> ObtenerSeguimientoPorIdAsync(int seguimientoId)
    {
        var u = await _seguimientoDiarioService.GetByIdAsync((long)seguimientoId);
        if (u == null || u.TipoSeguimiento != TipoProduccion)
            return null;
        // LoteId en seguimiento_diario (producción) es el id del lote en fase Producción (hijo)
        var loteProdId = int.TryParse(u.LoteId, out var id) ? id : 0;
        return MapToSeguimientoItemDto(u, loteProdId);
    }

    /// <summary>
    /// Elimina un seguimiento diario de producción (tabla unificada seguimiento_diario).
    /// El inventario nuevo (inventario-gestion) no se usa en este módulo; solo en Seguimiento diario pollo de engorde.
    /// </summary>
    public async Task<bool> EliminarSeguimientoAsync(int seguimientoId)
    {
        var u = await _seguimientoDiarioService.GetByIdAsync((long)seguimientoId);
        if (u == null || u.TipoSeguimiento != TipoProduccion)
            return false;
        return await _seguimientoDiarioService.DeleteAsync((long)seguimientoId);
    }

    /// <summary>
    /// Obtiene los lotes que tienen semana 26 o superior (calculada desde fechaEncaset)
    /// Solo muestra lotes que han alcanzado la semana 26 de producción
    /// </summary>
    public async Task<IEnumerable<LoteDtos.LoteDetailDto>> ObtenerLotesProduccionAsync()
    {
        var fechaHoy = DateTime.Today;
        
        // Calcular la fecha límite: para estar en semana 26, deben haber pasado al menos 26 semanas (182 días)
        // Semana 26 = días desde fechaEncaset >= 182 días
        // Por lo tanto: fechaEncaset <= fechaHoy - 182 días
        var diasSemana26 = 26 * 7; // 182 días
        var fechaLimiteSemana26 = fechaHoy.AddDays(-diasSemana26);

        // Un lote está en semana 26 o más si: fechaEncaset <= fechaLimiteSemana26
        // Esto significa que han pasado al menos 26 semanas desde fechaEncaset
        var lotes = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Where(l => 
                l.CompanyId == _currentUser.CompanyId && 
                l.DeletedAt == null &&
                l.FechaEncaset != null &&
                l.FechaEncaset <= fechaLimiteSemana26)
            .OrderBy(l => l.LoteId)
            .ToListAsync();

        // Proyectar a LoteDetailDto
        return lotes.Select(l => new LoteDtos.LoteDetailDto(
            l.LoteId ?? 0,
            l.LoteNombre,
            l.LotePosturaBaseId,
            l.GranjaId,
            l.NucleoId,
            l.GalponId,
            l.Regional,
            l.FechaEncaset,
            l.HembrasL,
            l.MachosL,
            l.PesoInicialH,
            l.PesoInicialM,
            l.UnifH,
            l.UnifM,
            l.MortCajaH,
            l.MortCajaM,
            l.Raza,
            l.AnoTablaGenetica,
            l.Linea,
            l.TipoLinea,
            l.CodigoGuiaGenetica,
            l.LineaGeneticaId,
            l.Tecnico,
            l.Mixtas,
            l.PesoMixto,
            l.AvesEncasetadas,
            l.EdadInicial,
            l.LoteErp,
            l.EstadoTraslado,
            l.LotePadreId,
            l.PaisId,
            l.PaisNombre,
            l.EmpresaNombre,
            l.CompanyId,
            l.CreatedByUserId,
            l.CreatedAt,
            l.UpdatedByUserId,
            l.UpdatedAt,
            // Relaciones
            l.Farm != null ? new FarmLiteDto(
                l.Farm.Id,
                l.Farm.Name,
                l.Farm.RegionalId,
                l.Farm.DepartamentoId,
                l.Farm.MunicipioId
            ) : throw new InvalidOperationException($"Lote {l.LoteId} no tiene Farm asociado"),
            l.Nucleo != null ? new NucleoLiteDto(
                l.Nucleo.NucleoId,
                l.Nucleo.NucleoNombre,
                l.Nucleo.GranjaId
            ) : null,
            l.Galpon != null ? new GalponLiteDto(
                l.Galpon.GalponId,
                l.Galpon.GalponNombre,
                l.Galpon.NucleoId,
                l.Galpon.GranjaId
            ) : null
        ));
    }
    
    /// <summary>
    /// Construye el objeto Metadata JSONB con los campos adicionales.
    /// </summary>
    private static System.Text.Json.JsonDocument? BuildMetadata(
        double? consumoHembras, string? unidadHembras, 
        double? consumoMachos, string? unidadMachos,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        
        // Consumo original con unidad
        if (consumoHembras.HasValue)
        {
            metadata["consumoOriginalHembras"] = consumoHembras.Value;
            metadata["unidadConsumoOriginalHembras"] = unidadHembras ?? "kg";
        }
        
        if (consumoMachos.HasValue)
        {
            metadata["consumoOriginalMachos"] = consumoMachos.Value;
            metadata["unidadConsumoOriginalMachos"] = unidadMachos ?? "kg";
        }
        
        // Tipo de ítem (alimento, medicamento, etc.)
        if (!string.IsNullOrWhiteSpace(tipoItemHembras))
        {
            metadata["tipoItemHembras"] = tipoItemHembras;
        }
        
        if (!string.IsNullOrWhiteSpace(tipoItemMachos))
        {
            metadata["tipoItemMachos"] = tipoItemMachos;
        }
        
        // IDs de alimentos seleccionados
        if (tipoAlimentoHembras.HasValue)
        {
            metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        }
        
        if (tipoAlimentoMachos.HasValue)
        {
            metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        }
        
        if (metadata.Count == 0) return null;
        
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }

    private static (List<ItemSeguimientoDto> alimentos, List<ItemSeguimientoDto> otrosItems) SepararAlimentosYOtrosItems(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0)
            return (new List<ItemSeguimientoDto>(), new List<ItemSeguimientoDto>());
        var alimentos = new List<ItemSeguimientoDto>();
        var otros = new List<ItemSeguimientoDto>();
        foreach (var item in items)
        {
            if (string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                alimentos.Add(item);
            else
                otros.Add(item);
        }
        return (alimentos, otros);
    }

    private static double CalcularConsumoTotalAlimentos(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0) return 0;
        double total = 0;
        foreach (var item in items)
        {
            if (!string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase)) continue;
            var unidad = item.Unidad?.ToLower().Trim() ?? "kg";
            var cantidadKg = item.Cantidad;
            if (unidad == "g" || unidad == "gramos" || unidad == "gramo")
                cantidadKg = item.Cantidad / 1000.0;
            total += cantidadKg;
        }
        return total;
    }

    private static JsonDocument? BuildItemsAdicionales(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var dict = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            dict["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            dict["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (dict.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private static string ConstruirTipoAlimentoString(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var parts = new List<string>();
        if (itemsHembras != null)
            foreach (var i in itemsHembras)
                if (string.Equals(i.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"H:{i.CatalogItemId}");
        if (itemsMachos != null)
            foreach (var i in itemsMachos)
                if (string.Equals(i.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"M:{i.CatalogItemId}");
        return parts.Count > 0 ? string.Join(" / ", parts) : string.Empty;
    }

    private static JsonDocument? BuildMetadataFromItems(
        List<ItemSeguimientoDto>? itemsHembras,
        List<ItemSeguimientoDto>? itemsMachos,
        double? consumoH, string? unidadH, double? consumoM, string? unidadM,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            metadata["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, itemInventarioEcuadorId = i.ItemInventarioEcuadorId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            metadata["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, itemInventarioEcuadorId = i.ItemInventarioEcuadorId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if ((itemsHembras == null || itemsHembras.Count == 0) && consumoH.HasValue) { metadata["consumoOriginalHembras"] = consumoH.Value; metadata["unidadConsumoOriginalHembras"] = unidadH ?? "kg"; }
        if ((itemsMachos == null || itemsMachos.Count == 0) && consumoM.HasValue) { metadata["consumoOriginalMachos"] = consumoM.Value; metadata["unidadConsumoOriginalMachos"] = unidadM ?? "kg"; }
        if (!string.IsNullOrWhiteSpace(tipoItemHembras)) metadata["tipoItemHembras"] = tipoItemHembras;
        if (!string.IsNullOrWhiteSpace(tipoItemMachos)) metadata["tipoItemMachos"] = tipoItemMachos;
        if (tipoAlimentoHembras.HasValue) metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        if (tipoAlimentoMachos.HasValue) metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        if (metadata.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }

    private static decimal ToKg(double cantidad, string unidad)
    {
        var u = (unidad ?? "kg").Trim().ToLowerInvariant();
        if (u == "g" || u == "gramos" || u == "gramo") return (decimal)(cantidad / 1000.0);
        return (decimal)cantidad;
    }
}

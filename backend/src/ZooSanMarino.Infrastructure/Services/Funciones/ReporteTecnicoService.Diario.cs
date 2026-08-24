// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoService.Diario.cs
// Reporte tecnico DIARIO: por sublote y consolidado, machos y hembras por separado, levante y produccion.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService
{
    public async Task<ReporteTecnicoCompletoDto> GenerarReporteDiarioSubloteAsync(
        int loteId, 
        DateTime? fechaInicio = null, 
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);

        if (lote == null)
            throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");

        var infoLote = MapearInformacionLote(lote);
        var sublote = ExtraerSublote(lote.LoteNombre);
        infoLote.Sublote = sublote;
        infoLote.Etapa = "LEVANTE"; // Forzar etapa a LEVANTE para reporte de levante

        // Para reporte de levante, siempre usar datos de levante y filtrar por semana (1-25)
        // Esto permite ver datos históricos de levante incluso si el lote está en producción
        var datosDiarios = await ObtenerDatosDiariosLevanteAsync(loteId, lote.FechaEncaset, fechaInicio, fechaFin, ct);
        
        // Filtrar solo semanas de levante (1-25)
        datosDiarios = datosDiarios.Where(d => d.EdadSemanas <= 25).ToList();

        var avesIniciales = (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);
        var datosSemanales = ConsolidarSemanales(datosDiarios, lote.FechaEncaset, avesIniciales);
        
        // Filtrar también las semanas consolidadas (solo semanas 1-25)
        datosSemanales = datosSemanales.Where(s => s.Semana <= 25).ToList();

        return new ReporteTecnicoCompletoDto
        {
            InformacionLote = infoLote,
            DatosDiarios = datosDiarios,
            DatosSemanales = datosSemanales,
            EsConsolidado = false,
            SublotesIncluidos = new List<string> { sublote ?? "Sin sublote" }
        };
    }

    public async Task<ReporteTecnicoCompletoDto> GenerarReporteDiarioConsolidadoAsync(
        string loteNombreBase,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        int? loteId = null,
        CancellationToken ct = default)
    {
        List<Lote> sublotes;
        
        // Si se proporciona loteId, usar lógica de lote padre
        if (loteId.HasValue)
        {
            var loteSeleccionado = await _ctx.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId.Value && 
                                         l.CompanyId == _currentUser.CompanyId &&
                                         l.DeletedAt == null, ct);
            
            if (loteSeleccionado == null)
                throw new InvalidOperationException($"Lote con ID {loteId.Value} no encontrado");
            
            // Si el lote seleccionado es un lote padre, traer todos sus hijos
            if (loteSeleccionado.LotePadreId == null)
            {
                // Es un lote padre, traer todos los lotes que tienen este como padre
                sublotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => l.LotePadreId == loteId.Value &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
                
                // Incluir también el lote padre
                sublotes.Insert(0, loteSeleccionado);
            }
            else
            {
                // Es un lote hijo, traer el padre y todos sus hermanos (incluyendo el seleccionado)
                var padreId = loteSeleccionado.LotePadreId.Value;
                sublotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => (l.LotePadreId == padreId || l.LoteId == padreId) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
            }
        }
        else
        {
            // Lógica antigua: buscar por nombre base (compatibilidad hacia atrás)
            sublotes = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LoteNombre.StartsWith(loteNombreBase) && 
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);
        }

        if (!sublotes.Any())
            throw new InvalidOperationException($"No se encontraron sublotes para el lote {loteNombreBase}");

        var todosDatosDiarios = new List<ReporteTecnicoDiarioDto>();
        var sublotesIncluidos = new List<string>();

        foreach (var sublote in sublotes)
        {
            var subloteNombre = ExtraerSublote(sublote.LoteNombre) ?? "Sin sublote";
            sublotesIncluidos.Add(subloteNombre);

            // Para reporte de levante, siempre usar datos de levante (semanas 1-25)
            var datosSublote = await ObtenerDatosDiariosLevanteAsync(sublote.LoteId ?? 0, sublote.FechaEncaset, fechaInicio, fechaFin, ct);
            
            // Filtrar solo semanas de levante (1-25)
            datosSublote = datosSublote.Where(d => d.EdadSemanas <= 25).ToList();

            todosDatosDiarios.AddRange(datosSublote);
        }

        // Consolidar por fecha (sumar datos de todos los sublotes para la misma fecha)
        var datosConsolidados = ConsolidarDatosDiarios(todosDatosDiarios);
        
        // Filtrar solo semanas de levante (1-25)
        datosConsolidados = datosConsolidados.Where(d => d.EdadSemanas <= 25).ToList();

        // Usar información del primer sublote como base
        var loteBase = sublotes.First();
        var infoLote = MapearInformacionLote(loteBase);
        infoLote.Sublote = null; // Consolidado no tiene sublote específico
        infoLote.Etapa = "LEVANTE"; // Forzar etapa a LEVANTE para reporte de levante

        var avesInicialesConsolidado = sublotes.Sum(s => (s.HembrasL ?? 0) + (s.MachosL ?? 0));
        var datosSemanales = ConsolidarSemanales(datosConsolidados, loteBase.FechaEncaset, avesInicialesConsolidado);
        
        // Filtrar también las semanas consolidadas (solo semanas 1-25)
        datosSemanales = datosSemanales.Where(s => s.Semana <= 25).ToList();

        return new ReporteTecnicoCompletoDto
        {
            InformacionLote = infoLote,
            DatosDiarios = datosConsolidados.OrderBy(d => d.Fecha).ToList(),
            DatosSemanales = datosSemanales,
            EsConsolidado = true,
            SublotesIncluidos = sublotesIncluidos.Distinct().ToList()
        };
    }

    private async Task<List<ReporteTecnicoDiarioDto>> ObtenerDatosDiariosLevanteAsync(
        int loteId,
        DateTime? fechaEncaset,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        // IMPORTANTE: Para calcular correctamente aves actuales y acumulados,
        // necesitamos TODOS los registros desde el inicio (tabla unificada seguimiento_diario, fase levante)
        var todosSeguimientos = await ObtenerSeguimientosLevanteUnificadoAsync(loteId, ct);

        // Filtrar por edad/semana: solo semanas 1-25 (levante)
        // Calcular edad para cada registro y filtrar
        if (fechaEncaset.HasValue)
        {
            todosSeguimientos = todosSeguimientos.Where(seg =>
            {
                var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.FechaRegistro);
                var edadSemanas = CalcularEdadSemanas(edadDias);
                return edadSemanas <= 25; // Solo levante (semanas 1-25)
            }).ToList();
        }

        // Aplicar filtros de fecha solo para los registros que se mostrarán
        var queryFiltrado = todosSeguimientos.AsQueryable();
        if (fechaInicio.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);

        if (fechaFin.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);

        var seguimientos = queryFiltrado.ToList();

        if (!fechaEncaset.HasValue)
            return new List<ReporteTecnicoDiarioDto>();

        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);

        if (lote == null)
            return new List<ReporteTecnicoDiarioDto>();

        var datosDiarios = new List<ReporteTecnicoDiarioDto>();
        var avesIniciales = (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);
        
        // Calcular valores acumulados desde el INICIO del lote (todos los registros)
        var mortalidadAcumuladaTotal = todosSeguimientos.Sum(s => s.MortalidadHembras + s.MortalidadMachos);
        var consumoAcumuladoTotal = todosSeguimientos.Sum(s => (decimal)s.ConsumoKgHembras + (decimal)(s.ConsumoKgMachos ?? 0));
        var errorSexajeAcumuladoTotal = todosSeguimientos.Sum(s => s.ErrorSexajeHembras + s.ErrorSexajeMachos);
        
        // Calcular descarte acumulado (incluyendo traslados)
        var descarteAcumuladoTotal = 0;
        foreach (var seg in todosSeguimientos)
        {
            var seleccionH = seg.SelH;
            var seleccionM = seg.SelM;
            var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);
            var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
            var trasladosAbsoluto = Math.Abs(traslados);
            descarteAcumuladoTotal += (int)(seleccionNormal + trasladosAbsoluto);
        }
        
        // Calcular aves actuales desde el inicio
        var avesActualesBase = avesIniciales;
        foreach (var seg in todosSeguimientos)
        {
            var mortalidadTotal = seg.MortalidadHembras + seg.MortalidadMachos;
            avesActualesBase -= mortalidadTotal;
            
            var seleccionH = seg.SelH;
            var seleccionM = seg.SelM;
            var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);
            var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
            var trasladosAbsoluto = Math.Abs(traslados);
            
            avesActualesBase -= seleccionNormal;
            avesActualesBase -= trasladosAbsoluto;
        }
        
        // Variables para acumular solo hasta la fecha actual del registro que se está procesando
        decimal? pesoAnterior = null;

        // Procesar todos los registros desde el inicio para calcular acumulados correctamente
        // pero solo mostrar los que están en el rango de fechas
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);

            // Calcular acumulados hasta esta fecha (incluyendo todos los registros anteriores)
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();

            var mortalidadTotal = seg.MortalidadHembras + seg.MortalidadMachos;
            var mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadHembras + s.MortalidadMachos);

            var errorSexaje = seg.ErrorSexajeHembras + seg.ErrorSexajeMachos;
            var errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeHembras + s.ErrorSexajeMachos);

            // Descarte incluye selecciones (SelH, SelM) que pueden ser negativas si son descuentos por traslado
            // Separar selección normal de traslados para calcular correctamente
            var seleccionH = seg.SelH;
            var seleccionM = seg.SelM;
            
            // Selección normal (valores positivos): aves retiradas por selección/descarte
            var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);
            
            // Traslados (valores negativos): aves trasladadas a otro lote/granja
            // Los valores negativos representan aves que salieron, así que debemos restar el valor absoluto
            var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
            var trasladosAbsoluto = Math.Abs(traslados);
            
            // Descarte normal (valores positivos): selección/descarte normal
            var descarteNormal = seleccionNormal;
            
            // Traslados (valores negativos en valor absoluto)
            var trasladosNumero = (int)trasladosAbsoluto;
            
            // Total descarte = selección normal + traslados (en valor absoluto para acumulación)
            // Este campo se mantiene para compatibilidad, pero ahora tenemos campos separados
            var descarte = seleccionH + seleccionM;
            
            // Calcular descarte acumulado hasta esta fecha (solo valores positivos)
            var descarteAcumulado = 0;
            var trasladosAcumulado = 0;
            foreach (var reg in registrosHastaFecha)
            {
                var selH = reg.SelH;
                var selM = reg.SelM;
                var selNormal = Math.Max(0, selH) + Math.Max(0, selM);
                var tras = Math.Min(0, selH) + Math.Min(0, selM);
                var trasAbs = Math.Abs(tras);
                descarteAcumulado += (int)selNormal;
                trasladosAcumulado += (int)trasAbs;
            }
            
            // Calcular aves actuales hasta esta fecha
            // IMPORTANTE: Para calcular el porcentaje de mortalidad diario correctamente,
            // necesitamos las aves ANTES de aplicar la mortalidad del día actual
            var avesActuales = avesIniciales;
            var avesAntesMortalidad = avesIniciales; // Aves antes de aplicar la mortalidad del día actual
            
            foreach (var reg in registrosHastaFecha)
            {
                var mortTotal = reg.MortalidadHembras + reg.MortalidadMachos;
                
                // Si este es el registro actual, guardar aves antes de aplicar mortalidad
                if (reg.Id == seg.Id)
                {
                    avesAntesMortalidad = avesActuales;
                }
                
                avesActuales -= mortTotal;
                
                var selH = reg.SelH;
                var selM = reg.SelM;
                var selNormal = Math.Max(0, selH) + Math.Max(0, selM);
                var tras = Math.Min(0, selH) + Math.Min(0, selM);
                var trasAbs = Math.Abs(tras);
                
                avesActuales -= selNormal;
                avesActuales -= trasAbs;
            }

            var consumoKilos = (decimal)seg.ConsumoKgHembras + (decimal)(seg.ConsumoKgMachos ?? 0);
            var consumoAcumulado = registrosHastaFecha.Sum(s => (decimal)s.ConsumoKgHembras + (decimal)(s.ConsumoKgMachos ?? 0));

            var consumoGramosPorAve = avesActuales > 0 ? (consumoKilos * 1000) / avesActuales : 0;

            var pesoActual = (decimal?)(seg.PesoPromH ?? seg.PesoPromM);
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;

            var dto = new ReporteTecnicoDiarioDto
            {
                Fecha = seg.FechaRegistro,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                NumeroAves = avesActuales,
                MortalidadTotal = mortalidadTotal,
                // CORRECCIÓN: El porcentaje de mortalidad diario debe calcularse sobre las aves ANTES de la mortalidad del día
                MortalidadPorcentajeDiario = avesAntesMortalidad > 0 ? (decimal)mortalidadTotal / avesAntesMortalidad * 100 : 0,
                MortalidadPorcentajeAcumulado = avesIniciales > 0 ? (decimal)mortalidadAcumulada / avesIniciales * 100 : 0,
                ErrorSexajeNumero = errorSexaje,
                ErrorSexajePorcentaje = avesActuales > 0 ? (decimal)errorSexaje / avesActuales * 100 : 0,
                ErrorSexajePorcentajeAcumulado = avesIniciales > 0 ? (decimal)errorSexajeAcumulado / avesIniciales * 100 : 0,
                DescarteNumero = descarteNormal, // Solo descarte normal (valores positivos)
                DescartePorcentajeDiario = avesActuales > 0 ? (decimal)descarteNormal / avesActuales * 100 : 0,
                DescartePorcentajeAcumulado = avesIniciales > 0 ? (decimal)descarteAcumulado / avesIniciales * 100 : 0,
                TrasladosNumero = trasladosNumero, // Traslados (valores negativos en valor absoluto)
                ConsumoBultos = CalcularBultos(consumoKilos), // Asumiendo 40kg por bulto estándar
                ConsumoKilos = consumoKilos,
                ConsumoKilosAcumulado = consumoAcumulado,
                ConsumoGramosPorAve = consumoGramosPorAve,
                IngresosAlimentoKilos = await ObtenerIngresosAlimentoAsync(lote.GranjaId, seg.FechaRegistro, ct),
                TrasladosAlimentoKilos = await ObtenerTrasladosAlimentoAsync(lote.GranjaId, seg.FechaRegistro, ct),
                PesoActual = pesoActual,
                Uniformidad = (decimal?)(seg.UniformidadH ?? seg.UniformidadM),
                GananciaPeso = gananciaPeso,
                CoeficienteVariacion = (decimal?)(seg.CvH ?? seg.CvM),
                SeleccionVentasNumero = descarte,
                SeleccionVentasPorcentaje = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0
            };

            // Actualizar peso anterior para el siguiente cálculo
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;

            datosDiarios.Add(dto);
        }

        return datosDiarios;
    }

    private async Task<List<ReporteTecnicoDiarioDto>> ObtenerDatosDiariosProduccionAsync(
        string loteId,
        DateTime? fechaEncaset,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        var loteIdInt = int.TryParse(loteId, out var id) ? id : 0;
        var lote = await _ctx.Lotes.AsNoTracking().FirstOrDefaultAsync(l => l.LoteId == loteIdInt, ct);
        var loteProd = lote != null && lote.Fase != "Produccion"
            ? await _ctx.Lotes.AsNoTracking().FirstOrDefaultAsync(l => l.LotePadreId == loteIdInt && l.Fase == "Produccion" && l.DeletedAt == null, ct)
            : lote;
        var loteIdSeguimiento = (loteProd ?? lote)?.LoteId ?? loteIdInt;
        var query = _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteIdSeguimiento);

        if (fechaInicio.HasValue)
            query = query.Where(s => s.Fecha >= fechaInicio.Value);

        if (fechaFin.HasValue)
            query = query.Where(s => s.Fecha <= fechaFin.Value);

        var seguimientos = await query
            .OrderBy(s => s.Fecha)
            .ToListAsync(ct);

        if (!fechaEncaset.HasValue)
            return new List<ReporteTecnicoDiarioDto>();

        if (lote == null)
            return new List<ReporteTecnicoDiarioDto>();

        var avesIniciales = loteProd != null
            ? (loteProd.HembrasInicialesProd ?? 0) + (loteProd.MachosInicialesProd ?? 0)
            : (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);

        var datosDiarios = new List<ReporteTecnicoDiarioDto>();
        var avesActuales = avesIniciales;
        var mortalidadAcumulada = 0;
        var consumoAcumulado = 0m;
        var descarteAcumulado = 0;
        decimal? pesoAnterior = null;

        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.Fecha);
            var edadSemanas = CalcularEdadSemanas(edadDias);

            var mortalidadTotal = seg.MortalidadH + seg.MortalidadM;
            mortalidadAcumulada += mortalidadTotal;
            avesActuales -= mortalidadTotal;

            var descarte = seg.SelH;
            descarteAcumulado += descarte;
            avesActuales -= descarte;

            var consumoKilos = seg.ConsKgH + seg.ConsKgM;
            consumoAcumulado += consumoKilos;

            var consumoGramosPorAve = avesActuales > 0 ? (consumoKilos * 1000) / avesActuales : 0;

            var pesoActual = seg.PesoH;
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;

            var dto = new ReporteTecnicoDiarioDto
            {
                Fecha = seg.Fecha,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                NumeroAves = avesActuales,
                MortalidadTotal = mortalidadTotal,
                MortalidadPorcentajeDiario = avesActuales > 0 ? (decimal)mortalidadTotal / avesActuales * 100 : 0,
                MortalidadPorcentajeAcumulado = avesIniciales > 0 ? (decimal)mortalidadAcumulada / avesIniciales * 100 : 0,
                ErrorSexajeNumero = 0, // No aplica en producción
                ErrorSexajePorcentaje = 0,
                ErrorSexajePorcentajeAcumulado = 0,
                DescarteNumero = descarte,
                DescartePorcentajeDiario = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0,
                DescartePorcentajeAcumulado = avesIniciales > 0 ? (decimal)descarteAcumulado / avesIniciales * 100 : 0,
                ConsumoBultos = CalcularBultos(consumoKilos), // Asumiendo 40kg por bulto estándar
                ConsumoKilos = consumoKilos,
                ConsumoKilosAcumulado = consumoAcumulado,
                ConsumoGramosPorAve = consumoGramosPorAve,
                IngresosAlimentoKilos = await ObtenerIngresosAlimentoAsync(lote.GranjaId, seg.Fecha, ct),
                TrasladosAlimentoKilos = await ObtenerTrasladosAlimentoAsync(lote.GranjaId, seg.Fecha, ct),
                PesoActual = pesoActual,
                Uniformidad = seg.Uniformidad,
                GananciaPeso = gananciaPeso,
                CoeficienteVariacion = seg.CoeficienteVariacion,
                SeleccionVentasNumero = descarte,
                SeleccionVentasPorcentaje = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0
            };

            // Actualizar peso anterior para el siguiente cálculo
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;

            datosDiarios.Add(dto);
        }

        return datosDiarios;
    }

    private List<ReporteTecnicoDiarioDto> ConsolidarDatosDiarios(List<ReporteTecnicoDiarioDto> todosDatos)
    {
        return todosDatos
            .GroupBy(d => d.Fecha.Date)
            .Select(g =>
            {
                var datosFecha = g.ToList();
                var primero = datosFecha.First();

                return new ReporteTecnicoDiarioDto
                {
                    Fecha = primero.Fecha,
                    EdadDias = (int)datosFecha.Average(d => d.EdadDias),
                    EdadSemanas = (int)datosFecha.Average(d => d.EdadSemanas),
                    NumeroAves = datosFecha.Sum(d => d.NumeroAves),
                    MortalidadTotal = datosFecha.Sum(d => d.MortalidadTotal),
                    MortalidadPorcentajeDiario = datosFecha.Average(d => d.MortalidadPorcentajeDiario),
                    MortalidadPorcentajeAcumulado = datosFecha.Average(d => d.MortalidadPorcentajeAcumulado),
                    ErrorSexajeNumero = datosFecha.Sum(d => d.ErrorSexajeNumero),
                    ErrorSexajePorcentaje = datosFecha.Average(d => d.ErrorSexajePorcentaje),
                    ErrorSexajePorcentajeAcumulado = datosFecha.Average(d => d.ErrorSexajePorcentajeAcumulado),
                    DescarteNumero = datosFecha.Sum(d => d.DescarteNumero),
                    DescartePorcentajeDiario = datosFecha.Average(d => d.DescartePorcentajeDiario),
                    DescartePorcentajeAcumulado = datosFecha.Average(d => d.DescartePorcentajeAcumulado),
                    TrasladosNumero = datosFecha.Sum(d => d.TrasladosNumero),
                    ConsumoBultos = datosFecha.Sum(d => d.ConsumoBultos),
                    ConsumoKilos = datosFecha.Sum(d => d.ConsumoKilos),
                    ConsumoKilosAcumulado = datosFecha.Sum(d => d.ConsumoKilosAcumulado),
                    ConsumoGramosPorAve = datosFecha.Average(d => d.ConsumoGramosPorAve),
                    IngresosAlimentoKilos = datosFecha.Sum(d => d.IngresosAlimentoKilos),
                    TrasladosAlimentoKilos = datosFecha.Sum(d => d.TrasladosAlimentoKilos),
                    PesoActual = datosFecha.Where(d => d.PesoActual.HasValue).Select(d => d.PesoActual!.Value).DefaultIfEmpty(0).Average(),
                    Uniformidad = datosFecha.Where(d => d.Uniformidad.HasValue).Select(d => d.Uniformidad!.Value).DefaultIfEmpty(0).Average(),
                    GananciaPeso = null, // TODO: Calcular ganancia
                    CoeficienteVariacion = datosFecha.Where(d => d.CoeficienteVariacion.HasValue).Select(d => d.CoeficienteVariacion!.Value).DefaultIfEmpty(0).Average(),
                    SeleccionVentasNumero = datosFecha.Sum(d => d.SeleccionVentasNumero),
                    SeleccionVentasPorcentaje = datosFecha.Average(d => d.SeleccionVentasPorcentaje)
                };
            })
            .OrderBy(d => d.Fecha)
            .ToList();
    }

    /// <summary>
    /// Genera reporte diario específico de MACHOS desde el seguimiento diario de levante.
    /// lotePosturaLevanteId = id de lote_postura_levante (seguimiento_diario.lote_postura_levante_id).
    /// </summary>
    public async Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
        int lotePosturaLevanteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);
            
            if (lpl == null)
                throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");
            
            if (!lpl.FechaEncaset.HasValue)
                throw new InvalidOperationException($"El lote levante {lotePosturaLevanteId} no tiene fecha de encaset");
            
            var machosIniciales = lpl.MachosL ?? 0;
            var granjaId = lpl.GranjaId;
        
        var todosSeguimientos = await ObtenerSeguimientosLevantePorLPLAsync(lotePosturaLevanteId, ct);
        
        todosSeguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();
        
        var queryFiltrado = todosSeguimientos.AsQueryable();
        if (fechaInicio.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);
        if (fechaFin.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);
        
        var seguimientos = queryFiltrado.ToList();
        
        var datosMachos = new List<ReporteTecnicoDiarioMachosDto>();
        decimal? pesoAnterior = null;
        
        decimal porcMortalidadAcumuladaAnterior = 0;
        decimal porcSeleccionAcumuladaAnterior = 0;
        decimal porcDescarteAcumuladaAnterior = 0;
        decimal porcErrorSexajeAcumuladaAnterior = 0;
        decimal consumoAcumuladoAnterior = 0;
        
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();
            // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad_diaria - seleccion_diaria - descarte_diaria
            // Donde: seleccion_diaria = traslados, descarte_diaria = seleccion_normal + error_sexaje
            var machosActuales = machosIniciales;
            
            foreach (var reg in registrosHastaFecha)
            {
                // Aplicar mortalidad
                machosActuales -= reg.MortalidadMachos;
                
                // Separar selección normal de traslados
                var selMReg = reg.SelM;
                var seleccionNormalReg = Math.Max(0, selMReg);
                var trasladosReg = Math.Abs(Math.Min(0, selMReg));
                
                // Descarte = selección normal + error de sexaje
                var descarteReg = seleccionNormalReg + reg.ErrorSexajeMachos;
                
                // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad - seleccion (traslados) - descarte
                machosActuales -= trasladosReg; // seleccion_diaria (traslados)
                machosActuales -= descarteReg;  // descarte_diaria (seleccion_normal + error_sexaje)
            }
            
            // Calcular valores del día actual
            var mortalidad = seg.MortalidadMachos;
            var mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadMachos);
            
            var selM = seg.SelM;
            var seleccionNormal = Math.Max(0, selM);
            var traslados = Math.Abs(Math.Min(0, selM));
            var seleccionAcumulada = registrosHastaFecha.Sum(s => Math.Max(0, s.SelM));
            var trasladosAcumulados = registrosHastaFecha.Sum(s => Math.Abs(Math.Min(0, s.SelM)));
            
            var errorSexaje = seg.ErrorSexajeMachos;
            var errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeMachos);
            
            // Descarte = selección + error de sexaje
            var descarteDiaria = seleccionNormal + errorSexaje;
            var descarteAcumulada = seleccionAcumulada + errorSexajeAcumulado;
            
            // FÓRMULAS SEGÚN EXCEL:
            // porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
            var porcMortalidadDiaria = machosIniciales > 0 
                ? (decimal)mortalidad / machosIniciales * 100 
                : 0;
            
            // porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
            var porcMortalidadAcumulada = porcMortalidadAcumuladaAnterior + porcMortalidadDiaria;
            
            // porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
            var porcSeleccionDiaria = machosIniciales > 0 
                ? (decimal)seleccionNormal / machosIniciales * 100 
                : 0;
            
            // porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
            var porcSeleccionAcumulada = porcSeleccionAcumuladaAnterior + porcSeleccionDiaria;
            
            // porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
            var porcDescarteDiario = machosIniciales > 0 
                ? (decimal)descarteDiaria / machosIniciales * 100 
                : 0;
            
            // porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
            var porcDescarteAcumulada = porcDescarteAcumuladaAnterior + porcDescarteDiario;
            
            // CONSUMO según fórmulas Excel:
            // consumo_diario = consumo_semanal / 40 (en bultos, asumiendo 40kg por bulto)
            // Nota: En el seguimiento tenemos consumo diario en kg, así que:
            // consumo_diario_bultos = consumo_kg / 40
            var consumoKg = (decimal)(seg.ConsumoKgMachos ?? 0);
            var consumoDiarioBultos = consumoKg / 40;
            
            // consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
            // Nota: consumo_semanal en kg, así que acumulamos en kg
            var consumoAcumulado = consumoAcumuladoAnterior + consumoKg;
            
            // consumo_por_ave = (consumo_diario * 40000) / aves_vivas
            // consumo_diario está en bultos, entonces (bultos * 40000g) / aves = gramos por ave
            var consumoGramosPorAve = machosActuales > 0 
                ? (consumoDiarioBultos * 40000) / machosActuales 
                : 0;
            
            // consumo_total_kg = (aves_vivas * consumo_unitario_gramos) / 1000
            // consumo_unitario_gramos es el consumo por ave en gramos
            var consumoTotalKg = (machosActuales * consumoGramosPorAve) / 1000;
            
            // Peso y ganancia
            var pesoActual = (decimal?)(seg.PesoPromM);
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;
            
            // Valores nutricionales
            var kcalAl = seg.KcalAlH; // Mismo alimento para machos y hembras
            var protAl = seg.ProtAlH;
            var kcalAve = machosActuales > 0 && kcalAl.HasValue 
                ? (kcalAl.Value * (double)consumoKg) / machosActuales 
                : (double?)null;
            var protAve = machosActuales > 0 && protAl.HasValue 
                ? (protAl.Value * (double)consumoKg) / machosActuales 
                : (double?)null;
            
            // Ingresos y traslados de alimento
            var ingresosAlimento = granjaId > 0 
                ? await ObtenerIngresosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            var trasladosAlimento = granjaId > 0 
                ? await ObtenerTrasladosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            
            var dto = new ReporteTecnicoDiarioMachosDto
            {
                Fecha = seg.FechaRegistro,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                SaldoMachos = machosActuales,
                MortalidadMachos = mortalidad,
                MortalidadMachosAcumulada = mortalidadAcumulada,
                // FÓRMULA EXCEL: porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
                MortalidadMachosPorcentajeDiario = porcMortalidadDiaria,
                // FÓRMULA EXCEL: porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
                MortalidadMachosPorcentajeAcumulado = porcMortalidadAcumulada,
                SeleccionMachos = seleccionNormal,
                SeleccionMachosAcumulada = seleccionAcumulada,
                // FÓRMULA EXCEL: porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
                SeleccionMachosPorcentajeDiario = porcSeleccionDiaria,
                // FÓRMULA EXCEL: porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
                SeleccionMachosPorcentajeAcumulado = porcSeleccionAcumulada,
                TrasladosMachos = traslados,
                TrasladosMachosAcumulados = trasladosAcumulados,
                ErrorSexajeMachos = errorSexaje,
                ErrorSexajeMachosAcumulado = errorSexajeAcumulado,
                // Error de sexaje también sobre total_inicial_aves
                // porc_error_diario = (error_diario / total_inicial_aves) * 100
                ErrorSexajeMachosPorcentajeDiario = machosIniciales > 0 
                    ? (decimal)errorSexaje / machosIniciales * 100 
                    : 0,
                // porc_error_acumulado = porc_error_acumulado_anterior + porc_error_diario
                ErrorSexajeMachosPorcentajeAcumulado = porcErrorSexajeAcumuladaAnterior + (machosIniciales > 0 
                    ? (decimal)errorSexaje / machosIniciales * 100 
                    : 0),
                // DESCARTE (Selección + Error Sexaje)
                DescarteMachos = descarteDiaria,
                DescarteMachosAcumulado = descarteAcumulada,
                // FÓRMULA EXCEL: porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
                DescarteMachosPorcentajeDiario = porcDescarteDiario,
                // FÓRMULA EXCEL: porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
                DescarteMachosPorcentajeAcumulado = porcDescarteAcumulada,
                // FÓRMULA EXCEL: consumo_diario = consumo_semanal / 40 (en bultos)
                // Guardamos consumo en kg (consumoKg), pero el cálculo de gramos/ave usa la fórmula Excel
                ConsumoKgMachos = consumoKg,
                // FÓRMULA EXCEL: consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
                ConsumoKgMachosAcumulado = consumoAcumulado,
                // FÓRMULA EXCEL: consumo_por_ave = (consumo_diario * 40000) / aves_vivas
                ConsumoGramosPorAveMachos = consumoGramosPorAve,
                PesoPromedioMachos = pesoActual,
                UniformidadMachos = (decimal?)(seg.UniformidadM),
                CoeficienteVariacionMachos = (decimal?)(seg.CvM),
                GananciaPesoMachos = gananciaPeso,
                KcalAlMachos = kcalAl,
                ProtAlMachos = protAl,
                KcalAveMachos = kcalAve,
                ProtAveMachos = protAve,
                IngresosAlimentoKilos = ingresosAlimento,
                TrasladosAlimentoKilos = trasladosAlimento,
                Observaciones = seg.Observaciones
            };
            
            // Actualizar valores acumulados para la siguiente iteración
            porcMortalidadAcumuladaAnterior = porcMortalidadAcumulada;
            porcSeleccionAcumuladaAnterior = porcSeleccionAcumulada;
            porcDescarteAcumuladaAnterior = porcDescarteAcumulada;
            porcErrorSexajeAcumuladaAnterior = dto.ErrorSexajeMachosPorcentajeAcumulado;
            consumoAcumuladoAnterior = consumoAcumulado;
            
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;
            
            datosMachos.Add(dto);
        }
        
        return datosMachos;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte diario de machos para lote levante {lotePosturaLevanteId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Genera reporte diario específico de HEMBRAS desde el seguimiento diario de levante.
    /// lotePosturaLevanteId = id de lote_postura_levante.
    /// </summary>
    public async Task<List<ReporteTecnicoDiarioHembrasDto>> GenerarReporteDiarioHembrasAsync(
        int lotePosturaLevanteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);
            
            if (lpl == null)
                throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");
            
            if (!lpl.FechaEncaset.HasValue)
                throw new InvalidOperationException($"El lote levante {lotePosturaLevanteId} no tiene fecha de encaset");
            
            var hembrasIniciales = lpl.HembrasL ?? 0;
            var granjaId = lpl.GranjaId;
        
        var todosSeguimientos = await ObtenerSeguimientosLevantePorLPLAsync(lotePosturaLevanteId, ct);
        
        todosSeguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();
        
        // Aplicar filtros de fecha
        var queryFiltrado = todosSeguimientos.AsQueryable();
        if (fechaInicio.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);
        if (fechaFin.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);
        
        var seguimientos = queryFiltrado.ToList();
        
        // Procesar cada registro diario
        var datosHembras = new List<ReporteTecnicoDiarioHembrasDto>();
        decimal? pesoAnterior = null;
        
        // Variables para acumular porcentajes (según fórmulas Excel)
        decimal porcMortalidadAcumuladaAnterior = 0;
        decimal porcSeleccionAcumuladaAnterior = 0;
        decimal porcDescarteAcumuladaAnterior = 0;
        decimal porcErrorSexajeAcumuladaAnterior = 0;
        decimal consumoAcumuladoAnterior = 0;
        
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();
            // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad_diaria - seleccion_diaria - descarte_diaria
            // Donde: seleccion_diaria = traslados, descarte_diaria = seleccion_normal + error_sexaje
            var hembrasActuales = hembrasIniciales;
            
            foreach (var reg in registrosHastaFecha)
            {
                // Aplicar mortalidad
                hembrasActuales -= reg.MortalidadHembras;
                
                // Separar selección normal de traslados
                var selHReg = reg.SelH;
                var seleccionNormalReg = Math.Max(0, selHReg);
                var trasladosReg = Math.Abs(Math.Min(0, selHReg));
                
                // Descarte = selección normal + error de sexaje
                var descarteReg = seleccionNormalReg + reg.ErrorSexajeHembras;
                
                // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad - seleccion (traslados) - descarte
                hembrasActuales -= trasladosReg; // seleccion_diaria (traslados)
                hembrasActuales -= descarteReg;  // descarte_diaria (seleccion_normal + error_sexaje)
            }
            
            // Calcular valores del día actual
            var mortalidad = seg.MortalidadHembras;
            var mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadHembras);
            
            var selH = seg.SelH;
            var seleccionNormal = Math.Max(0, selH);
            var traslados = Math.Abs(Math.Min(0, selH));
            var seleccionAcumulada = registrosHastaFecha.Sum(s => Math.Max(0, s.SelH));
            var trasladosAcumulados = registrosHastaFecha.Sum(s => Math.Abs(Math.Min(0, s.SelH)));
            
            var errorSexaje = seg.ErrorSexajeHembras;
            var errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeHembras);
            
            // Descarte = selección + error de sexaje
            var descarteDiaria = seleccionNormal + errorSexaje;
            var descarteAcumulada = seleccionAcumulada + errorSexajeAcumulado;
            
            // FÓRMULAS SEGÚN EXCEL:
            // porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
            var porcMortalidadDiaria = hembrasIniciales > 0 
                ? (decimal)mortalidad / hembrasIniciales * 100 
                : 0;
            
            // porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
            var porcMortalidadAcumulada = porcMortalidadAcumuladaAnterior + porcMortalidadDiaria;
            
            // porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
            var porcSeleccionDiaria = hembrasIniciales > 0 
                ? (decimal)seleccionNormal / hembrasIniciales * 100 
                : 0;
            
            // porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
            var porcSeleccionAcumulada = porcSeleccionAcumuladaAnterior + porcSeleccionDiaria;
            
            // porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
            var porcDescarteDiario = hembrasIniciales > 0 
                ? (decimal)descarteDiaria / hembrasIniciales * 100 
                : 0;
            
            // porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
            var porcDescarteAcumulada = porcDescarteAcumuladaAnterior + porcDescarteDiario;
            
            // CONSUMO según fórmulas Excel:
            // consumo_diario = consumo_semanal / 40 (en bultos, asumiendo 40kg por bulto)
            // Nota: En el seguimiento tenemos consumo diario en kg, así que:
            // consumo_diario_bultos = consumo_kg / 40
            var consumoKg = (decimal)seg.ConsumoKgHembras;
            var consumoDiarioBultos = consumoKg / 40;
            
            // consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
            // Nota: consumo_semanal en kg, así que acumulamos en kg
            var consumoAcumulado = consumoAcumuladoAnterior + consumoKg;
            
            // consumo_por_ave = (consumo_diario * 40000) / aves_vivas
            // consumo_diario está en bultos, entonces (bultos * 40000g) / aves = gramos por ave
            var consumoGramosPorAve = hembrasActuales > 0 
                ? (consumoDiarioBultos * 40000) / hembrasActuales 
                : 0;
            
            // consumo_total_kg = (aves_vivas * consumo_unitario_gramos) / 1000
            // consumo_unitario_gramos es el consumo por ave en gramos
            var consumoTotalKg = (hembrasActuales * consumoGramosPorAve) / 1000;
            
            // Peso y ganancia
            var pesoActual = (decimal?)(seg.PesoPromH);
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;
            
            // Valores nutricionales
            var kcalAl = seg.KcalAlH;
            var protAl = seg.ProtAlH;
            // KcalAveH y ProtAveH pueden venir del seguimiento o calcularse
            var kcalAve = seg.KcalAveH ?? (hembrasActuales > 0 && kcalAl.HasValue 
                ? (kcalAl.Value * (double)consumoKg) / hembrasActuales 
                : (double?)null);
            var protAve = seg.ProtAveH ?? (hembrasActuales > 0 && protAl.HasValue 
                ? (protAl.Value * (double)consumoKg) / hembrasActuales 
                : (double?)null);
            
            // Ingresos y traslados de alimento
            var ingresosAlimento = granjaId > 0 
                ? await ObtenerIngresosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            var trasladosAlimento = granjaId > 0 
                ? await ObtenerTrasladosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            
            var dto = new ReporteTecnicoDiarioHembrasDto
            {
                Fecha = seg.FechaRegistro,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                SaldoHembras = hembrasActuales,
                MortalidadHembras = mortalidad,
                MortalidadHembrasAcumulada = mortalidadAcumulada,
                // FÓRMULA EXCEL: porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
                MortalidadHembrasPorcentajeDiario = porcMortalidadDiaria,
                // FÓRMULA EXCEL: porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
                MortalidadHembrasPorcentajeAcumulado = porcMortalidadAcumulada,
                SeleccionHembras = seleccionNormal,
                SeleccionHembrasAcumulada = seleccionAcumulada,
                // FÓRMULA EXCEL: porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
                SeleccionHembrasPorcentajeDiario = porcSeleccionDiaria,
                // FÓRMULA EXCEL: porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
                SeleccionHembrasPorcentajeAcumulado = porcSeleccionAcumulada,
                TrasladosHembras = traslados,
                TrasladosHembrasAcumulados = trasladosAcumulados,
                ErrorSexajeHembras = errorSexaje,
                ErrorSexajeHembrasAcumulado = errorSexajeAcumulado,
                // Error de sexaje también sobre total_inicial_aves
                // porc_error_diario = (error_diario / total_inicial_aves) * 100
                ErrorSexajeHembrasPorcentajeDiario = hembrasIniciales > 0 
                    ? (decimal)errorSexaje / hembrasIniciales * 100 
                    : 0,
                // porc_error_acumulado = porc_error_acumulado_anterior + porc_error_diario
                ErrorSexajeHembrasPorcentajeAcumulado = porcErrorSexajeAcumuladaAnterior + (hembrasIniciales > 0 
                    ? (decimal)errorSexaje / hembrasIniciales * 100 
                    : 0),
                // DESCARTE (Selección + Error Sexaje)
                DescarteHembras = descarteDiaria,
                DescarteHembrasAcumulado = descarteAcumulada,
                // FÓRMULA EXCEL: porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
                DescarteHembrasPorcentajeDiario = porcDescarteDiario,
                // FÓRMULA EXCEL: porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
                DescarteHembrasPorcentajeAcumulado = porcDescarteAcumulada,
                // FÓRMULA EXCEL: consumo_diario = consumo_semanal / 40 (en bultos)
                // Guardamos consumo en kg (consumoKg), pero el cálculo de gramos/ave usa la fórmula Excel
                ConsumoKgHembras = consumoKg,
                // FÓRMULA EXCEL: consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
                ConsumoKgHembrasAcumulado = consumoAcumulado,
                // FÓRMULA EXCEL: consumo_por_ave = (consumo_diario * 40000) / aves_vivas
                ConsumoGramosPorAveHembras = consumoGramosPorAve,
                PesoPromedioHembras = pesoActual,
                UniformidadHembras = (decimal?)(seg.UniformidadH),
                CoeficienteVariacionHembras = (decimal?)(seg.CvH),
                GananciaPesoHembras = gananciaPeso,
                KcalAlHembras = kcalAl,
                ProtAlHembras = protAl,
                KcalAveHembras = kcalAve,
                ProtAveHembras = protAve,
                IngresosAlimentoKilos = ingresosAlimento,
                TrasladosAlimentoKilos = trasladosAlimento,
                Observaciones = seg.Observaciones
            };
            
            // Actualizar valores acumulados para la siguiente iteración
            porcMortalidadAcumuladaAnterior = porcMortalidadAcumulada;
            porcSeleccionAcumuladaAnterior = porcSeleccionAcumulada;
            porcDescarteAcumuladaAnterior = porcDescarteAcumulada;
            porcErrorSexajeAcumuladaAnterior = dto.ErrorSexajeHembrasPorcentajeAcumulado;
            consumoAcumuladoAnterior = consumoAcumulado;
            
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;
            
            datosHembras.Add(dto);
        }
        
        return datosHembras;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte diario de hembras para lote levante {lotePosturaLevanteId}: {ex.Message}", ex);
        }
    }
}

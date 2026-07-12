// MovimientoAves/Funciones/MovimientoAvesService.Mapeo.cs
// Expresiones LINQ estáticas compartidas para proyectar la entidad MovimientoAves a sus DTOs
// (resumen y navegación completa). Se ejecutan en la BD (traducibles a SQL) → el backend orquesta.
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    private static System.Linq.Expressions.Expression<Func<MovimientoAves, MovimientoAvesDto>> ToDto =>
        m => new MovimientoAvesDto(
            m.Id,
            m.NumeroMovimiento,
            m.FechaMovimiento,
            m.TipoMovimiento,
            // Origen
            new UbicacionMovimientoDto(
                m.LoteOrigenId,
                m.LoteOrigen != null ? m.LoteOrigen.LoteNombre : null,
                m.GranjaOrigenId,
                m.GranjaOrigen != null ? m.GranjaOrigen.Name : null,
                m.NucleoOrigenId,
                null, // NucleoOrigen navigation property removed
                m.GalponOrigenId,
                null  // GalponOrigen navigation property removed
            ),
            // Destino
            new UbicacionMovimientoDto(
                m.LoteDestinoId,
                m.LoteDestino != null ? m.LoteDestino.LoteNombre : null,
                m.GranjaDestinoId,
                m.GranjaDestino != null ? m.GranjaDestino.Name : null,
                m.NucleoDestinoId,
                null, // NucleoDestino navigation property removed
                m.GalponDestinoId,
                null  // GalponDestino navigation property removed
            ),
            // Cantidades
            m.CantidadHembras,
            m.CantidadMachos,
            m.CantidadMixtas,
            m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas,
            // Estado e información
            m.Estado,
            m.MotivoMovimiento,
            m.Descripcion,
            m.PlantaDestino,
            m.Observaciones,
            // Usuario
            m.UsuarioMovimientoId,
            m.UsuarioNombre,
            // Fechas
            m.FechaProcesamiento,
            m.FechaCancelacion,
            m.CreatedAt,
            // Campos específicos para despacho (Ecuador)
            m.EdadAves,
            m.Raza,
            m.Placa,
            m.HoraSalida,
            m.GuiaAgrocalidad,
            m.Sellos,
            m.Ayuno,
            m.Conductor,
            m.TotalPollosGalpon,
            m.PesoBruto,
            m.PesoTara,
            m.PesoNeto,
            m.PromedioPesoAve
        );

    private static System.Linq.Expressions.Expression<Func<MovimientoAves, MovimientoAvesCompletoDto>> ToMovimientoCompletoDto =>
        m => new MovimientoAvesCompletoDto(
            m.Id,
            m.NumeroMovimiento,
            m.FechaMovimiento,
            m.TipoMovimiento,
            // Origen completo
            new UbicacionCompletaDto(
                m.LoteOrigenId,
                m.GranjaOrigenId,
                m.NucleoOrigenId,
                m.GalponOrigenId,
                m.CompanyId,
                m.LoteOrigen != null ? m.LoteOrigen.LoteNombre : null,
                m.GranjaOrigen != null ? m.GranjaOrigen.Name : null,
                null, // NucleoNombre - se obtendrá por separado
                null, // GalponNombre - se obtendrá por separado
                m.GranjaOrigen != null ? m.GranjaOrigen.CompanyId.ToString() : null,
                null, // Regional
                null, // Departamento
                null, // Municipio
                null, // TipoGalpon
                null, // AnchoGalpon
                null, // LargoGalpon
                m.LoteOrigen != null ? m.LoteOrigen.Raza : null,
                m.LoteOrigen != null ? m.LoteOrigen.Linea : null,
                m.LoteOrigen != null ? m.LoteOrigen.TipoLinea : null,
                m.LoteOrigen != null ? m.LoteOrigen.CodigoGuiaGenetica : null,
                m.LoteOrigen != null ? m.LoteOrigen.AnoTablaGenetica : null,
                m.LoteOrigen != null ? m.LoteOrigen.Tecnico : null,
                m.GranjaOrigen != null ? m.GranjaOrigen.Status : null,
                m.LoteOrigen != null ? m.LoteOrigen.FechaEncaset : null,
                m.LoteOrigen != null ? m.LoteOrigen.EdadInicial : null
            ),
            // Destino completo
            new UbicacionCompletaDto(
                m.LoteDestinoId,
                m.GranjaDestinoId,
                m.NucleoDestinoId,
                m.GalponDestinoId,
                m.CompanyId,
                m.LoteDestino != null ? m.LoteDestino.LoteNombre : null,
                m.GranjaDestino != null ? m.GranjaDestino.Name : null,
                null, // NucleoNombre - se obtendrá por separado
                null, // GalponNombre - se obtendrá por separado
                m.GranjaDestino != null ? m.GranjaDestino.CompanyId.ToString() : null,
                null, // Regional
                null, // Departamento
                null, // Municipio
                null, // TipoGalpon
                null, // AnchoGalpon
                null, // LargoGalpon
                m.LoteDestino != null ? m.LoteDestino.Raza : null,
                m.LoteDestino != null ? m.LoteDestino.Linea : null,
                m.LoteDestino != null ? m.LoteDestino.TipoLinea : null,
                m.LoteDestino != null ? m.LoteDestino.CodigoGuiaGenetica : null,
                m.LoteDestino != null ? m.LoteDestino.AnoTablaGenetica : null,
                m.LoteDestino != null ? m.LoteDestino.Tecnico : null,
                m.GranjaDestino != null ? m.GranjaDestino.Status : null,
                m.LoteDestino != null ? m.LoteDestino.FechaEncaset : null,
                m.LoteDestino != null ? m.LoteDestino.EdadInicial : null
            ),
            // Cantidades
            m.CantidadHembras,
            m.CantidadMachos,
            m.CantidadMixtas,
            m.TotalAves,
            // Estado e información
            m.Estado,
            m.MotivoMovimiento,
            m.Observaciones,
            // Usuario
            m.UsuarioMovimientoId,
            m.UsuarioNombre,
            // Fechas
            m.FechaProcesamiento,
            m.FechaCancelacion,
            m.CreatedAt,
            m.UpdatedAt,
            // Información calculada
            m.GranjaOrigenId == m.GranjaDestinoId,
            m.GranjaOrigenId != m.GranjaDestinoId,
            m.TipoMovimiento == "Traslado" ? "Traslado de Aves" :
            m.TipoMovimiento == "Ajuste" ? "Ajuste de Inventario" :
            m.TipoMovimiento == "Liquidacion" ? "Liquidación de Lote" :
            m.TipoMovimiento
        );
}

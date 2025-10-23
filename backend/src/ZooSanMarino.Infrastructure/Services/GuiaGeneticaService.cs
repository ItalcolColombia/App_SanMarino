using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class GuiaGeneticaService : IGuiaGeneticaService
{
    private readonly ZooSanMarinoContext _ctx;

    public GuiaGeneticaService(ZooSanMarinoContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// Obtiene los datos de guía genética para una raza, año y edad específicos
    /// </summary>
    public async Task<GuiaGeneticaResponse> ObtenerGuiaGeneticaAsync(GuiaGeneticaRequest request)
    {
        try
        {
            // Buscar en la tabla de guías genéticas
            var guia = await _ctx.ProduccionAvicolaRaw
                .Where(p => p.Raza == request.Raza && 
                           p.AnioGuia == request.AnoTabla.ToString() && 
                           p.Edad == request.Edad.ToString())
                .FirstOrDefaultAsync();

            if (guia == null)
            {
                return new GuiaGeneticaResponse(
                    Existe: false,
                    Datos: null,
                    Mensaje: $"No se encontró guía genética para Raza: {request.Raza}, Año: {request.AnoTabla}, Edad: {request.Edad}"
                );
            }

            // Parsear los valores de la guía genética
            var datos = new GuiaGeneticaDto(
                Edad: request.Edad,
                ConsumoHembras: ParseDouble(guia.GrAveDiaH),
                ConsumoMachos: ParseDouble(guia.GrAveDiaM),
                PesoHembras: ParseDouble(guia.PesoH),
                PesoMachos: ParseDouble(guia.PesoM),
                MortalidadHembras: ParseDouble(guia.MortSemH),
                MortalidadMachos: ParseDouble(guia.MortSemM),
                Uniformidad: ParseDouble(guia.Uniformidad),
                PisoTermicoRequerido: DeterminarPisoTermico(request.Edad, guia),
                Observaciones: $"Guía: {guia.Raza} {guia.AnioGuia}"
            );

            return new GuiaGeneticaResponse(
                Existe: true,
                Datos: datos,
                Mensaje: "Guía genética encontrada exitosamente"
            );
        }
        catch (Exception ex)
        {
            return new GuiaGeneticaResponse(
                Existe: false,
                Datos: null,
                Mensaje: $"Error al obtener guía genética: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Obtiene múltiples edades de una guía genética
    /// </summary>
    public async Task<IEnumerable<GuiaGeneticaDto>> ObtenerGuiaGeneticaRangoAsync(string raza, int anoTabla, int edadDesde, int edadHasta)
    {
        var guias = await _ctx.ProduccionAvicolaRaw
            .Where(p => p.Raza == raza && 
                       p.AnioGuia == anoTabla.ToString())
            .ToListAsync();

        return guias
            .Where(p => int.TryParse(p.Edad, out var edad) && 
                       edad >= edadDesde && edad <= edadHasta)
            .OrderBy(p => int.Parse(p.Edad ?? "0"))
            .Select(g => new GuiaGeneticaDto(
                Edad: int.Parse(g.Edad ?? "0"),
                ConsumoHembras: ParseDouble(g.GrAveDiaH),
                ConsumoMachos: ParseDouble(g.GrAveDiaM),
                PesoHembras: ParseDouble(g.PesoH),
                PesoMachos: ParseDouble(g.PesoM),
                MortalidadHembras: ParseDouble(g.MortSemH),
                MortalidadMachos: ParseDouble(g.MortSemM),
                Uniformidad: ParseDouble(g.Uniformidad),
                PisoTermicoRequerido: DeterminarPisoTermico(int.Parse(g.Edad ?? "0"), g),
                Observaciones: $"Guía: {g.Raza} {g.AnioGuia}"
            ));
    }

    /// <summary>
    /// Verifica si existe una guía genética para los parámetros dados
    /// </summary>
    public async Task<bool> ExisteGuiaGeneticaAsync(string raza, int anoTabla)
    {
        return await _ctx.ProduccionAvicolaRaw
            .AnyAsync(p => p.Raza == raza && p.AnioGuia == anoTabla.ToString());
    }

    /// <summary>
    /// Obtiene las razas disponibles en las guías genéticas
    /// </summary>
    public async Task<IEnumerable<string>> ObtenerRazasDisponiblesAsync()
    {
        var razas = await _ctx.ProduccionAvicolaRaw
            .Where(p => !string.IsNullOrEmpty(p.Raza))
            .Select(p => p.Raza!)
            .Distinct()
            .ToListAsync();

        // Filtrar solo las razas que parecen ser códigos de guía genética válidos
        // Mantener todas las razas disponibles en la base de datos
        var razasValidas = razas
            .Where(raza => !string.IsNullOrEmpty(raza) && 
                          raza.Trim().Length >= 2)
            .Select(raza => raza.Trim())
            .OrderBy(r => r)
            .ToList();

        return razasValidas;
    }

    /// <summary>
    /// Obtiene los años disponibles para una raza específica
    /// </summary>
    public async Task<IEnumerable<int>> ObtenerAnosDisponiblesAsync(string raza)
    {
        var anos = await _ctx.ProduccionAvicolaRaw
            .Where(p => p.Raza == raza && !string.IsNullOrEmpty(p.AnioGuia))
            .Select(p => p.AnioGuia!)
            .Distinct()
            .ToListAsync();

        // Parsear de forma segura y filtrar solo los años válidos
        var anosValidos = anos
            .Where(ano => int.TryParse(ano, out _))
            .Select(ano => int.Parse(ano))
            .OrderByDescending(a => a)
            .ToList();

        return anosValidos;
    }

    // ================== MÉTODOS PRIVADOS ==================

    /// <summary>
    /// Parsea un string a double de forma segura
    /// </summary>
    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0.0;

        // Remover caracteres no numéricos excepto punto y coma
        var cleanValue = value.Replace(",", ".").Trim();
        
        if (double.TryParse(cleanValue, out var result))
            return result;

        return 0.0;
    }

    /// <summary>
    /// Determina si se requiere piso térmico basado en la edad y datos de la guía
    /// </summary>
    private static bool DeterminarPisoTermico(int edad, ProduccionAvicolaRaw guia)
    {
        // Lógica para determinar piso térmico:
        // 1. Si la edad es menor o igual a 3 semanas
        // 2. O si hay información específica en la guía
        
        if (edad <= 3)
            return true;

        // Verificar si hay información específica en campos adicionales
        // Por ejemplo, si Valor1000 contiene información sobre temperatura
        if (!string.IsNullOrEmpty(guia.Valor1000))
        {
            var valor = guia.Valor1000.ToLower();
            if (valor.Contains("termico") || valor.Contains("calor") || valor.Contains("temperatura"))
                return true;
        }

        return false;
    }
}

// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Operaciones.cs
// Despacho por tipo. Estructura (Estructura.cs) y Seguimientos (Historicos.cs) implementados;
// Ventas/Movimientos (Fase 3) lanzan NotImplementedException → 501 en el controller.
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    public async Task<IReadOnlyList<LoteElegibleDto>> GetElegiblesAsync(TipoMigracion tipo, MigracionContextoDto contexto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return tipo switch
        {
            TipoMigracion.SeguimientoLevante or TipoMigracion.SeguimientoProduccion
                => await ElegiblesHistoricosAsync(tipo, companyId, contexto, ct),
            TipoMigracion.SeguimientoPolloEngorde or TipoMigracion.VentaPolloEngorde
                => await ElegiblesEngordeAsync(companyId, contexto, ct),
            TipoMigracion.SeguimientoReproductoraEngorde
                => await ElegiblesReproductoraEngordeAsync(companyId, contexto, ct),
            _ => throw new NotImplementedException($"Los lotes elegibles para '{tipo}' se implementan en la Fase 3.")
        };
    }

    public async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaAsync(TipoMigracion tipo, MigracionContextoDto contexto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return tipo switch
        {
            TipoMigracion.Granjas  => await GenerarPlantillaGranjasAsync(companyId, ct),
            TipoMigracion.Nucleos  => await GenerarPlantillaNucleosAsync(companyId, ct),
            TipoMigracion.Galpones => await GenerarPlantillaGalponesAsync(companyId, ct),
            TipoMigracion.SeguimientoLevante or TipoMigracion.SeguimientoProduccion
                => await GenerarPlantillaSeguimientoAsync(tipo, companyId, contexto, ct),
            TipoMigracion.LotesPolloEngorde       => await GenerarPlantillaLotesPolloEngordeAsync(companyId, ct),
            TipoMigracion.SeguimientoPolloEngorde => await GenerarPlantillaSeguimientoEngordeAsync(companyId, contexto, ct),
            TipoMigracion.SeguimientoReproductoraEngorde => await GenerarPlantillaSeguimientoReproductoraAsync(companyId, contexto, ct),
            TipoMigracion.VentaPolloEngorde       => await GenerarPlantillaVentaEngordeAsync(companyId, contexto, ct),
            _ => throw new NotImplementedException($"La plantilla de '{tipo}' se implementa en su fase correspondiente.")
        };
    }

    /// <summary>Valida el archivo (dry-run): nunca inserta, siempre reporta el listado completo de errores.</summary>
    public Task<MigracionResultDto> ValidarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, CancellationToken ct = default)
        => ProcesarAsync(tipo, file, contexto, dryRun: true, permitirParcial: false, ct);

    /// <summary>
    /// Importa el archivo: valida y, si no hay errores, inserta de forma masiva. Si
    /// <paramref name="permitirParcial"/> es true y hay ≥1 fila válida junto a filas con error,
    /// inserta SOLO las válidas (Estado "ProcesadoParcial"); por defecto (false) se mantiene el
    /// comportamiento all-or-nothing: cualquier error real cancela la importación completa.
    /// </summary>
    public Task<MigracionResultDto> ImportarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, bool permitirParcial, CancellationToken ct = default)
        => ProcesarAsync(tipo, file, contexto, dryRun: false, permitirParcial, ct);

    private async Task<MigracionResultDto> ProcesarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, bool dryRun, bool permitirParcial, CancellationToken ct)
    {
        ValidarArchivo(file);
        var companyId = await GetEffectiveCompanyIdAsync();

        var sw = Stopwatch.StartNew();
        var result = tipo switch
        {
            TipoMigracion.Granjas  => await ProcesarGranjasAsync(file, dryRun, permitirParcial, companyId, ct),
            TipoMigracion.Nucleos  => await ProcesarNucleosAsync(file, dryRun, permitirParcial, companyId, ct),
            TipoMigracion.Galpones => await ProcesarGalponesAsync(file, dryRun, permitirParcial, companyId, ct),
            TipoMigracion.SeguimientoLevante    => await ProcesarSeguimientoLevanteAsync(file, dryRun, permitirParcial, companyId, contexto, ct),
            TipoMigracion.SeguimientoProduccion => await ProcesarSeguimientoProduccionAsync(file, dryRun, permitirParcial, companyId, contexto, ct),
            TipoMigracion.LotesPolloEngorde       => await ProcesarLotesPolloEngordeAsync(file, dryRun, permitirParcial, companyId, ct),
            TipoMigracion.SeguimientoPolloEngorde => await ProcesarSeguimientoEngordeAsync(file, dryRun, permitirParcial, companyId, contexto, ct),
            TipoMigracion.SeguimientoReproductoraEngorde => await ProcesarSeguimientoReproductoraAsync(file, dryRun, permitirParcial, companyId, contexto, ct),
            TipoMigracion.VentaPolloEngorde       => await ProcesarVentaEngordeAsync(file, dryRun, permitirParcial, companyId, contexto, ct),
            _ => throw new NotImplementedException($"La migración de '{tipo}' se implementa en su fase correspondiente.")
        };
        sw.Stop();
        result = result with { DuracionMs = sw.ElapsedMilliseconds };

        // Auditoría CENTRAL: se registra toda corrida (dry-run o real, exitosa o no) exactamente una vez.
        await RegistrarAuditoriaAsync(tipo, companyId, file.FileName, result, ct);
        return result;
    }
}

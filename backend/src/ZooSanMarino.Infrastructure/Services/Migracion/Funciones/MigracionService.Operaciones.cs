// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Operaciones.cs
// Despacho por tipo. Estructura (Estructura.cs) y Seguimientos (Historicos.cs) implementados;
// Ventas/Movimientos (Fase 3) lanzan NotImplementedException → 501 en el controller.
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
            TipoMigracion.VentaPolloEngorde       => await GenerarPlantillaVentaEngordeAsync(companyId, contexto, ct),
            _ => throw new NotImplementedException($"La plantilla de '{tipo}' se implementa en su fase correspondiente.")
        };
    }

    public Task<MigracionResultDto> ValidarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, CancellationToken ct = default)
        => ProcesarAsync(tipo, file, contexto, dryRun: true, ct);

    public Task<MigracionResultDto> ImportarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, CancellationToken ct = default)
        => ProcesarAsync(tipo, file, contexto, dryRun: false, ct);

    private async Task<MigracionResultDto> ProcesarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, bool dryRun, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return tipo switch
        {
            TipoMigracion.Granjas  => await ProcesarGranjasAsync(file, dryRun, companyId, ct),
            TipoMigracion.Nucleos  => await ProcesarNucleosAsync(file, dryRun, companyId, ct),
            TipoMigracion.Galpones => await ProcesarGalponesAsync(file, dryRun, companyId, ct),
            TipoMigracion.SeguimientoLevante    => await ProcesarSeguimientoLevanteAsync(file, dryRun, companyId, contexto, ct),
            TipoMigracion.SeguimientoProduccion => await ProcesarSeguimientoProduccionAsync(file, dryRun, companyId, contexto, ct),
            TipoMigracion.LotesPolloEngorde       => await ProcesarLotesPolloEngordeAsync(file, dryRun, companyId, ct),
            TipoMigracion.SeguimientoPolloEngorde => await ProcesarSeguimientoEngordeAsync(file, dryRun, companyId, contexto, ct),
            TipoMigracion.VentaPolloEngorde       => await ProcesarVentaEngordeAsync(file, dryRun, companyId, contexto, ct),
            _ => throw new NotImplementedException($"La migración de '{tipo}' se implementa en su fase correspondiente.")
        };
    }
}

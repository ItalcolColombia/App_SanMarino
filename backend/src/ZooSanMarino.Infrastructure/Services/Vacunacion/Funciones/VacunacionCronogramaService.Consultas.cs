// Vacunacion/Funciones/VacunacionCronogramaService.Consultas.cs
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionCronogramaService
{
    /// <inheritdoc />
    /// <remarks>UN solo round trip: fn_vacunacion_cronograma_lote resuelve el par Levante↔Producción,
    /// los joins (vacuna/lote/granja/registro/usuarios) y la franja calculada + ordenada en SQL
    /// (antes eran ~6 consultas encadenadas y cálculo en C#). El mapeo fila→DTO es puro
    /// (VacunacionCronogramaMapper), incluida la excepción de franja imposible.</remarks>
    public async Task<List<VacunacionCronogramaItemDto>> GetCronogramaLoteAsync(VacunacionCronogramaLoteRequest req, CancellationToken ct = default)
    {
        if (!LineasValidas.Contains(req.LineaProductiva))
            throw new InvalidOperationException($"lineaProductiva inválida: '{req.LineaProductiva}'.");

        var pCompany = new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = _currentUser.CompanyId };
        var pLinea = new NpgsqlParameter("p_linea_productiva", NpgsqlDbType.Text) { Value = req.LineaProductiva };
        var pLote = new NpgsqlParameter("p_lote_id", NpgsqlDbType.Integer) { Value = req.LoteId };

        const string sql =
            "SELECT * FROM public.fn_vacunacion_cronograma_lote(@p_company_id, @p_linea_productiva, @p_lote_id)";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionCronogramaItemRow>(sql, pCompany, pLinea, pLote)
            .ToListAsync(ct);

        return rows.Select(VacunacionCronogramaMapper.ToDto).ToList();
    }
}

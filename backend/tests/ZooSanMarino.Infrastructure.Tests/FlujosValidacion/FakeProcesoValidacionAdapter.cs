using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Tests.FlujosValidacion;

/// <summary>
/// Doble de prueba de <see cref="IProcesoValidacionAdapter"/>: no toca ningún seguimiento real, solo
/// registra las llamadas para que el test verifique que el motor las hizo. Sustituye a los
/// adaptadores concretos de Levante/Producción, que requieren filas reales de
/// <c>seguimiento_diario</c>/<c>seguimiento_diario_produccion</c> fuera del alcance de este harness
/// liviano (sin Postgres real).
/// </summary>
public sealed class FakeProcesoValidacionAdapter : IProcesoValidacionAdapter
{
    public string AdapterKey { get; }
    public int CompanyIdARetornar { get; set; }
    public ResumenRecursoValidacion? ResumenARetornar { get; set; }

    public List<string> RecursosFinalizados { get; } = new();
    public List<string> RecursosLiberados { get; } = new();

    public FakeProcesoValidacionAdapter(string adapterKey) => AdapterKey = adapterKey;

    public Task<int?> ResolverCompanyIdAsync(string recursoId, CancellationToken ct = default) =>
        Task.FromResult<int?>(CompanyIdARetornar);

    public Task<ResumenRecursoValidacion?> ResumirAsync(string recursoId, CancellationToken ct = default) =>
        Task.FromResult(ResumenARetornar);

    public Task FinalizarAsync(string recursoId, CancellationToken ct = default)
    {
        RecursosFinalizados.Add(recursoId);
        return Task.CompletedTask;
    }

    public Task LiberarAsync(string recursoId, CancellationToken ct = default)
    {
        RecursosLiberados.Add(recursoId);
        return Task.CompletedTask;
    }
}

using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Tests.FlujosValidacion;

/// <summary>Doble de prueba mínimo de <see cref="ICurrentUser"/>, mutable para simular distintos
/// usuarios/empresas dentro del mismo test sin reconstruir el service.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public int CompanyId { get; set; }
    public int UserId { get; set; } = 1;
    public int? PaisId { get; set; }
    public string? ActiveCompanyName { get; set; }
    public Guid? UserGuid { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = new List<string> { "flujos_validacion.gestionar", "flujos_validacion.ver" };
    public bool EsAdminEmpresas { get; set; }
}

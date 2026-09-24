using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Tests.FlujosValidacion;

/// <summary>
/// <see cref="ZooSanMarinoContext"/> es el contexto de PRODUCCIÓN completo: al construir el modelo
/// para CUALQUIER test, EF valida TODAS las entidades registradas, no solo las que el test toca.
/// Varias (ajenas a flujos de validación, p. ej. <c>SeguimientoDiario.Metadata</c>) usan
/// <c>System.Text.Json.JsonDocument</c>, que Npgsql mapea nativamente a <c>jsonb</c> pero que el
/// proveedor InMemory no sabe construir (no tiene constructor público bindable) y revienta al
/// inicializar el modelo. Este converter genérico —aplicado a CUALQUIER propiedad JsonDocument del
/// modelo, la toque o no este test— es lo mínimo para que el contexto real arranque bajo InMemory.
/// </summary>
public class TestZooSanMarinoContext : ZooSanMarinoContext
{
    public TestZooSanMarinoContext(DbContextOptions<ZooSanMarinoContext> options) : base(options) { }

    /// <summary>
    /// Debe registrarse ACÁ (no en <c>OnModelCreating</c>): sin un conversor conocido de antemano,
    /// EF ni siquiera clasifica una propiedad <c>JsonDocument</c> como escalar — la deja pendiente
    /// como candidata a entidad poseída/navegación, así que un <c>SetValueConverter</c> tardío en
    /// <c>OnModelCreating</c> no llega a tiempo y la finalización del modelo revienta igual.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<JsonDocument>().HaveConversion<JsonDocumentToStringConverter>();
    }

    private sealed class JsonDocumentToStringConverter : ValueConverter<JsonDocument, string>
    {
        public JsonDocumentToStringConverter() : base(
            v => v.RootElement.GetRawText(),
            v => JsonDocument.Parse(v, default))
        {
        }
    }
}

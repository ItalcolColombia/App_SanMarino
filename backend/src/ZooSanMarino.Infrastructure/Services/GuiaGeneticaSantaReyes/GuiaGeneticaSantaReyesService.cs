// src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaSantaReyes/GuiaGeneticaSantaReyesService.cs
// Partial 'ancla': usings, campos, ctor, constantes, helpers estáticos compartidos y la interfaz.
// El resto vive en Funciones/ (Crud, Import) — misma clase, mismo namespace PLANO.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Guía genética REDUCIDA (<c>guia_genetica_santa_reyes</c>): la puerta de escritura que la tabla
/// nunca tuvo.
///
/// <para>
/// <b>Invariantes del módulo</b> (los tres nacen de un defecto real del módulo compartido):
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Clave natural</b> <c>codigo_guia_genetica = Raza+AnioGuia+Edad</c>, calculada por
///     <see cref="GuiaGeneticaSantaReyesCalculos.CodigoNatural"/> y <b>recalculada</b> ante cualquier
///     cambio de esos tres. Es lo que hace el import idempotente contra el UNIQUE parcial
///     <c>ux_guia_genetica_santa_reyes_codigo</c>. En la tabla compartida, 644 de 1128 filas tienen
///     el código en NULL y el reimport las reinserta en silencio.
///   </description></item>
///   <item><description>
///     <b>Baja SUAVE</b>: <c>DeletedAt</c>, jamás <c>Remove()</c>. El UNIQUE está filtrado por
///     <c>deleted_at IS NULL</c> precisamente para que dar de baja no bloquee recrear el código.
///   </description></item>
///   <item><description>
///     <b>Vacío ⇒ NULL, nunca 0</b> en las tres métricas. La raza Criolla tiene 40 filas legítimamente
///     nulas (semanas 101–140): un 0 ahí diría «puso cero huevos» en vez de «no hay guía».
///   </description></item>
/// </list>
///
/// <para>
/// Todo queda scopeado por la <b>empresa efectiva</b>: empresa activa ya validada por
/// <c>ActiveCompanyMiddleware</c> (<c>ICurrentUser.ActiveCompanyName</c>) y, si no resuelve, el
/// <c>CompanyId</c> del token. Es el mismo <c>GetEffectiveCompanyIdAsync</c> de
/// <c>ProduccionAvicolaRawService</c> y <c>GuiaGeneticaService</c>: la empresa NUNCA sale de un
/// header crudo.
/// </para>
/// </summary>
public partial class GuiaGeneticaSantaReyesService : IGuiaGeneticaSantaReyesService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public GuiaGeneticaSantaReyesService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    /// <summary>Tope del archivo de import, igual que el del módulo compartido.</summary>
    private const long MaxTamanoArchivoBytes = 10 * 1024 * 1024;

    /// <summary>Extensiones admitidas por el import, iguales a las del módulo compartido.</summary>
    private static readonly string[] ExtensionesAdmitidas = { ".xlsx", ".xls" };

    /// <summary>
    /// Empresa efectiva de la request. Prioridad: nombre de la empresa activa (ya <b>validado</b>
    /// por <c>ActiveCompanyMiddleware</c>, no es el header crudo) ⇒ <c>CompanyId</c> del token.
    /// Copiado tal cual de <c>ProduccionAvicolaRawService</c> / <c>GuiaGeneticaService</c> para que
    /// los tres módulos de guía resuelvan la empresa exactamente igual.
    /// </summary>
    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (cid.HasValue) return cid.Value;
        }

        return _currentUser.CompanyId;
    }

    /// <summary>Filas VIVAS de la empresa (la baja es suave: <c>deleted_at</c>).</summary>
    private IQueryable<GuiaGeneticaSantaReyes> Vivas(int companyId) =>
        _ctx.GuiaGeneticaSantaReyes.Where(g => g.CompanyId == companyId && g.DeletedAt == null);

    private static GuiaGeneticaSantaReyesDto MapToDto(GuiaGeneticaSantaReyes e) =>
        new(
            e.Id,
            e.CompanyId,
            e.Raza,
            e.AnioGuia,
            e.Edad,
            e.ProdPorcentaje,
            e.RetiroAcH,
            e.GrAveDiaH,
            e.CodigoGuiaGenetica,
            e.CreatedAt,
            e.UpdatedAt);

    /// <summary>Proyección para las consultas: se traduce a SQL, no materializa la entidad.</summary>
    private static System.Linq.Expressions.Expression<Func<GuiaGeneticaSantaReyes, GuiaGeneticaSantaReyesDto>> MapToDtoExpression() =>
        e => new GuiaGeneticaSantaReyesDto(
            e.Id,
            e.CompanyId,
            e.Raza,
            e.AnioGuia,
            e.Edad,
            e.ProdPorcentaje,
            e.RetiroAcH,
            e.GrAveDiaH,
            e.CodigoGuiaGenetica,
            e.CreatedAt,
            e.UpdatedAt);

    /// <summary>
    /// Escribe raza/año/edad y <b>recalcula el código</b>. Un solo lugar para los tres campos que
    /// forman la clave natural: si se asignaran a mano en el alta y en la edición, tarde o temprano
    /// una de las dos se olvidaría de recalcular y la fila quedaría con el código del valor viejo.
    /// </summary>
    private static void AplicarClaveNatural(GuiaGeneticaSantaReyes entidad, string raza, string anioGuia, int edad)
    {
        entidad.Raza = raza.Trim();
        entidad.AnioGuia = anioGuia.Trim();
        entidad.Edad = edad;
        entidad.CodigoGuiaGenetica = GuiaGeneticaSantaReyesCalculos.CodigoNatural(entidad.Raza, entidad.AnioGuia, edad);
    }

    /// <summary>
    /// ¿Hay otra línea VIVA de la empresa con este código? Se consulta antes de guardar para poder
    /// devolver un mensaje legible en vez del error crudo del UNIQUE
    /// <c>ux_guia_genetica_santa_reyes_codigo</c>.
    /// </summary>
    private Task<bool> ExisteCodigoAsync(int companyId, string? codigo, int idExcluido, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(codigo)
            ? Task.FromResult(false)
            : Vivas(companyId).AnyAsync(g => g.CodigoGuiaGenetica == codigo && g.Id != idExcluido, ct);

    /// <summary>Mensaje del choque de clave natural, con el código para que el usuario lo ubique.</summary>
    private static string MensajeCodigoDuplicado(string? codigo) =>
        $"Ya existe una línea de guía genética con el código «{codigo}» " +
        "(la clave es raza + año + semana). Edite la línea existente en vez de crear otra.";
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Correos;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Adaptador del módulo de tickets. Calca el patrón de <c>LesionService</c>:
/// resuelve la empresa efectiva con <see cref="ICompanyResolver"/> y toma país/usuario
/// de <see cref="ICurrentUser"/>. Ningún listado materializa <c>imagen_base64</c>.
/// </summary>
/// <remarks>
/// Archivo ANCLA del partial: usings, campos, constructor, helpers compartidos y la interfaz.
/// La gestión tipo tablero (prioridad, planificación, tablero, roadmap, timeline, métricas) vive
/// en <c>Tickets/Funciones/TicketService.Gestion.cs</c>.
/// </remarks>
public partial class TicketService : ITicketService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;
    private readonly IEmailQueueService _emailQueue;
    private readonly IConfiguration _configuration;
    private readonly string _logoUrl;
    private readonly string _logoSecundarioUrl;
    private readonly string _brandName;
    private readonly string _brandTagline;
    private readonly string _applicationUrl;

    public TicketService(ZooSanMarinoContext ctx, ICurrentUser currentUser,
        ICompanyResolver companyResolver, IEmailQueueService emailQueue, IConfiguration configuration)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
        _emailQueue = emailQueue;
        _configuration = configuration;
        _applicationUrl = _configuration["Email:ApplicationUrl"] ?? "http://localhost:4200";
        _brandName = _configuration["Email:BrandName"] ?? "ItalGranja";
        _brandTagline = _configuration["Email:Tagline"] ?? "Gestión de granjas avícolas · Italcol";
        // Encabezado con los logos de la pantalla de ingreso (Italcol + San Marino), no el de
        // Italfoods que se usaba antes y no aparece en ninguna pantalla de la aplicación.
        _logoUrl = EmailMarca.LogoPrincipal(_applicationUrl, _configuration["Email:LogoUrl"]);
        _logoSecundarioUrl = EmailMarca.LogoSecundario(_applicationUrl, _configuration["Email:LogoSecundarioUrl"]);
    }

    private string BrandLine => $"{_brandName} · {_brandTagline}";

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
    }
}

using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CompanyService : ICompanyService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPermissionService _userPermissionService;

    public CompanyService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        IUserPermissionService userPermissionService)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _userPermissionService = userPermissionService;
    }

    // Convierte entidad a DTO; requiere que c.Logo esté cargado (Include o eager)
    private static CompanyDto ToDto(Company c) => new CompanyDto(
        c.Id,
        c.Name,
        c.Identifier,
        c.DocumentType,
        c.Address,
        c.Phone,
        c.Email,
        c.Country,
        c.State,
        c.City,
        CompanyCalculos.BuildLogoDataUrl(c.Logo?.LogoBytes, c.Logo?.LogoContentType),
        c.MobileAccess,
        c.VisualPermissions ?? Array.Empty<string>(),
        c.ManejaAlimentoPorGalpon
    );
}

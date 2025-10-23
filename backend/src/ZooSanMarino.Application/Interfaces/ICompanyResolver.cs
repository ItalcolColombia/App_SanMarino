// src/ZooSanMarino.Application/Interfaces/ICompanyResolver.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para resolver información de empresas por nombre
/// </summary>
public interface ICompanyResolver
{
    /// <summary>
    /// Obtiene el ID de una empresa por su nombre
    /// </summary>
    Task<int?> GetCompanyIdByNameAsync(string companyName);

    /// <summary>
    /// Obtiene información completa de una empresa por su nombre
    /// </summary>
    Task<CompanyDto?> GetCompanyByNameAsync(string companyName);

    /// <summary>
    /// Verifica si una empresa existe y está activa
    /// </summary>
    Task<bool> IsCompanyValidAsync(string companyName);

    /// <summary>
    /// Obtiene todas las empresas disponibles para un usuario
    /// </summary>
    Task<IEnumerable<CompanyDto>> GetCompaniesForUserAsync(int userId);
}

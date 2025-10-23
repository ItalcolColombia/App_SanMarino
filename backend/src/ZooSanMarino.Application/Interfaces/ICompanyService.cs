// src/ZooSanMarino.Application/Interfaces/ICompanyService.cs
using ZooSanMarino.Application.DTOs;
namespace ZooSanMarino.Application.Interfaces;
public interface ICompanyService
{
    Task<IEnumerable<CompanyDto>> GetAllAsync();
    Task<IEnumerable<CompanyDto>> GetAllForAdminAsync(); // Nuevo método para administración
    Task<CompanyDto?>            GetByIdAsync(int id);
    Task<CompanyDto>             CreateAsync(CreateCompanyDto dto);
    Task<CompanyDto?>            UpdateAsync(UpdateCompanyDto dto);
    Task<bool>                   DeleteAsync(int id);
}

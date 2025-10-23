// src/ZooSanMarino.Application/Interfaces/IMenuService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IMenuService
{
    Task<IEnumerable<MenuItemDto>> GetTreeAsync();

    // Árbol filtrado por permisos del usuario
    Task<IEnumerable<MenuItemDto>> GetForUserAsync(Guid userId);

    // Árbol filtrado por permisos + scope de compañía (nullable)
    Task<IEnumerable<MenuItemDto>> GetForUserAsync(Guid userId, int? companyId);

    // ABM
    Task<MenuItemDto> CreateAsync(CreateMenuDto dto);
    Task<MenuItemDto?> UpdateAsync(UpdateMenuDto dto);
    Task<bool> DeleteAsync(int id);
}

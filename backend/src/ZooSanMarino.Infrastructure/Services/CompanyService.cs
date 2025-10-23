using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ZooSanMarinoContext _ctx;
        private readonly ICurrentUser _currentUser;
        private readonly IUserPermissionService _userPermissionService;

        public CompanyService(ZooSanMarinoContext ctx, ICurrentUser currentUser, IUserPermissionService userPermissionService)
        {
            _ctx = ctx;
            _currentUser = currentUser;
            _userPermissionService = userPermissionService;
        }

        public async Task<IEnumerable<CompanyDto>> GetAllAsync()
        {
            // Debug: Log de información del usuario actual
            Console.WriteLine($"CompanyService.GetAllAsync - UserId: {_currentUser.UserId}");
            Console.WriteLine($"CompanyService.GetAllAsync - CompanyId: {_currentUser.CompanyId}");
            Console.WriteLine($"CompanyService.GetAllAsync - ActiveCompanyName: '{_currentUser.ActiveCompanyName}'");

            // TEMPORAL: Para pruebas sin autenticación válida, devolver datos de prueba
            if (_currentUser.UserId == 0) // Usuario no autenticado
            {
                Console.WriteLine("CompanyService.GetAllAsync - Usuario no autenticado, devolviendo datos de prueba");
                
                // Si hay empresa activa especificada, devolver empresas con ese nombre
                if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
                {
                    Console.WriteLine($"CompanyService.GetAllAsync - Modo prueba: buscando empresas con nombre '{_currentUser.ActiveCompanyName}'");
                    
                    var companiesWithSameName = await _ctx.Companies
                        .AsNoTracking()
                        .Where(c => c.Name == _currentUser.ActiveCompanyName)
                        .Select(c => new CompanyDto(
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
                            c.MobileAccess,
                            c.VisualPermissions
                        ))
                        .ToListAsync();

                    Console.WriteLine($"CompanyService.GetAllAsync - Empresas con nombre '{_currentUser.ActiveCompanyName}' encontradas: {companiesWithSameName.Count()}");
                    
                    // Si no se encuentran empresas con ese nombre exacto, devolver todas las empresas para prueba
                    if (companiesWithSameName.Count() == 0)
                    {
                        Console.WriteLine("CompanyService.GetAllAsync - No se encontraron empresas con ese nombre, devolviendo todas las empresas para prueba");
                        return await _ctx.Companies
                            .AsNoTracking()
                            .Select(c => new CompanyDto(
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
                                c.MobileAccess,
                                c.VisualPermissions
                            ))
                            .ToListAsync();
                    }
                    
                    return companiesWithSameName;
                }
                
                // Sin empresa activa, devolver todas las empresas para prueba
                Console.WriteLine("CompanyService.GetAllAsync - Modo prueba: devolviendo todas las empresas");
                return await _ctx.Companies
                    .AsNoTracking()
                    .Select(c => new CompanyDto(
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
                        c.MobileAccess,
                        c.VisualPermissions
                    ))
                    .ToListAsync();
            }

            // Verificar si es el super admin
            if (await IsSuperAdminAsync(_currentUser.UserId))
            {
                Console.WriteLine("CompanyService.GetAllAsync - Usuario es super admin, devolviendo todas las empresas");
                // Super admin puede ver todas las empresas
                return await _ctx.Companies
                    .Select(c => new CompanyDto(
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
                        c.MobileAccess,
                        c.VisualPermissions
                    ))
                    .ToListAsync();
            }

            // NUEVA LÓGICA: Si hay empresa activa especificada, devolver todas las empresas con ese nombre
            if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
            {
                Console.WriteLine($"CompanyService.GetAllAsync - Buscando empresas con nombre: '{_currentUser.ActiveCompanyName}'");
                
                var companiesWithSameName = await _ctx.Companies
                    .AsNoTracking()
                    .Where(c => c.Name == _currentUser.ActiveCompanyName)
                    .Select(c => new CompanyDto(
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
                        c.MobileAccess,
                        c.VisualPermissions
                    ))
                    .ToListAsync();

                Console.WriteLine($"CompanyService.GetAllAsync - Empresas con nombre '{_currentUser.ActiveCompanyName}' encontradas: {companiesWithSameName.Count()}");
                foreach (var company in companiesWithSameName)
                {
                    Console.WriteLine($"  - {company.Name} (ID: {company.Id})");
                }

                return companiesWithSameName;
            }

            // Fallback: Obtener solo las empresas asignadas al usuario actual
            Console.WriteLine("CompanyService.GetAllAsync - No hay empresa activa, devolviendo empresas asignadas al usuario");
            
            // Convertir userId de int a Guid para la consulta
            var userIdGuid = new Guid(_currentUser.UserId.ToString("D32").PadLeft(32, '0'));
            Console.WriteLine($"CompanyService.GetAllAsync - UserIdGuid: {userIdGuid}");

            var companies = await _ctx.UserCompanies
                .AsNoTracking()
                .Include(uc => uc.Company)
                .Where(uc => uc.UserId == userIdGuid)
                .Select(uc => new CompanyDto(
                    uc.Company.Id,
                    uc.Company.Name,
                    uc.Company.Identifier,
                    uc.Company.DocumentType,
                    uc.Company.Address,
                    uc.Company.Phone,
                    uc.Company.Email,
                    uc.Company.Country,
                    uc.Company.State,
                    uc.Company.City,
                    uc.Company.MobileAccess,
                    uc.Company.VisualPermissions
                ))
                .ToListAsync();

            Console.WriteLine($"CompanyService.GetAllAsync - Empresas asignadas encontradas: {companies.Count()}");
            foreach (var company in companies)
            {
                Console.WriteLine($"  - {company.Name} (ID: {company.Id})");
            }

            return companies;
        }

        /// <summary>
        /// Obtiene TODAS las empresas sin filtro para administración
        /// </summary>
        public async Task<IEnumerable<CompanyDto>> GetAllForAdminAsync()
        {
            Console.WriteLine("CompanyService.GetAllForAdminAsync - Devolviendo TODAS las empresas para administración");
            
            try
            {
                var companies = await _ctx.Companies
                    .AsNoTracking()
                    .ToListAsync();

                Console.WriteLine($"CompanyService.GetAllForAdminAsync - Empresas encontradas: {companies.Count}");

                var result = companies.Select(c => new CompanyDto(
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
                    c.MobileAccess,
                    c.VisualPermissions
                )).OrderBy(c => c.Name).ToList();

                Console.WriteLine($"CompanyService.GetAllForAdminAsync - DTOs creados: {result.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CompanyService.GetAllForAdminAsync - Error: {ex.Message}");
                Console.WriteLine($"CompanyService.GetAllForAdminAsync - StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<CompanyDto?> GetByIdAsync(int id) =>
            await _ctx.Companies
                .Where(c => c.Id == id)
                .Select(c => new CompanyDto(
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
                    c.MobileAccess,
                    c.VisualPermissions
                ))
                .SingleOrDefaultAsync();

        public async Task<CompanyDto> CreateAsync(CreateCompanyDto dto)
        {
            var c = new Company
            {
                Name              = dto.Name,
                Identifier        = dto.Identifier,
                DocumentType      = dto.DocumentType,
                Address           = dto.Address,
                Phone             = dto.Phone,
                Email             = dto.Email,
                Country           = dto.Country,
                State             = dto.State,
                City              = dto.City,
                VisualPermissions = dto.VisualPermissions,
                MobileAccess      = dto.MobileAccess
            };
            _ctx.Companies.Add(c);
            await _ctx.SaveChangesAsync();
            var result = await GetByIdAsync(c.Id);
            if (result is null)
                throw new InvalidOperationException("Created company could not be retrieved.");
            return result;
        }

        public async Task<CompanyDto?> UpdateAsync(UpdateCompanyDto dto)
        {
            var c = await _ctx.Companies.FindAsync(dto.Id);
            if (c is null) return null;

            c.Name              = dto.Name;
            c.Identifier        = dto.Identifier;
            c.DocumentType      = dto.DocumentType;
            c.Address           = dto.Address;
            c.Phone             = dto.Phone;
            c.Email             = dto.Email;
            c.Country           = dto.Country;
            c.State             = dto.State;
            c.City              = dto.City;
            c.VisualPermissions = dto.VisualPermissions;
            c.MobileAccess      = dto.MobileAccess;

            await _ctx.SaveChangesAsync();
            return await GetByIdAsync(c.Id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var c = await _ctx.Companies.FindAsync(id);
            if (c is null) return false;
            _ctx.Companies.Remove(c);
            await _ctx.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Verifica si el usuario es el super admin (moiesbbuga@gmail.com)
        /// </summary>
        private async Task<bool> IsSuperAdminAsync(int userId)
        {
            // Convertir userId de int a Guid para la consulta
            var userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
            
            // Buscar el email del usuario
            var userEmail = await _ctx.UserLogins
                .AsNoTracking()
                .Include(ul => ul.Login)
                .Where(ul => ul.UserId == userIdGuid)
                .Select(ul => ul.Login.email)
                .FirstOrDefaultAsync();

            return userEmail?.ToLower() == "moiesbbuga@gmail.com";
        }
    }
}

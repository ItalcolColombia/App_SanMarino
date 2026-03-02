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
            // Verificar si el usuario es admin o administrador
            var isAdmin = await IsUserAdminOrAdministratorAsync(_currentUser.UserId);
            var isSuperAdmin = await IsSuperAdminAsync(_currentUser.UserId);
            
            Console.WriteLine($"CompanyService.GetAllAsync - UserId: {_currentUser.UserId}");
            Console.WriteLine($"CompanyService.GetAllAsync - IsSuperAdmin: {isSuperAdmin}");
            Console.WriteLine($"CompanyService.GetAllAsync - IsAdmin: {isAdmin}");
            
            // Si es super admin o admin/administrador, devolver TODAS las empresas
            if (isSuperAdmin || isAdmin)
            {
                Console.WriteLine($"CompanyService.GetAllAsync - Usuario es super admin o admin/administrador, devolviendo TODAS las empresas");
                var allCompanies = await _ctx.Companies
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();
                return allCompanies.Select(ToDto).ToList();
            }

            // Si NO es admin/administrador, devolver solo las empresas asignadas al usuario
            Console.WriteLine($"CompanyService.GetAllAsync - Usuario NO es admin, devolviendo solo empresas asignadas al usuario");
            
            // Obtener el Guid del usuario (preferir UserGuid del token, sino convertir desde int)
            var userIdGuid = _currentUser.UserGuid;
            if (!userIdGuid.HasValue)
            {
                // Fallback: convertir desde int (no ideal, pero necesario para compatibilidad)
                userIdGuid = new Guid(_currentUser.UserId.ToString("D32").PadLeft(32, '0'));
            }

            var companies = await _ctx.UserCompanies
                .AsNoTracking()
                .Include(uc => uc.Company)
                .Where(uc => uc.UserId == userIdGuid.Value)
                .OrderBy(uc => uc.Company.Name)
                .Select(uc => uc.Company)
                .ToListAsync();

            Console.WriteLine($"CompanyService.GetAllAsync - Empresas asignadas encontradas: {companies.Count()}");
            foreach (var company in companies)
            {
                Console.WriteLine($"  - {company.Name} (ID: {company.Id})");
            }

            return companies.Select(ToDto).ToList();
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

                var result = companies.Select(ToDto).OrderBy(c => c.Name).ToList();

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

        public async Task<CompanyDto?> GetByIdAsync(int id)
        {
            var c = await _ctx.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return c == null ? null : ToDto(c);
        }

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

            if (TryExtractLogo(dto.LogoDataUrl, out var bytes, out var contentType, out var clear) && !clear)
            {
                c.LogoBytes = bytes;
                c.LogoContentType = contentType;
            }

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

            // Logo: null => no cambia; "" => borrar; dataUrl => actualizar
            if (TryExtractLogo(dto.LogoDataUrl, out var bytes, out var contentType, out var clear))
            {
                if (clear)
                {
                    c.LogoBytes = null;
                    c.LogoContentType = null;
                }
                else
                {
                    c.LogoBytes = bytes;
                    c.LogoContentType = contentType;
                }
            }

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
            BuildLogoDataUrl(c.LogoBytes, c.LogoContentType),
            c.MobileAccess,
            c.VisualPermissions ?? Array.Empty<string>()
        );

        private static string? BuildLogoDataUrl(byte[]? bytes, string? contentType)
        {
            if (bytes == null || bytes.Length == 0) return null;
            if (string.IsNullOrWhiteSpace(contentType)) contentType = "image/png";
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }

        /// <summary>
        /// Extrae bytes y content-type de un dataURL. Retorna false si es null (no actualizar).
        /// Si es string vacío, clear=true (borrar).
        /// </summary>
        private static bool TryExtractLogo(string? logoDataUrl, out byte[]? bytes, out string? contentType, out bool clear)
        {
            bytes = null;
            contentType = null;
            clear = false;

            if (logoDataUrl == null) return false; // no cambiar
            if (logoDataUrl.Length == 0) { clear = true; return true; } // borrar

            var s = logoDataUrl.Trim();
            if (s.Length == 0) { clear = true; return true; }

            // Esperado: data:image/png;base64,....
            if (!s.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
            var comma = s.IndexOf(',');
            if (comma < 0) return false;

            var meta = s.Substring(5, comma - 5); // after "data:"
            var data = s[(comma + 1)..];

            // meta: "image/png;base64"
            var metaParts = meta.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (metaParts.Length == 0) return false;
            var ct = metaParts[0];
            var isBase64 = metaParts.Any(p => p.Equals("base64", StringComparison.OrdinalIgnoreCase));
            if (!isBase64) return false;
            if (!ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                var raw = Convert.FromBase64String(data);
                // Logo: limitar tamaño (512 KB)
                if (raw.Length > 512 * 1024) return false;
                bytes = raw;
                contentType = ct;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si el usuario es el super admin (moiesbbuga@gmail.com)
        /// </summary>
        private async Task<bool> IsSuperAdminAsync(int userId)
        {
            // Obtener el Guid del usuario desde ICurrentUser (preferido) o convertir desde int
            var userIdGuid = _currentUser.UserGuid;
            if (!userIdGuid.HasValue)
            {
                // Fallback: convertir desde int (no ideal, pero necesario para compatibilidad)
                userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
            }
            
            // Buscar el email del usuario
            var userEmail = await _ctx.UserLogins
                .AsNoTracking()
                .Include(ul => ul.Login)
                .Where(ul => ul.UserId == userIdGuid.Value)
                .Select(ul => ul.Login.email)
                .FirstOrDefaultAsync();

            return userEmail?.ToLower() == "moiesbbuga@gmail.com";
        }

        /// <summary>
        /// Verifica si el usuario tiene rol "admin" o "administrador" (case-insensitive)
        /// </summary>
        private async Task<bool> IsUserAdminOrAdministratorAsync(int userId)
        {
            // Obtener el Guid del usuario desde ICurrentUser (preferido) o convertir desde int
            var userIdGuid = _currentUser.UserGuid;
            if (!userIdGuid.HasValue)
            {
                // Fallback: convertir desde int (no ideal, pero necesario para compatibilidad)
                userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
            }
            
            Console.WriteLine($"CompanyService.IsUserAdminOrAdministratorAsync - UserIdGuid: {userIdGuid}");
            
            var userRoles = await _ctx.UserRoles
                .AsNoTracking()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userIdGuid.Value)
                .Select(ur => ur.Role.Name)
                .ToListAsync();

            Console.WriteLine($"CompanyService.IsUserAdminOrAdministratorAsync - Roles encontrados: {string.Join(", ", userRoles)}");

            // Verificar si tiene rol "admin", "Admin", "administrador" o "Administrador" (case-insensitive)
            var isAdmin = userRoles.Any(role => 
                !string.IsNullOrWhiteSpace(role) && 
                (role.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                 role.Equals("administrador", StringComparison.OrdinalIgnoreCase))
            );
            
            Console.WriteLine($"CompanyService.IsUserAdminOrAdministratorAsync - IsAdmin result: {isAdmin}");
            
            return isAdmin;
        }
    }
}

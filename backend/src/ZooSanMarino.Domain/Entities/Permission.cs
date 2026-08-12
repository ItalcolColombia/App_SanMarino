using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZooSanMarino.Domain.Entities
{
    public class Permission
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!; // Ej: "user.create"
        public string Description { get; set; } = null!;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        // 👇 navegación inversa (nueva)
        public ICollection<MenuPermission> MenuPermissions { get; set; } = new List<MenuPermission>();
        /// <summary>Empresas que tienen habilitado este permiso (ver <see cref="CompanyPermission"/>).</summary>
        public ICollection<CompanyPermission> CompanyPermissions { get; set; } = new List<CompanyPermission>();
    }

}
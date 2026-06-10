import { Role } from '../../../../core/services/role/role.service';

export function filterRoles(roles: Role[], query: string): Role[] {
  const t = query.trim().toLowerCase();
  if (!t) return roles;
  return roles.filter(r =>
    r.name?.toLowerCase().includes(t) ||
    (r.permissions ?? []).some(p => p.toLowerCase().includes(t))
  );
}

export function getCombinedPermissions(roleIds: number[], rolesMap: Map<number, Role>): string[] {
  const set = new Set<string>();
  for (const id of roleIds) {
    const r = rolesMap.get(id);
    (r?.permissions ?? []).forEach(p => set.add(p.toLowerCase()));
  }
  return Array.from(set).sort();
}

export function getRolePermissions(roleId: number | null, rolesMap: Map<number, Role>): string[] {
  if (!roleId) return [];
  return (rolesMap.get(roleId)?.permissions ?? []).map(p => p.toLowerCase()).sort();
}

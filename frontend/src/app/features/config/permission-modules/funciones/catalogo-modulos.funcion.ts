import type { Permission } from '../../../../core/services/permission/permission.service';
import type { CambioPermisos, PermissionModule } from '../../../../core/services/permission-module/permission-module.service';

/** Filtra el catálogo de permisos por key o descripción (sin distinguir mayúsculas). Devuelve lista nueva. */
export function filtrarPermisosCatalogo(permisos: readonly Permission[], termino: string): Permission[] {
  const t = (termino || '').trim().toLowerCase();
  if (!t) return [...(permisos ?? [])];
  return (permisos ?? []).filter(
    p => (p.key || '').toLowerCase().includes(t) || (p.description || '').toLowerCase().includes(t)
  );
}

/**
 * Para cada key de permiso (minúscula), los NOMBRES de los otros módulos que la contienen — sirve para
 * mostrar que un permiso es compartido mientras se edita un módulo.
 */
export function otrosModulosPorPermiso(
  modulos: readonly PermissionModule[],
  moduloActualId: number | null
): Map<string, string[]> {
  const indice = new Map<string, string[]>();
  for (const m of modulos ?? []) {
    if (m.id === moduloActualId) continue;
    for (const k of m.permissionKeys ?? []) {
      const key = (k || '').toLowerCase();
      indice.set(key, [...(indice.get(key) ?? []), m.nombre]);
    }
  }
  return indice;
}

/** Ids de los permisos del catálogo que el módulo agrupa hoy. */
export function idsDePermisosDelModulo(permisos: readonly Permission[], modulo: PermissionModule | null): Set<number> {
  const keys = new Set((modulo?.permissionKeys ?? []).map(k => (k || '').toLowerCase()));
  return new Set((permisos ?? []).filter(p => keys.has((p.key || '').toLowerCase())).map(p => p.id));
}

/** Mensaje del toast tras un cambio que recalcula `company_permissions`. */
export function describirCambioPermisos(cambio: CambioPermisos | null | undefined): string {
  const prendidos = cambio?.permisosPrendidos ?? 0;
  const apagados = cambio?.permisosApagados ?? 0;
  if (prendidos === 0 && apagados === 0) return 'Guardado. No cambió ningún permiso de las empresas.';
  const empresas = cambio?.empresasAfectadas ?? 0;
  const donde = empresas > 1 ? ` en ${empresas} empresas` : '';
  return `Guardado: ${prendidos} permiso(s) prendido(s) y ${apagados} apagado(s)${donde}.`;
}

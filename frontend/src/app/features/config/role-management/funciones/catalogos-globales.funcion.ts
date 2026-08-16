// features/config/role-management/funciones/catalogos-globales.funcion.ts
//
// Quién puede tocar los CATÁLOGOS GLOBALES del sistema (el catálogo de permisos y el árbol de
// menús). No son configuración del rol: son estructuras compartidas por TODAS las empresas —
// borrar una key de permiso o un ítem de menú acá se lo lleva puesto a todo el mundo.
//
// Función PURA: no usa `this`, no inyecta nada y no toca la sesión. El componente le pasa los roles
// que ya trae `session$`.

/** Los tres tabs de primer nivel del módulo. */
export type TabRoles = 'roles' | 'perms' | 'menus';

/** Tabs que administran un catálogo global y por eso quedan reservados al perfil Admin. */
export const TABS_SOLO_ADMIN: readonly TabRoles[] = ['perms', 'menus'] as const;

/**
 * Nombres de rol que cuentan como «administrador de la aplicación».
 *
 * Comparación EXACTA (case-insensitive), no `includes`: en la base conviven `Admin Panama`,
 * `Admin Demo`, `Ecuador Administrador`, `Santa Reyes Administrador` y `ADMINISTRADOR DE GRANJA`,
 * que son administradores DE SU EMPRESA. Con `includes` todos ellos entrarían al catálogo global,
 * que es exactamente lo que hay que evitar.
 */
const ROLES_ADMIN_APLICACION: readonly string[] = ['admin', 'administrador'] as const;

/**
 * ¿La sesión pertenece al administrador de la aplicación?
 *
 * **Fail-closed**: sin roles, con `null`/`undefined` o si la sesión no se pudo leer, devuelve
 * `false`. Ante la duda el módulo muestra menos, nunca más.
 */
export function esAdminDeAplicacion(
  roles: readonly (string | null | undefined)[] | null | undefined
): boolean {
  if (!roles?.length) return false;
  return roles.some(rol =>
    !!rol && ROLES_ADMIN_APLICACION.includes(rol.trim().toLowerCase())
  );
}

/**
 * ¿Se puede mostrar/activar este tab?
 *
 * `roles` siempre se puede: es la razón de ser del módulo y no toca nada global. `perms` y `menus`
 * solo para el admin de la aplicación.
 */
export function puedeVerTab(tab: TabRoles, esAdminApp: boolean): boolean {
  return TABS_SOLO_ADMIN.includes(tab) ? esAdminApp : true;
}

/**
 * Tab al que hay que caer cuando el actual dejó de estar permitido (p. ej. la sesión cambió de
 * usuario con el módulo abierto). Siempre existe: `roles` no se le niega a nadie.
 */
export function tabPorDefecto(): TabRoles {
  return 'roles';
}

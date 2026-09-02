// features/dashboard/funciones/resolver-paneles-visibles.funcion.ts
//
// Función PURA: decide qué paneles y qué bloques del dashboard ve un usuario.
// Sin `this`, sin DI, sin HTTP. Recibe la sesión ya resuelta y devuelve la lista.

import {
  BloqueId,
  CATALOGO_PANELES,
  DefinicionBloque,
  DefinicionPanel,
  FlagsGating,
  MenuNodo,
  PanelVisible
} from '../models/dashboard-panel.model';

/** Todo lo que hace falta para decidir. Las tres piezas YA viajan en la sesión. */
export interface EntradaGating {
  /** `session.menu` — el árbol efectivo que `fn_menu_usuario` resolvió (role_menus ∩ company_menus). */
  menu: readonly MenuNodo[] | null | undefined;
  /** `session.user.permisos` — las keys de `role_permissions`. */
  permisos: readonly string[] | null | undefined;
  /** Flags de la empresa activa. Ausente ⇒ todo apagado (mismo fail-closed que el servicio). */
  flags: FlagsGating | null | undefined;
}

/**
 * Normaliza una route para comparar: minúsculas, con barra inicial, sin barra final.
 *
 * Se compara texto porque es lo único estable entre entornos: **los ids de menú difieren
 * local↔prod** y sembrar el mapeo con ids fijos es el error que CLAUDE.md prohíbe (§🏢 punto 5).
 */
function normalizarRoute(route: string | null | undefined): string | null {
  if (!route) return null;

  const limpia = route.trim().toLowerCase();
  if (!limpia) return null;

  const conBarra = limpia.startsWith('/') ? limpia : `/${limpia}`;
  // Sin barra final, salvo que la route SEA la raíz.
  return conBarra.length > 1 ? conBarra.replace(/\/+$/, '') : conBarra;
}

/**
 * ¿La route de un menú cubre la del módulo?
 *
 * Cubre si es la misma o si es un descendiente: `/vacunacion/cronograma` cubre `/vacunacion`.
 * La barra del prefijo es deliberada — sin ella `/vacunacion-historica` cubriría `/vacunacion`,
 * que son módulos distintos.
 */
function cubre(routeMenu: string, routeModulo: string): boolean {
  return routeMenu === routeModulo || routeMenu.startsWith(`${routeModulo}/`);
}

/** Aplana el árbol de menú a un set de routes normalizadas, recorriendo todos los niveles. */
export function routesDelMenu(menu: readonly MenuNodo[] | null | undefined): ReadonlySet<string> {
  const routes = new Set<string>();

  const visitar = (nodos: readonly MenuNodo[] | null | undefined): void => {
    if (!nodos) return;
    for (const nodo of nodos) {
      if (!nodo) continue;
      const route = normalizarRoute(nodo.route);
      // Los nodos de agrupación (Configuración, Reportes…) no tienen route: son contenedores.
      if (route) routes.add(route);
      visitar(nodo.children);
    }
  };

  visitar(menu);
  return routes;
}

/** ¿El usuario tiene en su menú alguno de los módulos pedidos? */
function tieneAlgunModulo(
  routesUsuario: ReadonlySet<string>,
  routesModulo: readonly string[]
): boolean {
  if (routesModulo.length === 0) return false;

  for (const pedida of routesModulo) {
    const modulo = normalizarRoute(pedida);
    if (!modulo) continue;
    for (const propia of routesUsuario) {
      if (cubre(propia, modulo)) return true;
    }
  }
  return false;
}

/** ¿El usuario tiene alguno de los permisos pedidos? Sin permisos pedidos ⇒ pasa. */
function tieneAlgunPermiso(
  permisos: readonly string[] | null | undefined,
  pedidos: readonly string[] | undefined
): boolean {
  if (!pedidos || pedidos.length === 0) return true;
  if (!permisos || permisos.length === 0) return false;

  const propios = new Set(permisos.map(p => p?.trim().toLowerCase()).filter(Boolean));
  return pedidos.some(p => propios.has(p.trim().toLowerCase()));
}

/** Lee un flag tratando ausente/null como `false` (fail-closed, igual que el servicio). */
function flagPrendido(flags: FlagsGating | null | undefined, key: keyof FlagsGating): boolean {
  return flags?.[key] === true;
}

/** ¿Este bloque se dibuja para este usuario? */
function bloqueVisible(
  bloque: DefinicionBloque,
  panel: DefinicionPanel,
  routesUsuario: ReadonlySet<string>,
  entrada: EntradaGating
): boolean {
  // Un bloque sin routes propias hereda las del panel: si el panel se abrió, el bloque también.
  const routes = bloque.routesModulo ?? panel.routesModulo;
  if (!tieneAlgunModulo(routesUsuario, routes)) return false;

  if (!tieneAlgunPermiso(entrada.permisos, bloque.requierePermisos)) return false;

  if (bloque.requiereFlag && !flagPrendido(entrada.flags, bloque.requiereFlag)) return false;
  if (bloque.ocultoSiFlag && flagPrendido(entrada.flags, bloque.ocultoSiFlag)) return false;

  return true;
}

/**
 * Los paneles que este usuario ve, en orden estable, cada uno con SÓLO los bloques que le
 * corresponden.
 *
 * ## Garantías
 *
 * - **Fail-closed.** Menú vacío, nulo o sin ninguna route del catálogo ⇒ `[]`. Nunca se devuelve un
 *   panel «por las dudas»: un usuario sin módulos no tiene nada que mirar.
 * - **Un panel sin bloques visibles NO se devuelve.** Dibujar el cascarón vacío de un panel es peor
 *   que no dibujarlo: parece que no hay datos cuando en realidad no hay acceso.
 * - **Orden estable** por `orden` — la página no baila entre recargas.
 *
 * ⚠️ Esto decide lo que se DIBUJA. No es la protección: cada endpoint corta por su cuenta
 * (empresa + alcance del usuario). Ocultar no es proteger.
 */
export function resolverPanelesVisibles(entrada: EntradaGating): PanelVisible[] {
  const routesUsuario = routesDelMenu(entrada?.menu);
  if (routesUsuario.size === 0) return [];

  const visibles: PanelVisible[] = [];

  for (const panel of CATALOGO_PANELES) {
    if (!tieneAlgunModulo(routesUsuario, panel.routesModulo)) continue;

    const bloques = panel.bloques
      .filter(b => bloqueVisible(b, panel, routesUsuario, entrada))
      .map(b => b.id);

    if (bloques.length === 0) continue;

    visibles.push({
      id: panel.id,
      titulo: panel.titulo,
      descripcion: panel.descripcion,
      orden: panel.orden,
      bloques
    });
  }

  return visibles.sort((a, b) => a.orden - b.orden);
}

/** Atajo para el template: ¿este bloque quedó visible? */
export function tieneBloque(panel: PanelVisible | undefined, bloque: BloqueId): boolean {
  return !!panel?.bloques.includes(bloque);
}

// features/dashboard/models/dashboard-panel.model.ts
//
// Catálogo DECLARATIVO de los paneles del dashboard y de los bloques de cada uno.
//
// Es un dato, no código: `resolver-paneles-visibles.funcion.ts` lo recorre y decide. Agregar un
// panel o un bloque es agregar una entrada acá — no un `if` más en una función que crece.

/** Identificador estable de un panel. Se usa como clave de caché y en los tests. */
export type PanelId = 'postura' | 'engorde' | 'alimento-inventario' | 'cumplimiento';

/** Identificador estable de un bloque dentro de un panel. */
export type BloqueId =
  // Postura
  | 'postura.kpis'
  | 'postura.produccion-vs-guia'
  | 'postura.huevos'
  | 'postura.mortalidad'
  | 'postura.sin-validar'
  // Pollo engorde
  | 'engorde.kpis'
  | 'engorde.peso-vs-guia'
  | 'engorde.conversion'
  | 'engorde.ventas'
  // Alimento e inventario
  | 'inventario.stock'
  | 'inventario.descuadres'
  | 'inventario.consumo-vs-ingreso'
  | 'inventario.gastos'
  // Cumplimiento y pendientes
  | 'cumplimiento.vacunacion-pendiente'
  | 'cumplimiento.vacunacion-cumplimiento'
  | 'cumplimiento.cuadres-offline'
  | 'cumplimiento.implementacion';

/**
 * Flags de empresa que el gating consulta. Es un SUBCONJUNTO de `CompanyFlags`
 * (`core/services/company-config/`) a propósito: la función pura no debe depender del servicio ni
 * arrastrar los 17 flags para leer dos. `Partial` porque el llamador puede no tenerlos resueltos —
 * y ausente se trata como `false`, que es el fail-closed del servicio.
 */
export interface FlagsGating {
  /** La empresa clasifica huevos por ÍTEM del catálogo en vez de las 11 columnas fijas. */
  clasificacionHuevoPorItems?: boolean | null;
  /** La empresa no maneja machos en postura: no se dibuja su serie (SR-DEF-1). */
  ocultaMachosEnPostura?: boolean | null;
  /** Los seguimientos exigen doble validación ⇒ existe el estado «pendiente de validar». */
  requiereValidacionSeguimientoDiario?: boolean | null;
}

/**
 * Forma mínima de un nodo de menú. Coincide estructuralmente con `MenuItem` de
 * `core/auth/auth.models.ts` **sin importarlo**: la función pura no debe acoplarse al modelo de auth
 * (y así el test arma nodos con dos campos en vez de seis).
 */
export interface MenuNodo {
  route?: string | null;
  children?: readonly MenuNodo[] | null;
}

/** Un bloque del panel, con su condición de visibilidad. */
export interface DefinicionBloque {
  id: BloqueId;
  titulo: string;
  /**
   * Routes de módulo que habilitan el bloque. Si va vacío, hereda las del panel.
   * Con que el usuario tenga UNA en su menú alcanza.
   */
  routesModulo?: readonly string[];
  /**
   * Permisos de acción (de los 45 que YA existen) que habilitan el bloque. Vacío = sin permiso extra.
   * Con que el usuario tenga UNO alcanza.
   *
   * ⚠️ Acá NO se inventan keys: cada una tiene que existir en `permissions.key`.
   */
  requierePermisos?: readonly string[];
  /** El bloque sólo existe si este flag de empresa está prendido. */
  requiereFlag?: keyof FlagsGating;
  /** El bloque se oculta si este flag de empresa está prendido. */
  ocultoSiFlag?: keyof FlagsGating;
}

/** Un panel del dashboard. */
export interface DefinicionPanel {
  id: PanelId;
  titulo: string;
  descripcion: string;
  /**
   * Routes de módulo que habilitan el panel. Con que el usuario tenga UNA en su menú alcanza.
   *
   * 🔴 **Se localiza por `route`, JAMÁS por id de menú** — los ids difieren local↔prod
   * (CLAUDE.md §🏢 punto 5). Una route que ya no exista simplemente no matchea: no otorga acceso.
   */
  routesModulo: readonly string[];
  /** Orden estable en la página. */
  orden: number;
  bloques: readonly DefinicionBloque[];
}

/** Panel ya resuelto para un usuario concreto: sólo trae los bloques que esa persona ve. */
export interface PanelVisible {
  id: PanelId;
  titulo: string;
  descripcion: string;
  orden: number;
  bloques: readonly BloqueId[];
}

/**
 * El catálogo.
 *
 * ## Cómo se decide quién ve qué — sin permisos nuevos
 *
 * El modelo de acceso del repo ya tiene DOS señales, y las dos son por rol **y** por empresa:
 *
 * - **`role_menus` ∩ `company_menus`** (lo que `fn_menu_usuario` resuelve y viaja en `session.menu`)
 *   = a qué MÓDULOS accede esta persona en esta empresa. Es la señal de *perfil*.
 * - **`role_permissions`** (viaja en `session.user.permisos`) = qué ACCIONES puede ejecutar dentro
 *   del módulo. Es la señal de *nivel*.
 *
 * Hay 68 menús y sólo 45 permisos: la mayoría de los módulos no tiene permiso propio. Gatear sólo
 * por permisos dejaría los paneles invisibles para casi todos —incluido el perfil que sólo mira
 * números—, así que el PANEL lo abre el menú y el permiso queda para los bloques que exponen
 * acciones sensibles.
 *
 * Los tres perfiles del pedido (admin / técnico / administrativo) **no se codifican como enum**:
 * emergen del cruce. Nadie declara «soy técnico»; si tenés el módulo de seguimiento diario, tenés
 * el panel de seguimiento diario.
 */
export const CATALOGO_PANELES: readonly DefinicionPanel[] = Object.freeze([
  {
    id: 'postura',
    titulo: 'Postura',
    descripcion: 'Levante y producción: aves, mortalidad, producción de huevo y consumo.',
    routesModulo: ['/daily-log/seguimiento', '/daily-log/produccion'],
    orden: 10,
    bloques: [
      { id: 'postura.kpis', titulo: 'Indicadores' },
      { id: 'postura.produccion-vs-guia', titulo: '% producción vs. guía genética' },
      { id: 'postura.huevos', titulo: 'Huevo por tipo' },
      { id: 'postura.mortalidad', titulo: 'Mortalidad diaria' },
      {
        // Sólo existe donde existe el estado «pendiente de validar». Sin el flag, la columna
        // Estado ni se captura: mostrar el bloque sería mostrar un cero que no significa nada.
        id: 'postura.sin-validar',
        titulo: 'Seguimientos sin validar',
        requiereFlag: 'requiereValidacionSeguimientoDiario',
        requierePermisos: ['seguimiento_produccion.validar', 'seguimiento_produccion.desvalidar']
      }
    ]
  },
  {
    id: 'engorde',
    titulo: 'Pollo engorde',
    descripcion: 'Lotes activos, peso vs. guía, conversión, mortalidad y ventas.',
    routesModulo: ['/daily-log/aves-engorde'],
    orden: 20,
    bloques: [
      { id: 'engorde.kpis', titulo: 'Indicadores' },
      { id: 'engorde.peso-vs-guia', titulo: 'Peso real vs. guía' },
      { id: 'engorde.conversion', titulo: 'Conversión y mortalidad' },
      {
        // La venta es su propio módulo: quien sólo carga el seguimiento no la tiene en el menú.
        id: 'engorde.ventas',
        titulo: 'Ventas del período',
        routesModulo: ['/movimiento-pollo-engorde/lista']
      }
    ]
  },
  {
    id: 'alimento-inventario',
    titulo: 'Alimento e inventario',
    descripcion: 'Stock por granja, descuadres de alimento, consumo vs. ingreso y gastos.',
    routesModulo: ['/gestion-inventario', '/inventario-gastos'],
    orden: 30,
    bloques: [
      { id: 'inventario.stock', titulo: 'Stock por granja', routesModulo: ['/gestion-inventario'] },
      {
        id: 'inventario.descuadres',
        titulo: 'Descuadres de alimento',
        routesModulo: ['/gestion-inventario']
      },
      {
        id: 'inventario.consumo-vs-ingreso',
        titulo: 'Consumo vs. ingreso',
        routesModulo: ['/gestion-inventario']
      },
      { id: 'inventario.gastos', titulo: 'Gastos del período', routesModulo: ['/inventario-gastos'] }
    ]
  },
  {
    id: 'cumplimiento',
    titulo: 'Cumplimiento y pendientes',
    descripcion: 'Vacunación, cuadres sin conexión y tareas de implementación.',
    routesModulo: ['/vacunacion', '/cuadres-offline', '/implementacion'],
    orden: 40,
    bloques: [
      {
        id: 'cumplimiento.vacunacion-pendiente',
        titulo: 'Vacunación pendiente y vencida',
        routesModulo: ['/vacunacion']
      },
      {
        id: 'cumplimiento.vacunacion-cumplimiento',
        titulo: '% de cumplimiento',
        routesModulo: ['/vacunacion/reportes']
      },
      {
        id: 'cumplimiento.cuadres-offline',
        titulo: 'Cuadres sin resolver',
        routesModulo: ['/cuadres-offline']
      },
      {
        id: 'cumplimiento.implementacion',
        titulo: 'Tareas de implementación',
        routesModulo: ['/implementacion']
      }
    ]
  }
]);

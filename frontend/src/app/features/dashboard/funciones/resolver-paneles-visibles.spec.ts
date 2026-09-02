import { MenuNodo, PanelId } from '../models/dashboard-panel.model';
import {
  EntradaGating,
  resolverPanelesVisibles,
  routesDelMenu,
  tieneBloque
} from './resolver-paneles-visibles.funcion';

/** Arma un menú plano a partir de routes sueltas. */
const menuDe = (...routes: string[]): MenuNodo[] => routes.map(route => ({ route }));

/** Entrada por defecto: sin permisos y sin flags (el caso más pobre). */
const gating = (menu: MenuNodo[] | null, extra: Partial<EntradaGating> = {}): EntradaGating => ({
  menu,
  permisos: [],
  flags: null,
  ...extra
});

const ids = (menu: MenuNodo[] | null, extra: Partial<EntradaGating> = {}): PanelId[] =>
  resolverPanelesVisibles(gating(menu, extra)).map(p => p.id);

describe('resolverPanelesVisibles', () => {
  // ---------------------------------------------------------------- panel por módulo del menú

  it('1 · el menú de seguimiento de levante abre el panel de Postura', () => {
    expect(ids(menuDe('/daily-log/seguimiento'))).toEqual(['postura']);
  });

  it('1b · el de producción también lo abre (cualquiera de las dos routes alcanza)', () => {
    expect(ids(menuDe('/daily-log/produccion'))).toEqual(['postura']);
  });

  it('2 · sin ningún módulo de seguimiento, el panel de Postura no está en la lista', () => {
    // No es «oculto»: no existe. Un panel que no se devuelve tampoco dispara su request.
    expect(ids(menuDe('/config/users', '/config/companies'))).toEqual([]);
  });

  it('3 · sólo con inventario ve sólo el panel de Alimento e inventario', () => {
    expect(ids(menuDe('/gestion-inventario'))).toEqual(['alimento-inventario']);
  });

  it('4 · con los cuatro módulos ve los cuatro paneles, en orden estable', () => {
    const menu = menuDe(
      '/cuadres-offline',
      '/gestion-inventario',
      '/daily-log/aves-engorde',
      '/daily-log/seguimiento'
    );
    // El menú viene desordenado a propósito: el orden lo manda el catálogo, no la respuesta.
    expect(ids(menu)).toEqual(['postura', 'engorde', 'alimento-inventario', 'cumplimiento']);
  });

  // ---------------------------------------------------------------- fail-closed

  it('5 · menú vacío ⇒ cero paneles, sin excepción', () => {
    expect(ids([])).toEqual([]);
    expect(ids(null)).toEqual([]);
    expect(resolverPanelesVisibles(gating(undefined as never))).toEqual([]);
  });

  it('5b · un rol sin role_menus no recibe un panel «por las dudas»', () => {
    // Es el caso del usuario recién creado sin rol asignado. Devolver algo acá sería justo lo
    // contrario del fail-closed que el resto del repo sostiene.
    expect(resolverPanelesVisibles(gating([]))).toEqual([]);
  });

  it('5c · nodos basura (null, route vacía, sin route) no otorgan nada', () => {
    const menu = [
      null as unknown as MenuNodo,
      { route: '' },
      { route: '   ' },
      { label: 'Configuración' } as MenuNodo // contenedor sin route
    ];
    expect(resolverPanelesVisibles(gating(menu))).toEqual([]);
  });

  // ---------------------------------------------------------------- normalización de route

  it('6 · matchea con mayúsculas, barra final y espacios', () => {
    expect(ids(menuDe('/Daily-Log/Seguimiento/'))).toEqual(['postura']);
    expect(ids(menuDe('  /DAILY-LOG/PRODUCCION  '))).toEqual(['postura']);
    expect(ids(menuDe('daily-log/seguimiento'))).toEqual(['postura']); // sin barra inicial
  });

  it('6b · un módulo que sólo COMPARTE PREFIJO no cuenta como el módulo', () => {
    // `/vacunacion-historica` no es `/vacunacion`. Sin la barra en el prefijo, este test falla.
    expect(ids(menuDe('/vacunacion-historica'))).toEqual([]);
    expect(ids(menuDe('/gestion-inventarios-viejo'))).toEqual([]);
  });

  it('6c · un descendiente SÍ cubre al módulo padre', () => {
    expect(ids(menuDe('/vacunacion/cronograma'))).toEqual(['cumplimiento']);
  });

  it('7 · encuentra la route en cualquier nivel del árbol', () => {
    const menu: MenuNodo[] = [
      {
        route: null, // «Seguimiento Diario», contenedor
        children: [
          { route: '/otra-cosa' },
          { route: null, children: [{ route: '/daily-log/aves-engorde' }] } // 3er nivel
        ]
      }
    ];
    expect(ids(menu)).toEqual(['engorde']);
  });

  // ---------------------------------------------------------------- flags de empresa

  it('8 · el bloque de seguimientos sin validar no existe si la empresa no usa doble validación', () => {
    const menu = menuDe('/daily-log/seguimiento');
    const permisos = ['seguimiento_produccion.validar'];

    const sinFlag = resolverPanelesVisibles(gating(menu, { permisos }))[0];
    expect(tieneBloque(sinFlag, 'postura.sin-validar')).toBe(false);

    const conFlag = resolverPanelesVisibles(
      gating(menu, { permisos, flags: { requiereValidacionSeguimientoDiario: true } })
    )[0];
    expect(tieneBloque(conFlag, 'postura.sin-validar')).toBe(true);
  });

  it('11 · flags nulos o a medias se tratan como apagados (fail-closed), y el panel base igual se dibuja', () => {
    const menu = menuDe('/daily-log/seguimiento');
    const permisos = ['seguimiento_produccion.validar'];

    for (const flags of [null, undefined, {}, { requiereValidacionSeguimientoDiario: null }]) {
      const panel = resolverPanelesVisibles(gating(menu, { permisos, flags }))[0];
      expect(panel).withContext(`flags=${JSON.stringify(flags)}`).toBeDefined();
      expect(tieneBloque(panel, 'postura.sin-validar')).toBe(false);
      // Lo importante: el panel NO desaparece porque los flags no se hayan podido resolver.
      expect(tieneBloque(panel, 'postura.kpis')).toBe(true);
    }
  });

  // ---------------------------------------------------------------- permisos de acción

  it('10 · con el módulo pero sin el permiso ve el panel y NO el bloque de acción', () => {
    const panel = resolverPanelesVisibles(
      gating(menuDe('/daily-log/seguimiento'), {
        permisos: ['editar_registro'], // existe, pero no es el que pide el bloque
        flags: { requiereValidacionSeguimientoDiario: true }
      })
    )[0];

    expect(panel.id).toBe('postura');
    expect(tieneBloque(panel, 'postura.kpis')).toBe(true);
    expect(tieneBloque(panel, 'postura.sin-validar')).toBe(false);
  });

  it('10b · alcanza con UNO de los permisos pedidos, y no distingue mayúsculas', () => {
    const con = (permisos: string[]) =>
      tieneBloque(
        resolverPanelesVisibles(
          gating(menuDe('/daily-log/seguimiento'), {
            permisos,
            flags: { requiereValidacionSeguimientoDiario: true }
          })
        )[0],
        'postura.sin-validar'
      );

    expect(con(['seguimiento_produccion.validar'])).toBe(true);
    expect(con(['seguimiento_produccion.desvalidar'])).toBe(true);
    expect(con(['SEGUIMIENTO_PRODUCCION.VALIDAR'])).toBe(true);
    expect(con([])).toBe(false);
  });

  // ---------------------------------------------------------------- bloques con módulo propio

  it('el bloque de ventas de engorde pide SU módulo, no el del seguimiento', () => {
    const soloSeguimiento = resolverPanelesVisibles(gating(menuDe('/daily-log/aves-engorde')))[0];
    expect(tieneBloque(soloSeguimiento, 'engorde.kpis')).toBe(true);
    expect(tieneBloque(soloSeguimiento, 'engorde.ventas')).toBe(false);

    const conVentas = resolverPanelesVisibles(
      gating(menuDe('/daily-log/aves-engorde', '/movimiento-pollo-engorde/lista'))
    )[0];
    expect(tieneBloque(conVentas, 'engorde.ventas')).toBe(true);
  });

  it('con sólo gastos de inventario ve el panel, pero únicamente el bloque de gastos', () => {
    const panel = resolverPanelesVisibles(gating(menuDe('/inventario-gastos')))[0];

    expect(panel.id).toBe('alimento-inventario');
    expect(panel.bloques).toEqual(['inventario.gastos']);
    expect(tieneBloque(panel, 'inventario.stock')).toBe(false);
    expect(tieneBloque(panel, 'inventario.descuadres')).toBe(false);
  });

  it('un panel cuyos bloques quedan TODOS fuera no se devuelve (nada de cascarón vacío)', () => {
    // `/implementacion` abre el panel de cumplimiento, y su único bloque aplicable es el de
    // implementación: si estuviera gateado aparte, el panel no debería quedar vacío en pantalla.
    const panel = resolverPanelesVisibles(gating(menuDe('/implementacion/mis-tareas')))[0];
    expect(panel.bloques.length).toBeGreaterThan(0);

    // Y el caso duro: ninguna route del catálogo ⇒ ningún panel, ni vacío ni lleno.
    expect(resolverPanelesVisibles(gating(menuDe('/profile')))).toEqual([]);
  });

  // ---------------------------------------------------------------- estabilidad

  it('devuelve siempre el mismo resultado para la misma entrada (sin estado escondido)', () => {
    const menu = menuDe('/daily-log/seguimiento', '/gestion-inventario');
    const uno = JSON.stringify(resolverPanelesVisibles(gating(menu)));
    const dos = JSON.stringify(resolverPanelesVisibles(gating(menu)));
    expect(uno).toBe(dos);
  });

  it('no muta el catálogo ni la entrada', () => {
    const menu = menuDe('/daily-log/seguimiento');
    const entrada = gating(menu);
    const antes = JSON.stringify(entrada);

    resolverPanelesVisibles(entrada);
    resolverPanelesVisibles(entrada);

    expect(JSON.stringify(entrada)).toBe(antes);
  });
});

describe('routesDelMenu', () => {
  it('aplana el árbol completo y normaliza cada route', () => {
    const menu: MenuNodo[] = [
      { route: '/Config/Users/' },
      { route: null, children: [{ route: '/daily-log/seguimiento' }] }
    ];
    const routes = routesDelMenu(menu);

    expect(routes.has('/config/users')).toBe(true);
    expect(routes.has('/daily-log/seguimiento')).toBe(true);
    expect(routes.size).toBe(2);
  });

  it('menú nulo devuelve un set vacío, no revienta', () => {
    expect(routesDelMenu(null).size).toBe(0);
    expect(routesDelMenu(undefined).size).toBe(0);
  });
});

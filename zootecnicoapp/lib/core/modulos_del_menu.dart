/// Qué módulos ve el usuario en la app: los que le habilita su menú del backend.
///
/// El menú llega de `GET /api/Auth/menu` (cifrado) como un árbol de nodos con
/// `label`, `route` e `hijos`. Acá se aplana y se traduce a los módulos de
/// seguimiento que la app sabe capturar.
///
/// **Se mapea por `route`, jamás por id de menú:** los ids difieren entre local y
/// producción, así que un mapeo por id funcionaría en la máquina de desarrollo y
/// dejaría al usuario sin módulos en la granja.
///
/// Y el match es **exacto**, no `contains`: la ruta de *Seguimiento Reproductora
/// Postura* (`/daily-log/seguimiento-diario-lote-reproductora`) es prefijo literal
/// de la de *Reproductora Pollo Engorde* (`..._pollo_engorde`). Con `startsWith`,
/// un usuario de postura vería el formulario de engorde.
library;

import 'models.dart';

/// Ruta del menú → módulo de seguimiento. Las claves son las de la tabla `menus`.
const Map<String, ModuloSeguimiento> rutasDeSeguimiento = {
  '/daily-log/seguimiento': ModuloSeguimiento.levante,
  '/daily-log/aves-engorde': ModuloSeguimiento.engorde,
  '/daily-log/produccion': ModuloSeguimiento.produccion,
  '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde':
      ModuloSeguimiento.reproductora,
};

/// Un nodo del menú tal como lo manda el backend (`MenuItemDto`).
class MenuNodo {
  const MenuNodo({required this.label, this.route, this.hijos = const []});

  final String label;
  final String? route;
  final List<MenuNodo> hijos;

  factory MenuNodo.fromJson(Map<String, dynamic> j) => MenuNodo(
        label: (j['label'] ?? j['Label'] ?? '') as String,
        route: (j['route'] ?? j['Route']) as String?,
        hijos: _hijosDe(j),
      );

  static List<MenuNodo> _hijosDe(Map<String, dynamic> j) {
    final crudos = (j['children'] ?? j['Children'] ?? j['hijos']) as List?;
    if (crudos == null) return const [];
    return crudos
        .whereType<Map>()
        .map((h) => MenuNodo.fromJson(h.cast<String, dynamic>()))
        .toList();
  }
}

/// Los módulos que el usuario puede capturar, en el orden canónico del enum
/// (no en el del menú: el orden del menú lo edita cada empresa).
///
/// Un menú vacío devuelve lista vacía — **fail-closed**: sin módulo explícito,
/// la app no ofrece registrar nada.
List<ModuloSeguimiento> modulosDelMenu(List<MenuNodo> menu) {
  final rutas = _rutasPlanas(menu);
  final encontrados = <ModuloSeguimiento>{};

  for (final ruta in rutas) {
    final modulo = rutasDeSeguimiento[_normalizar(ruta)];
    if (modulo != null) encontrados.add(modulo);
  }

  return ModuloSeguimiento.values.where(encontrados.contains).toList();
}

/// Aplana el árbol quedándose sólo con los nodos que tienen ruta: los padres
/// ("Seguimiento Diario") vienen con `route` vacío y son sólo agrupadores.
Set<String> _rutasPlanas(List<MenuNodo> menu) {
  final rutas = <String>{};
  void recorrer(List<MenuNodo> nodos) {
    for (final n in nodos) {
      final r = n.route;
      if (r != null && r.trim().isNotEmpty) rutas.add(r);
      if (n.hijos.isNotEmpty) recorrer(n.hijos);
    }
  }

  recorrer(menu);
  return rutas;
}

/// Tolera mayúsculas y la barra final; no tolera prefijos (ver doc de arriba).
String _normalizar(String ruta) {
  var r = ruta.trim().toLowerCase();
  while (r.length > 1 && r.endsWith('/')) {
    r = r.substring(0, r.length - 1);
  }
  return r;
}

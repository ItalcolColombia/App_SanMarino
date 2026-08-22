/// Los módulos que ve el usuario salen de su menú del backend.
///
/// Los menús de abajo son los reales de la BD local (21-ago-2026): `admin.ecuador`
/// tiene *Pollo Engorde* y **no** *Reproductora Pollo Engorde*; `admin.panama`
/// tiene los dos.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/models.dart';
import 'package:zootecnicoapp/core/modulos_del_menu.dart';

MenuNodo _grupo(String label, List<MenuNodo> hijos) =>
    MenuNodo(label: label, route: '', hijos: hijos);

MenuNodo _item(String label, String route) => MenuNodo(label: label, route: route);

void main() {
  group('menús reales', () {
    test('admin.ecuador ve sólo Pollo Engorde', () {
      final menu = [
        _item('Gestion de Granjas', '/config/farm-management'),
        _grupo('Seguimiento Diario', [
          _item('Pollo Engorde', '/daily-log/aves-engorde'),
          _item('Gastos de inventario', '/inventario-gastos'),
        ]),
        _grupo('Reportes', [_item('Liquidacion tecnica', '/indicador-ecuador')]),
      ];
      expect(modulosDelMenu(menu), [ModuloSeguimiento.engorde]);
    });

    test('admin.panama ve engorde y reproductora', () {
      final menu = [
        _grupo('Seguimiento Diario', [
          _item('Seguimiento Reproductora Pollo Engorde',
              '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde'),
          _item('Pollo Engorde', '/daily-log/aves-engorde'),
        ]),
      ];
      expect(
        modulosDelMenu(menu),
        [ModuloSeguimiento.engorde, ModuloSeguimiento.reproductora],
      );
    });

    test('el orden es el del enum, no el del menú', () {
      final menu = [
        _grupo('Seguimiento Diario', [
          _item('Reproductora', '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde'),
          _item('Producción', '/daily-log/produccion'),
          _item('Levante', '/daily-log/seguimiento'),
          _item('Engorde', '/daily-log/aves-engorde'),
        ]),
      ];
      expect(modulosDelMenu(menu), [
        ModuloSeguimiento.levante,
        ModuloSeguimiento.engorde,
        ModuloSeguimiento.produccion,
        ModuloSeguimiento.reproductora,
      ]);
    });
  });

  group('la trampa del prefijo', () {
    test('postura NO habilita reproductora pollo engorde', () {
      // '/daily-log/seguimiento-diario-lote-reproductora' es prefijo literal de
      // la ruta de pollo engorde: con un `startsWith`, un usuario de postura
      // vería el formulario equivocado.
      final menu = [
        _grupo('Seguimiento Diario', [
          _item('Seguimiento Reproductora Postura',
              '/daily-log/seguimiento-diario-lote-reproductora'),
        ]),
      ];
      expect(modulosDelMenu(menu), isEmpty);
    });

    test('y al revés: pollo engorde no arrastra a postura', () {
      final menu = [
        _item('Reproductora Pollo Engorde',
            '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde'),
      ];
      expect(modulosDelMenu(menu), [ModuloSeguimiento.reproductora]);
    });
  });

  group('fail-closed', () {
    test('menú vacío no habilita nada', () {
      expect(modulosDelMenu(const []), isEmpty);
    });

    test('un menú sin rutas de seguimiento no habilita nada', () {
      final menu = [
        _grupo('Configuración', [_item('Usuarios', '/config/users')]),
        _item('Tickets', '/tickets'),
      ];
      expect(modulosDelMenu(menu), isEmpty);
    });

    test('los grupos sin ruta se ignoran, no rompen', () {
      expect(modulosDelMenu([_grupo('Seguimiento Diario', const [])]), isEmpty);
    });
  });

  group('normalización', () {
    test('tolera mayúsculas y barra final', () {
      expect(
        modulosDelMenu([_item('Engorde', '/Daily-Log/Aves-Engorde/')]),
        [ModuloSeguimiento.engorde],
      );
    });
  });

  group('parseo del árbol que manda el backend', () {
    test('lee children anidados', () {
      final nodo = MenuNodo.fromJson({
        'label': 'Seguimiento Diario',
        'route': '',
        'children': [
          {'label': 'Pollo Engorde', 'route': '/daily-log/aves-engorde'},
        ],
      });
      expect(nodo.hijos.single.route, '/daily-log/aves-engorde');
      expect(modulosDelMenu([nodo]), [ModuloSeguimiento.engorde]);
    });

    test('un nodo sin children no revienta', () {
      final nodo = MenuNodo.fromJson({'label': 'Tickets', 'route': '/tickets'});
      expect(nodo.hijos, isEmpty);
    });
  });
}

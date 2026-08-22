/// El array de ítems que dispara el descuento de inventario.
///
/// Estos tests cubren la clase de error que no da mensaje: mandar el id
/// equivocado no falla, **descuenta otro producto**. En la base hay 227 ids que
/// colisionan entre `catalogo_items` e `item_inventario_ecuador` — el 89 es un
/// alimento en una tabla y un líquido en la otra.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/items_consumo.dart';
import 'package:zootecnicoapp/core/models.dart';
import 'package:zootecnicoapp/core/models_inventario.dart';
import 'package:zootecnicoapp/core/perfil_pais.dart';

void main() {
  ItemInventario item({
    int id = 89,
    String nombre = 'ENGORDE 1',
    String tipo = 'Alimento',
    String? concepto,
  }) =>
      ItemInventario(
          id: id, codigo: 'C$id', nombre: nombre, tipo: tipo,
          unidad: 'kg', concepto: concepto);

  List<Map<String, dynamic>> armar(
    List<LineaConsumo> lineas, {
    int? pais = PaisId.colombia,
    bool silos = false,
  }) =>
      ItemsConsumo.armar(lineas: lineas, paisId: pais, manejaSilos: silos);

  group('qué id viaja — depende del país, no del módulo', () {
    test('Colombia manda catalogItemId y NO el de inventario', () {
      // El backend lo traduce por código contra el catálogo de la empresa.
      final r = armar([LineaConsumo(item: item(), cantidad: '100')],
          pais: PaisId.colombia);
      expect(r.single['catalogItemId'], 89);
      expect(r.single.containsKey('itemInventarioEcuadorId'), isFalse);
    });

    test('Ecuador y Panamá mandan los DOS con el mismo valor', () {
      for (final p in [PaisId.ecuador, PaisId.panama]) {
        final r = armar([LineaConsumo(item: item(), cantidad: '100')], pais: p);
        expect(r.single['itemInventarioEcuadorId'], 89, reason: 'país $p');
        expect(r.single['catalogItemId'], 89, reason: 'país $p');
      }
    });

    test('sin país resuelto NO manda el id de inventario: fail-closed', () {
      // Es el caso conservador. En el peor escenario no descuenta, que es
      // preferible a descontar el producto equivocado.
      final r = armar([LineaConsumo(item: item(), cantidad: '100')], pais: null);
      expect(r.single.containsKey('itemInventarioEcuadorId'), isFalse);
    });
  });

  group('qué líneas entran', () {
    test('una línea sin cantidad no es un consumo', () {
      expect(armar([LineaConsumo(item: item())]), isEmpty);
      expect(armar([LineaConsumo(item: item(), cantidad: '')]), isEmpty);
      expect(armar([LineaConsumo(item: item(), cantidad: '0')]), isEmpty);
    });

    test('un ítem sin id se descarta: el backend lo ignoraría en silencio', () {
      expect(armar([LineaConsumo(item: item(id: 0), cantidad: '100')]), isEmpty);
    });

    test('sin líneas cargadas devuelve vacío, no inventa un consumo', () {
      expect(armar(const []), isEmpty);
    });

    test('las líneas incompletas se descartan sin tirar abajo las buenas', () {
      final r = armar([
        LineaConsumo(item: item(id: 1), cantidad: '50'),
        LineaConsumo(item: item(id: 2)), // sin cantidad
        LineaConsumo(item: item(id: 3), cantidad: '25'),
      ]);
      expect(r.map((e) => e['catalogItemId']), [1, 3]);
    });
  });

  group('cantidad y unidad', () {
    test('siempre viaja en kg: el backend sólo sabe convertir gramos', () {
      // 'l', 'lb', 'qq' se restarían COMO KILOS sin error ni log.
      final r = armar([LineaConsumo(item: item(), cantidad: '340.5')]);
      expect(r.single['unidad'], 'kg');
      expect(r.single['cantidad'], 340.5);
    });

    test('acepta coma decimal: el teclado de Android da coma en es-CO', () {
      final r = armar([LineaConsumo(item: item(), cantidad: '340,5')]);
      expect(r.single['cantidad'], 340.5);
    });

    test('texto inválido no viaja como consumo', () {
      expect(armar([LineaConsumo(item: item(), cantidad: 'abc')]), isEmpty);
    });
  });

  group('silo: se omite la clave, no se manda en null', () {
    test('con el flag apagado el silo no viaja aunque esté cargado', () {
      // Con el flag OFF, un siloId con valor es un 400 en Colombia.
      final r = armar([LineaConsumo(item: item(), cantidad: '10', siloId: 7)],
          silos: false);
      expect(r.single.containsKey('siloId'), isFalse);
    });

    test('con el flag encendido viaja', () {
      final r = armar([LineaConsumo(item: item(), cantidad: '10', siloId: 7)],
          silos: true);
      expect(r.single['siloId'], 7);
    });

    test('flag encendido pero sin silo elegido: se omite, no va en 0', () {
      final r = armar([LineaConsumo(item: item(), cantidad: '10')], silos: true);
      expect(r.single.containsKey('siloId'), isFalse);
    });
  });

  group('tipoItem — no filtra el descuento, pero sí la columna de consumo', () {
    test('el alimento se marca como alimento', () {
      final r = armar([LineaConsumo(item: item(tipo: 'Alimento'), cantidad: '1')]);
      expect(r.single['tipoItem'], 'alimento');
    });

    test('reconoce el alimento sin importar mayúsculas', () {
      // El catálogo lo tiene como 'Alimento', 'alimento' y 'ALIMENTO' según la
      // empresa: por eso la app clasifica localmente y no con el filtro del backend.
      for (final t in ['Alimento', 'alimento', 'ALIMENTO', '  Alimento  ']) {
        final r = armar([LineaConsumo(item: item(tipo: t), cantidad: '1')]);
        expect(r.single['tipoItem'], 'alimento', reason: t);
      }
    });

    test('el concepto manda sobre el tipo cuando viene', () {
      final r = armar([
        LineaConsumo(item: item(tipo: 'Alimento', concepto: 'Medicamento'), cantidad: '1')
      ]);
      expect(r.single['tipoItem'], 'otro');
    });

    test('una vacuna igual descuenta stock: el backend no filtra por tipo', () {
      // Se marca 'otro' para que no sume a la columna de consumo, pero la línea
      // viaja igual — y el parser del backend la va a descontar.
      final r = armar([LineaConsumo(item: item(tipo: 'Vacuna'), cantidad: '5')]);
      expect(r, hasLength(1));
      expect(r.single['tipoItem'], 'otro');
    });
  });

  group('kilos de alimento para el total en pantalla', () {
    test('suma sólo el alimento', () {
      final k = ItemsConsumo.kgDeAlimento([
        LineaConsumo(item: item(id: 1, tipo: 'Alimento'), cantidad: '100'),
        LineaConsumo(item: item(id: 2, tipo: 'Vacuna'), cantidad: '50'),
      ]);
      expect(k, 100);
    });

    test('ignora las líneas incompletas', () {
      final k = ItemsConsumo.kgDeAlimento([
        LineaConsumo(item: item(id: 1), cantidad: '100'),
        LineaConsumo(item: item(id: 2)),
      ]);
      expect(k, 100);
    });
  });

  group('aviso temprano de stock', () {
    final existencia = ExistenciaInventario(
      itemId: 89, farmId: 40, galponId: 'G1',
      cantidad: 100, reservado: 30, unidad: 'kg',
    );
    final porClave = {existencia.clave: existencia};

    test('avisa cuando lo cargado supera lo disponible', () {
      // disponible = 100 - 30 = 70
      final a = ItemsConsumo.avisosDeStock(
        lineas: [LineaConsumo(item: item(), cantidad: '80')],
        existenciasPorClave: porClave, farmId: 40, galponId: 'G1',
      );
      expect(a, hasLength(1));
      expect(a.single, contains('70 kg'));
    });

    test('no avisa cuando alcanza', () {
      final a = ItemsConsumo.avisosDeStock(
        lineas: [LineaConsumo(item: item(), cantidad: '70')],
        existenciasPorClave: porClave, farmId: 40, galponId: 'G1',
      );
      expect(a, isEmpty);
    });

    test('avisa si no hay existencia en ESE galpón', () {
      // El stock es por (granja, núcleo, galpón, silo, ítem): mirar sólo el ítem
      // mostraría el total de la granja y dejaría pasar un galpón vacío.
      final a = ItemsConsumo.avisosDeStock(
        lineas: [LineaConsumo(item: item(), cantidad: '10')],
        existenciasPorClave: porClave, farmId: 40, galponId: 'OTRO',
      );
      expect(a.single, contains('no hay existencia'));
    });
  });

  group('dónde se pega el array en el payload', () {
    List<Map<String, dynamic>> uno() => [
          {'catalogItemId': 1, 'cantidad': 10.0}
        ];

    test('hembras y machos van en los cuatro módulos', () {
      for (final m in ModuloSeguimiento.values) {
        final p = <String, dynamic>{};
        ItemsConsumo.aplicarEn(p,
            itemsHembras: uno(), itemsMachos: uno(), modulo: m);
        expect(p['itemsHembras'], hasLength(1), reason: m.label);
        expect(p['itemsMachos'], hasLength(1), reason: m.label);
      }
    });

    test('itemsGenerales sólo en levante y engorde', () {
      // Reproductora ni siquiera lo proyecta al metadata y producción no lo
      // tiene en el contrato: mandarlo se perdería en silencio.
      for (final m in ModuloSeguimiento.values) {
        final p = <String, dynamic>{};
        ItemsConsumo.aplicarEn(p,
            itemsHembras: const [], itemsMachos: const [],
            itemsGenerales: uno(), modulo: m);
        final esperado =
            m == ModuloSeguimiento.levante || m == ModuloSeguimiento.engorde;
        expect(p.containsKey('itemsGenerales'), esperado, reason: m.label);
      }
    });

    test('un array vacío no ensucia el payload', () {
      final p = <String, dynamic>{};
      ItemsConsumo.aplicarEn(p,
          itemsHembras: const [], itemsMachos: const [],
          modulo: ModuloSeguimiento.engorde);
      expect(p, isEmpty);
    });
  });
}

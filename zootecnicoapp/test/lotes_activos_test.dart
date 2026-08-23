/// Qué lotes se le ofrecen al operario.
///
/// Un lote cerrado no admite registros nuevos: el backend los rechaza. Antes se
/// mostraban igual y el choque aparecía recién al tocarlos, con el formulario ya
/// elegido. Si esta regla se rompe, el síntoma no es un error: es que el
/// operario pierde tiempo eligiendo lotes que no puede trabajar.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/features/lotes/funciones/lotes_activos.dart';

Lote lote(int id, {bool cerrado = false, ModuloSeguimiento? modulo}) => Lote(
      id: id,
      nombre: 'L$id',
      granja: 'G',
      galpon: 'g',
      modulo: modulo ?? ModuloSeguimiento.engorde,
      dia: 10,
      aves: 100,
      cerrado: cerrado,
    );

void main() {
  group('lotesActivos', () {
    test('deja fuera los cerrados', () {
      final r = lotesActivos([
        lote(1),
        lote(2, cerrado: true),
        lote(3),
      ]);

      expect(r.map((l) => l.id).toList(), [1, 3]);
    });

    test('conserva el orden de entrada', () {
      // El backend ya los manda ordenados; reordenar acá cambiaría lo que el
      // operario espera ver primero.
      final r = lotesActivos([lote(9), lote(4, cerrado: true), lote(7), lote(1)]);

      expect(r.map((l) => l.id).toList(), [9, 7, 1]);
    });

    test('todos cerrados da vacío, no null', () {
      expect(lotesActivos([lote(1, cerrado: true)]), isEmpty);
    });

    test('lista vacía no rompe', () {
      expect(lotesActivos(const []), isEmpty);
    });

    test('la regla vale para los cuatro módulos', () {
      // Los tres orígenes usan campos distintos (`estadoOperativoLote`,
      // `estado`, `estadoCierre`) y el mapeo los unifica en `cerrado`: acá
      // se comprueba que el filtro no mire el módulo.
      final todos = [
        for (final m in ModuloSeguimiento.values) ...[
          lote(m.index * 10 + 1, modulo: m),
          lote(m.index * 10 + 2, modulo: m, cerrado: true),
        ],
      ];

      final r = lotesActivos(todos);

      expect(r.length, ModuloSeguimiento.values.length);
      expect(r.every((l) => !l.cerrado), isTrue);
    });

    test('no muta la lista original', () {
      final original = [lote(1), lote(2, cerrado: true)];

      lotesActivos(original);

      expect(original.length, 2, reason: 'la caché sigue teniendo todos');
    });
  });

  group('lotesCerrados', () {
    test('cuenta los que quedaron afuera', () {
      expect(
        lotesCerrados([lote(1), lote(2, cerrado: true), lote(3, cerrado: true)]),
        2,
      );
    });

    test('sin cerrados es cero', () {
      expect(lotesCerrados([lote(1), lote(2)]), 0);
    });
  });
}

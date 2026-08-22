/// La aritmética de postura: clasificadora de huevos y etapa del ciclo.
///
/// Los dos números viajan **ya calculados** en el payload y el backend los
/// persiste tal cual, así que un móvil que los calcule distinto del web produce
/// dos verdades para el mismo día del mismo lote.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/postura_calculos.dart';

void main() {
  group('clasificadora fija: incubables = limpio + tratado', () {
    test('sólo los dos incubables suman a incubables', () {
      final t = PosturaCalculos.totalesClasificadora({
        'huevoLimpio': '100',
        'huevoTratado': '50',
      });
      expect(t.incubables, 150);
      expect(t.total, 150);
    });

    test('los no incubables suman al total pero NO a incubables', () {
      final t = PosturaCalculos.totalesClasificadora({
        'huevoLimpio': '100',
        'huevoSucio': '20',
        'huevoRoto': '5',
      });
      expect(t.incubables, 100);
      expect(t.total, 125);
    });

    test('las nueve categorías no incubables cuentan', () {
      final campos = {for (final k in huevosNoIncubables) k: '1'};
      final t = PosturaCalculos.totalesClasificadora(campos);
      expect(huevosNoIncubables.length, 9);
      expect(t.total, 9);
      expect(t.incubables, 0);
    });

    test('sin nada cargado da cero, no null', () {
      final t = PosturaCalculos.totalesClasificadora(const {});
      expect(t.incubables, 0);
      expect(t.total, 0);
    });

    test('un campo con texto basura cuenta como cero', () {
      final t = PosturaCalculos.totalesClasificadora({'huevoLimpio': 'abc'});
      expect(t.total, 0);
    });

    test('ignora las claves que no son de huevo', () {
      final t = PosturaCalculos.totalesClasificadora({
        'huevoLimpio': '10',
        'mortalidadHembras': '999',
        'consumoKgHembras': '340',
      });
      expect(t.total, 10);
    });
  });

  group('porcentaje de incubabilidad', () {
    test('se calcula sobre el total', () {
      final t = PosturaCalculos.totalesClasificadora({
        'huevoLimpio': '75',
        'huevoSucio': '25',
      });
      expect(t.porcentajeIncubables, 75.0);
    });

    test('sin huevos es null, no 0 %', () {
      // Un 0 % diría "todo malo"; lo que pasó es que no se recogió nada.
      expect(TotalesHuevos.cero.porcentajeIncubables, isNull);
    });
  });

  group('etapa del ciclo', () {
    // El rango real es 26-33 / 34-50 / >50, con piso en 26.
    DateTime encaset(int semanas) =>
        DateTime(2026, 1, 1).add(Duration(days: semanas * 7));

    int etapaEn(int semana) => PosturaCalculos.etapa(
          fechaEncaset: DateTime(2026, 1, 1),
          fechaRegistro: encaset(semana),
        );

    test('semana 26 y 33 son etapa 1', () {
      expect(etapaEn(26), 1);
      expect(etapaEn(33), 1);
    });

    test('semana 34 y 50 son etapa 2', () {
      expect(etapaEn(34), 2);
      expect(etapaEn(50), 2);
    });

    test('más de 50 es etapa 3', () {
      expect(etapaEn(51), 3);
      expect(etapaEn(80), 3);
    });

    test('antes de la semana 26 NO baja de etapa 1: el piso es 26', () {
      // El cálculo hace max(26, semana): un lote joven da 1, no una etapa 0.
      expect(etapaEn(1), 1);
      expect(etapaEn(10), 1);
      expect(etapaEn(25), 1);
    });

    test('sin fecha de encasetamiento cae a 1 en vez de reventar', () {
      expect(
        PosturaCalculos.etapa(fechaEncaset: null, fechaRegistro: DateTime(2026, 8, 22)),
        1,
      );
    });

    test('una fecha de registro anterior al encasetamiento no rompe', () {
      expect(
        PosturaCalculos.etapa(
          fechaEncaset: DateTime(2026, 8, 22),
          fechaRegistro: DateTime(2026, 1, 1),
        ),
        1,
      );
    });

    test('la hora no mueve la etapa: sólo cuenta el día', () {
      final a = PosturaCalculos.etapa(
        fechaEncaset: DateTime(2026, 1, 1, 23, 59),
        fechaRegistro: DateTime(2026, 9, 1, 0, 1),
      );
      final b = PosturaCalculos.etapa(
        fechaEncaset: DateTime(2026, 1, 1),
        fechaRegistro: DateTime(2026, 9, 1),
      );
      expect(a, b);
    });
  });
}

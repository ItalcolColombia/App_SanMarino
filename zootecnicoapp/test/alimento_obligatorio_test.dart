/// La regla de alimento obligatorio, espejo de `AlimentoObligatorioCalculos.cs`.
///
/// Lo que se cuida acá es que la app **no encole** un registro que el backend va
/// a rechazar: en offline ese rechazo llega horas después, cuando el usuario ya
/// no está en el galpón y no sabe qué le faltó.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/alimento_obligatorio.dart';
import 'package:zootecnicoapp/core/models.dart';

void main() {
  String? motivo(
    ModuloSeguimiento m, {
    double? h,
    double? mm,
    String? tipo = 'Engorde 1',
  }) =>
      AlimentoObligatorio.motivo(
          modulo: m, kgHembras: h, kgMachos: mm, tipoAlimento: tipo);

  group('cumple', () {
    test('con consumo en hembras', () {
      expect(motivo(ModuloSeguimiento.engorde, h: 340.5), isNull);
    });

    test('con consumo sólo en machos', () {
      expect(motivo(ModuloSeguimiento.engorde, mm: 12), isNull);
    });

    test('con los dos', () {
      expect(motivo(ModuloSeguimiento.reproductora, h: 100, mm: 30), isNull);
    });
  });

  group('no cumple', () {
    test('sin nada cargado', () {
      expect(motivo(ModuloSeguimiento.engorde), isNotNull);
    });

    test('un consumo en cero no es un consumo', () {
      expect(motivo(ModuloSeguimiento.engorde, h: 0, mm: 0), isNotNull);
    });

    test('con kilos pero sin tipo de alimento', () {
      // Sin ítems de inventario, el backend no puede deducir el tipo: llegaría
      // en blanco y el reporte de consumo no sabría a qué alimento imputarlo.
      expect(motivo(ModuloSeguimiento.engorde, h: 340, tipo: ''), isNotNull);
      expect(motivo(ModuloSeguimiento.engorde, h: 340, tipo: '   '), isNotNull);
      expect(motivo(ModuloSeguimiento.engorde, h: 340, tipo: null), isNotNull);
    });
  });

  group('el mensaje nombra el bloque de cada módulo', () {
    test('engorde habla de Hembras o Machos', () {
      expect(motivo(ModuloSeguimiento.engorde), contains('Hembras o en Machos'));
    });

    test('reproductora habla del lote', () {
      expect(motivo(ModuloSeguimiento.reproductora), contains('del lote'));
    });

    test('postura admite uno u otro o ambos', () {
      expect(motivo(ModuloSeguimiento.produccion), contains('o en ambos'));
      expect(motivo(ModuloSeguimiento.levante), contains('o en ambos'));
    });
  });
}

/// Qué calidad de conexión implica lo que reporta el sistema.
///
/// Existe por un fallo medido: la versión anterior hacía `switch (results.first)`
/// con `_ => offline`, así que un equipo con **VPN corporativa** quedaba marcado
/// «Sin conexión» teniendo red perfecta — y la cola de registros no subía nunca.
library;

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/calculos/conectividad.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart' show CalidadConexion;

void main() {
  group('calidadDesdeConectividad — el caso normal', () {
    test('wifi es conexión fuerte', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.wifi]),
        CalidadConexion.wifiFuerte,
      );
    });

    test('ethernet cuenta como fuerte', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.ethernet]),
        CalidadConexion.wifiFuerte,
      );
    });

    test('datos móviles', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.mobile]),
        CalidadConexion.celular,
      );
    });
  });

  group('sin conexión — sólo cuando de verdad no hay ninguna', () {
    test('none es offline', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.none]),
        CalidadConexion.offline,
      );
    });

    test('lista vacía es offline', () {
      expect(calidadDesdeConectividad([]), CalidadConexion.offline);
    });
  });

  group('el fallo que motivó esto: transportes que NO son offline', () {
    // Si alguno de estos vuelve a dar `offline`, la cola deja de subir en los
    // equipos que lo usan y nadie se entera hasta que falta el dato.
    for (final caso in {
      'vpn': ConnectivityResult.vpn,
      'other (así reporta la VPN en iOS/macOS)': ConnectivityResult.other,
      'bluetooth': ConnectivityResult.bluetooth,
      'satellite': ConnectivityResult.satellite,
    }.entries) {
      test('${caso.key} NO es offline', () {
        expect(
          calidadDesdeConectividad([caso.value]).enLinea,
          isTrue,
          reason: '${caso.key} tiene transporte: marcarlo offline frena la cola',
        );
      });
    }
  });

  group('se mira la lista completa, no sólo el primero', () {
    test('[vpn, wifi] es wifi — antes daba offline por leer sólo el primero', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.vpn, ConnectivityResult.wifi]),
        CalidadConexion.wifiFuerte,
      );
    });

    test('[mobile, wifi] se queda con la mejor', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.mobile, ConnectivityResult.wifi]),
        CalidadConexion.wifiFuerte,
      );
    });

    test('[none, mobile] hay datos: no es offline', () {
      expect(
        calidadDesdeConectividad([ConnectivityResult.none, ConnectivityResult.mobile]),
        CalidadConexion.celular,
      );
    });
  });
}

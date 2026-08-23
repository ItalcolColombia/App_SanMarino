/// Decisión pura: qué calidad de conexión implica lo que reporta el sistema.
///
/// Está separada del `SyncService` porque es la regla que decide si la cola sube
/// o no, y necesita test propio: cuando esto se equivoca, el trabajo del operario
/// se queda en la tablet sin que nadie se entere.
///
/// ── Qué se corrigió acá (23-ago-2026) ────────────────────────────────────────
/// La versión anterior hacía `switch (results.first)` con `_ => offline`. Dos
/// fallos medidos:
///
///  1. **La VPN contaba como sin conexión.** El enum de `connectivity_plus`
///     incluye `vpn`, `bluetooth`, `satellite` y `other` (en iOS/macOS la VPN se
///     reporta como `other`). Un equipo con VPN corporativa quedaba marcado
///     «Sin conexión» con red perfecta y la cola no subía NUNCA.
///  2. **`results.first` descartaba el resto.** `[vpn, wifi]` se resolvía como
///     offline aunque el wifi estuviera ahí.
///
/// Regla nueva: se recorre la lista COMPLETA y sólo es `offline` cuando no hay
/// ninguna interfaz utilizable (lista vacía, o sólo `none`).
///
/// ⚠️ Esto sigue siendo *tipo de interfaz*, no alcanzabilidad: que haya wifi no
/// prueba que el backend responda. La confirmación real la da el primer POST.
library;

import 'package:connectivity_plus/connectivity_plus.dart';

import 'package:zootecnicoapp/core/sync/sync_service.dart' show CalidadConexion;

/// Traduce lo que reporta el sistema a la calidad que usa la app.
///
/// Precedencia: una interfaz "de cable/wifi" gana sobre celular, y cualquiera
/// gana sobre offline — si hay dos activas, se informa la mejor.
CalidadConexion calidadDesdeConectividad(List<ConnectivityResult> resultados) {
  var hayFuerte = false;
  var hayCelular = false;

  for (final r in resultados) {
    switch (r) {
      case ConnectivityResult.wifi:
      case ConnectivityResult.ethernet:
        hayFuerte = true;
      case ConnectivityResult.mobile:
        hayCelular = true;
      // vpn/other/bluetooth/satellite: hay transporte. No sabemos su calidad,
      // así que se informa el caso conservador (celular) en vez de mentir
      // diciendo Wi-Fi, pero NUNCA offline: eso frenaría la cola.
      case ConnectivityResult.vpn:
      case ConnectivityResult.other:
      case ConnectivityResult.bluetooth:
      case ConnectivityResult.satellite:
        hayCelular = true;
      case ConnectivityResult.none:
        break;
    }
  }

  if (hayFuerte) return CalidadConexion.wifiFuerte;
  if (hayCelular) return CalidadConexion.celular;
  return CalidadConexion.offline;
}

/// Estado de conexión y cola de sincronización.
///
/// Reglas de UX (ver README del design system):
///  - Cuando todo está al día: NADA visible. La ausencia es el mensaje.
///  - Al guardar: confirmación optimista inmediata, sin spinners.
///  - Al reconectar: ribbon progresivo detect → sync → éxito → colapso (~5 s).
///  - Offline NUNCA es rojo. Es un modo de trabajo válido, no un error.
library;

import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'local_db.dart';
import 'models.dart';

/// Calidad de conexión — más informativa que un booleano online/offline.
enum CalidadConexion { wifiFuerte, wifiDebil, celular, offline;

  String get label => switch (this) {
    CalidadConexion.wifiFuerte => 'Wi-Fi',
    CalidadConexion.wifiDebil  => 'Wi-Fi débil',
    CalidadConexion.celular    => 'Datos móviles',
    CalidadConexion.offline    => 'Sin conexión',
  };

  bool get enLinea => this != CalidadConexion.offline;
}

/// Fase del ribbon de reconexión.
enum FaseRibbon { oculto, detectando, sincronizando, exito }

class SyncService extends ChangeNotifier {
  SyncService({LocalDb? db}) : _db = db ?? LocalDb.instance;

  final LocalDb _db;
  final Connectivity _conn = Connectivity();
  StreamSubscription? _sub;

  CalidadConexion _calidad = CalidadConexion.wifiFuerte;
  FaseRibbon _fase = FaseRibbon.oculto;
  bool _autoSync = true;
  int _pendientes = 0;
  int _enviados = 0;
  int _totalLote = 0;

  CalidadConexion get calidad => _calidad;
  FaseRibbon get fase => _fase;
  bool get autoSync => _autoSync;
  bool get enLinea => _calidad.enLinea;
  int get pendientes => _pendientes;
  int get enviados => _enviados;
  int get totalLote => _totalLote;
  double get progreso => _totalLote == 0 ? 0 : _enviados / _totalLote;

  /// true cuando no hay nada que mostrar al usuario.
  bool get todoAlDia => enLinea && _pendientes == 0 && _fase == FaseRibbon.oculto;

  Future<void> init() async {
    await _refrescarPendientes();
    _aplicarConectividad(await _conn.checkConnectivity());
    _sub = _conn.onConnectivityChanged.listen(_aplicarConectividad);
  }

  void _aplicarConectividad(List<ConnectivityResult> results) {
    final anterior = _calidad;
    _calidad = switch (results.first) {
      ConnectivityResult.wifi     => CalidadConexion.wifiFuerte,
      ConnectivityResult.ethernet => CalidadConexion.wifiFuerte,
      ConnectivityResult.mobile   => CalidadConexion.celular,
      _                            => CalidadConexion.offline,
    };
    notifyListeners();

    // Volvió la red y hay cola → dispara el flujo de reconexión.
    if (!anterior.enLinea && _calidad.enLinea && _pendientes > 0) {
      _flujoReconexion();
    }
  }

  Future<void> _flujoReconexion() async {
    _fase = FaseRibbon.detectando;
    notifyListeners();
    await Future.delayed(const Duration(milliseconds: 900));

    if (!_autoSync) { _fase = FaseRibbon.oculto; notifyListeners(); return; }

    await sincronizar();
  }

  /// Sube la cola uno por uno para que el progreso sea visible y honesto.
  Future<void> sincronizar() async {
    if (!enLinea) return;

    final cola = (await _db.pendientes())
        .where((r) => r.estado == EstadoSync.pending || r.estado == EstadoSync.error)
        .toList();
    if (cola.isEmpty) { _fase = FaseRibbon.oculto; notifyListeners(); return; }

    _fase = FaseRibbon.sincronizando;
    _totalLote = cola.length;
    _enviados = 0;
    notifyListeners();

    for (final r in cola) {
      await _db.marcarEstado(r.id, EstadoSync.syncing);
      notifyListeners();
      try {
        // TODO: reemplazar por la llamada real del ApiClient según r.tipo.
        await Future.delayed(const Duration(milliseconds: 600));
        await _db.confirmarEnviado(r.id);
        _enviados++;
      } catch (e) {
        await _db.marcarEstado(r.id, EstadoSync.error, error: e.toString());
      }
      await _refrescarPendientes();
      notifyListeners();
    }

    _fase = FaseRibbon.exito;
    notifyListeners();
    await Future.delayed(const Duration(seconds: 3));
    _fase = FaseRibbon.oculto;
    notifyListeners();
  }

  /// Encola y devuelve inmediatamente: el usuario ya vio su confirmación.
  Future<void> encolar({
    required String tipo,
    required int loteId,
    required String loteNombre,
    required DateTime fecha,
    required Map<String, dynamic> payload,
  }) async {
    await _db.encolar(tipo: tipo, loteId: loteId, loteNombre: loteNombre, fecha: fecha, payload: payload);
    await _refrescarPendientes();
    notifyListeners();
    if (_autoSync && enLinea) sincronizar();
  }

  Future<void> _refrescarPendientes() async {
    _pendientes = await _db.contarPendientes();
  }

  set autoSync(bool v) { _autoSync = v; notifyListeners(); }

  /// Solo para pruebas y demos internas.
  @visibleForTesting
  void simularConexion(CalidadConexion c) {
    final anterior = _calidad;
    _calidad = c;
    notifyListeners();
    if (!anterior.enLinea && c.enLinea && _pendientes > 0) _flujoReconexion();
  }

  @override
  void dispose() { _sub?.cancel(); super.dispose(); }
}

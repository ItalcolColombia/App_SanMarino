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
import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/calculos/conectividad.dart';
import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';

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
  SyncService({
    LocalDb? db,
    SeguimientosApi? api,
    this.demoraDeteccion = const Duration(milliseconds: 900),
    this.demoraExito = const Duration(seconds: 3),
  })  : _db = db ?? LocalDb.instance,
        _api = api;

  /// Cuánto dura la fase "detectando" del ribbon antes de empezar a subir.
  ///
  /// Es tiempo de UX, no técnico: el ribbon tiene que alcanzar a verse. Se puede
  /// inyectar **sólo** para que los tests no esperen en vano; el default es el
  /// valor real y la app nunca lo cambia.
  final Duration demoraDeteccion;

  /// Cuánto se queda el ribbon en "éxito" antes de colapsar. Mismo criterio.
  final Duration demoraExito;

  final LocalDb _db;

  /// Null mientras no haya sesión: sin usuario autenticado no hay a dónde subir.
  /// La cola se sigue llenando igual — es el punto de la app.
  SeguimientosApi? _api;
  set api(SeguimientosApi? v) => _api = v;
  final Connectivity _conn = Connectivity();
  StreamSubscription? _sub;

  // Arranca offline a proposito: asi la PRIMERA lectura real de conectividad
  // cuenta como transicion y dispara el flujo de reconexion si hay cola. Con
  // wifiFuerte de arranque, abrir la app con pendientes y red no sincronizaba.
  CalidadConexion _calidad = CalidadConexion.offline;
  // Guarda de reentrada: dos disparos concurrentes tomaban el mismo snapshot
  // y posteaban la misma fila dos veces.
  bool _sincronizando = false;
  bool _repetirAlTerminar = false;
  FaseRibbon _fase = FaseRibbon.oculto;
  bool _autoSync = true;
  int _pendientes = 0;
  int _enviados = 0;
  int _totalLote = 0;
  int _duplicados = 0;
  int _rechazados = 0;
  bool _requiereRelogin = false;
  String? _avisoPlataforma;

  CalidadConexion get calidad => _calidad;
  FaseRibbon get fase => _fase;
  bool get autoSync => _autoSync;
  bool get enLinea => _calidad.enLinea;
  int get pendientes => _pendientes;
  int get enviados => _enviados;
  int get totalLote => _totalLote;
  double get progreso => _totalLote == 0 ? 0 : _enviados / _totalLote;

  /// Registros que el servidor ya tenía (mismo lote y día). No son un error del
  /// usuario: se le informa una vez y salen de la cola.
  int get duplicados => _duplicados;

  /// Registros que el servidor rechazó por su contenido en la última pasada. El
  /// día vuelve a quedar libre para cargarlo de nuevo.
  int get rechazados => _rechazados;

  /// Filas que la cola ya no reintenta sola: necesitan que alguien las mire.
  Future<List<RegistroPendiente>> agotadas() => _db.agotadas();

  /// Devuelve una fila agotada a la cola, después de que el usuario corrigió lo
  /// que estuviera mal.
  Future<void> reintentar(String id) async {
    await _db.reintentar(id);
    await _refrescarPendientes();
    notifyListeners();
    if (_autoSync && enLinea) sincronizar();
  }

  /// El token venció mientras subía la cola. La cola NO se pierde: queda
  /// esperando a que el usuario vuelva a entrar.
  bool get requiereRelogin => _requiereRelogin;

  /// El backend no reconoció el origen de la petición (`X-Auth-Failure:
  /// platform-secret`). Es un problema de configuración del servidor, no del
  /// usuario: se muestra el aviso y **no** se cierra su sesión.
  String? get avisoPlataforma => _avisoPlataforma;

  /// true cuando no hay nada que mostrar al usuario.
  bool get todoAlDia => enLinea && _pendientes == 0 && _fase == FaseRibbon.oculto;

  Future<void> init() async {
    await _refrescarPendientes();
    _aplicarConectividad(await _conn.checkConnectivity());
    _sub = _conn.onConnectivityChanged.listen(_aplicarConectividad);
  }

  void _aplicarConectividad(List<ConnectivityResult> results) {
    final anterior = _calidad;
    _calidad = calidadDesdeConectividad(results);
    notifyListeners();

    // Volvió la red y hay cola → dispara el flujo de reconexión.
    if (!anterior.enLinea && _calidad.enLinea && _pendientes > 0) {
      _flujoReconexion();
    }
  }

  Future<void> _flujoReconexion() async {
    _fase = FaseRibbon.detectando;
    notifyListeners();
    await Future.delayed(demoraDeteccion);

    if (!_autoSync) { _fase = FaseRibbon.oculto; notifyListeners(); return; }

    await sincronizar();
  }

  /// Sube la cola uno por uno para que el progreso sea visible y honesto.
  ///
  /// Se para en seco ante un 401 o un fallo de red: seguir intentando con las
  /// filas siguientes sólo produciría veinte errores idénticos y gastaría batería.
  Future<void> sincronizar() async {
    if (!enLinea) return;

    // Si ya hay una corrida en curso, no se arranca una segunda: tomaría el
    // mismo snapshot de la cola y postearía las mismas filas de nuevo. Se anota
    // que hay que repetir al terminar, para no perder el pedido.
    if (_sincronizando) {
      _repetirAlTerminar = true;
      return;
    }
    _sincronizando = true;
    try {
      await _sincronizarInterno();
    } finally {
      _sincronizando = false;
    }
    if (_repetirAlTerminar) {
      _repetirAlTerminar = false;
      await sincronizar();
    }
  }

  Future<void> _sincronizarInterno() async {
    final api = _api;
    if (api == null) {
      // Sin sesión no hay a dónde subir. No es un error: la cola espera.
      _fase = FaseRibbon.oculto;
      notifyListeners();
      return;
    }

    final cola = await _db.porEnviar();
    if (cola.isEmpty) {
      _fase = FaseRibbon.oculto;
      notifyListeners();
      return;
    }

    _fase = FaseRibbon.sincronizando;
    _totalLote = cola.length;
    _enviados = 0;
    _duplicados = 0;
    _rechazados = 0;
    notifyListeners();

    for (final r in cola) {
      final endpoint = r.endpoint ?? endpointDeModulo[ModuloSeguimiento.fromId(r.tipo)];
      if (endpoint == null) {
        // Un módulo que esta versión no sabe enviar. Se marca y se sigue: la fila
        // queda visible en la pantalla de sincronización en vez de desaparecer.
        await _db.marcarEstado(r.id, EstadoSync.error,
            error: 'Esta versión de la app no sincroniza "${r.tipo}"');
        continue;
      }

      await _db.marcarEstado(r.id, EstadoSync.syncing);
      notifyListeners();

      try {
        final remoteId = await api.enviar(endpoint: endpoint, payload: r.payload);
        await _db.confirmarEnviado(r.id, remoteId: remoteId);
        await _db.marcarRegistrado(
          modulo: r.tipo, loteId: r.loteId, fecha: r.fecha, origen: 'servidor');
        _enviados++;
      } on ApiError catch (e) {
        final parar = await _manejarFallo(r, e);
        await _refrescarPendientes();
        notifyListeners();
        if (parar) {
          _fase = FaseRibbon.oculto;
          notifyListeners();
          return;
        }
        continue;
      } catch (e) {
        await _db.marcarEstado(r.id, EstadoSync.error,
            error: e.toString(), sumarIntento: true);
      }

      await _refrescarPendientes();
      notifyListeners();
    }

    _fase = FaseRibbon.exito;
    notifyListeners();
    await Future.delayed(demoraExito);
    _fase = FaseRibbon.oculto;
    notifyListeners();
  }

  /// Qué hacer con cada tipo de fallo. Devuelve `true` si hay que parar la cola.
  Future<bool> _manejarFallo(RegistroPendiente r, ApiError e) async {
    switch (e.tipo) {
      case TipoFallo.duplicado:
        // El día ya está en el servidor: el registro NO se perdió. Sale de la
        // cola como resuelto y se anota para no volver a ofrecer esa fecha.
        await _db.confirmarEnviado(r.id);
        await _db.marcarRegistrado(
          modulo: r.tipo, loteId: r.loteId, fecha: r.fecha, origen: 'servidor');
        _duplicados++;
        return false;

      case TipoFallo.plataformaRechazada:
        // NO se cierra la sesión: el usuario y su token están bien. Si se cerrara,
        // rotar el secreto en el servidor borraría la cola de todos los equipos.
        _avisoPlataforma = e.mensaje;
        await _db.marcarEstado(r.id, EstadoSync.pending, error: e.mensaje);
        return true;

      case TipoFallo.sesionVencida:
        _requiereRelogin = true;
        await _db.marcarEstado(r.id, EstadoSync.pending, error: e.mensaje);
        return true;

      case TipoFallo.sinRed:
        await _db.marcarEstado(r.id, EstadoSync.pending, error: e.mensaje);
        return true;

      case TipoFallo.datosInvalidos:
        // El backend rechazó el CONTENIDO: reintentar no lo va a arreglar solo.
        // Queda en error con el mensaje del servidor a la vista, y se le suelta
        // la marca del día para que el operario pueda volver a cargarlo — si no,
        // vería un día "registrado" que el servidor nunca aceptó.
        await _db.marcarEstado(r.id, EstadoSync.error,
            error: e.mensaje, sumarIntento: true);
        await _db.desmarcarRegistroLocal(
            modulo: r.tipo, loteId: r.loteId, fecha: r.fecha);
        _rechazados++;
        return false;

      case TipoFallo.servidor:
        await _db.marcarEstado(r.id, EstadoSync.error,
            error: e.mensaje, sumarIntento: true);
        return true;
    }
  }

  /// Baja qué días de este lote ya tiene el servidor y los deja en la caché.
  ///
  /// Sin esto, `registros_conocidos` sólo sabe lo que registró **este equipo
  /// desde que se instaló**. Una tablet nueva, un "borrar datos" o un segundo
  /// equipo en la misma granja dejan que el operario llene veinte campos, vea
  /// "Guardado" y el servidor lo rechace tres horas después.
  ///
  /// Es **por lote y a demanda**, no en la sincronización diaria: el endpoint es
  /// uno por lote, y con 124 lotes asignados serían 124 peticiones cada mañana.
  /// Se llama al abrir el formulario, que es cuando el dato sirve para algo.
  ///
  /// Falla en silencio: sin red se sigue con lo que haya en caché. Perder esta
  /// consulta no puede impedir registrar el día.
  Future<void> refrescarDiasDelServidor(Lote lote) async {
    final api = _api;
    if (api == null || !enLinea) return;
    try {
      final fechas = await api.fechasRegistradas(lote);
      await _db.reemplazarRegistrosDelServidor(
        modulo: lote.modulo.id,
        loteId: lote.id,
        fechas: fechas,
      );
    } on ApiError {
      // Sin red o error del servidor: la caché local sigue siendo la verdad
      // disponible. No es un fallo del formulario.
    }
  }

  /// Encola y devuelve inmediatamente: el usuario ya vio su confirmación.
  Future<void> encolar({
    required String tipo,
    required int loteId,
    required String loteNombre,
    required DateTime fecha,
    required Map<String, dynamic> payload,
    String? endpoint,
    bool marcaElDia = true,
  }) async {
    await _db.encolar(
      tipo: tipo,
      loteId: loteId,
      loteNombre: loteNombre,
      fecha: fecha,
      payload: payload,
      endpoint: endpoint,
    );
    // Se anota YA como registrado: sin red, este es el único dato que existe
    // para impedir que el mismo día se cargue dos veces (invariante I10).
    //
    // Sólo vale para el SEGUIMIENTO, que es uno por lote y por día. Un
    // movimiento (traslado, venta) es N-por-día: marcarlo haría dos daños a la
    // vez — el segundo traslado del día se descartaría como duplicado, y el
    // seguimiento de ese lote quedaría marcado como cargado sin estarlo.
    if (marcaElDia) {
      await _db.marcarRegistrado(modulo: tipo, loteId: loteId, fecha: fecha);
    }
    await _refrescarPendientes();
    notifyListeners();
    if (_autoSync && enLinea) sincronizar();
  }

  /// Se llama cuando el usuario vuelve a entrar: limpia el motivo por el que la
  /// cola se había detenido.
  void reanudar() {
    _requiereRelogin = false;
    _avisoPlataforma = null;
    notifyListeners();
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

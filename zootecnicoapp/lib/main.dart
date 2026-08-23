/// San Marino Zootécnico — app móvil.
/// Offline-first: todo registro se guarda en SQLite y se sincroniza cuando hay red.
library;

import 'package:flutter/material.dart';
import 'theme/app_theme.dart';
import 'theme/app_colors.dart';
import 'theme/app_spacing.dart';
import 'core/api/api_client.dart';
import 'core/api/auth_api.dart';
import 'core/api/inventario_api.dart';
import 'core/api/lotes_api.dart';
import 'core/api/seguimientos_api.dart';
import 'core/models.dart';
import 'core/models_inventario.dart';
import 'core/local_db.dart';
import 'core/platform_db/db_init.dart';
import 'core/session/session_store.dart';
import 'core/sync_service.dart';
import 'screens/login_screen.dart';
import 'screens/home_screen.dart';
import 'screens/app_screens.dart';
import 'screens/seguimiento_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // No-op fuera de web: sqflite no tiene backend nativo en el navegador, así
  // que sólo ahí hace falta darle uno (ver core/platform_db/).
  inicializarFactoryWebSiCorresponde();
  // La sesión se lee del disco ANTES de pintar: así un usuario que ya entró
  // alguna vez no ve el login por un instante al abrir la app sin señal.
  await SessionStore.instance.cargar();
  runApp(const SanMarinoApp());
}

class SanMarinoApp extends StatelessWidget {
  const SanMarinoApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'San Marino Zootécnico',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      home: const RootShell(),
    );
  }
}

class RootShell extends StatefulWidget {
  const RootShell({super.key});

  @override
  State<RootShell> createState() => _RootShellState();
}

class _RootShellState extends State<RootShell> {
  final SyncService _sync = SyncService();
  final SessionStore _sesion = SessionStore.instance;

  late final ApiClient _api = ApiClient(sesion: _sesion);
  late final AuthApi _auth = AuthApi(_api);
  late final LotesApi _lotesApi = LotesApi(_api);
  late final SeguimientosApi _segApi = SeguimientosApi(_api);
  late final InventarioApi _inventarioApi = InventarioApi(_api);

  Usuario? _usuario;
  List<Lote> _lotes = const [];
  int _tab = 0;
  ModuloSeguimiento? _filtroLotes;
  String? _mensajeLogin;

  @override
  void initState() {
    super.initState();
    _sync.init();
    _sync.addListener(_onSync);
    _restaurarSesion();
  }

  void _onSync() {
    if (!mounted) return;
    // El token venció mientras subía la cola: se vuelve al login SIN borrar los
    // registros pendientes, que son trabajo del usuario y no se tocan.
    if (_sync.requiereRelogin && _usuario != null) {
      _volverAlLogin('Tu sesión venció. Ingresá de nuevo para subir lo que quedó pendiente.');
      return;
    }
    setState(() {});
  }

  @override
  void dispose() {
    _sync.removeListener(_onSync);
    _sync.dispose();
    super.dispose();
  }

  /// Arranque con sesión guardada: se entra con lo que hay en caché y, si hay
  /// red, se refresca en segundo plano. Nunca se bloquea la entrada por la red.
  Future<void> _restaurarSesion() async {
    final u = _sesion.usuario;
    if (u == null) return;

    _sync.api = _segApi;
    final cacheados = await LocalDb.instance.lotesCacheados();
    if (!mounted) return;
    setState(() {
      _usuario = u;
      _lotes = cacheados;
    });

    if (_sync.enLinea) await _refrescarDesdeServidor(u, silencioso: true);
  }

  Future<void> _login(Usuario u) async {
    await _sesion.guardar(u);
    _sync.api = _segApi;
    _sync.reanudar();
    if (!mounted) return;
    setState(() {
      _usuario = u;
      _tab = 0;
      _mensajeLogin = null;
    });

    await _refrescarDesdeServidor(u);
    // Lo que quedó pendiente de una sesión anterior se sube ahora que hay token.
    if (_sync.enLinea) _sync.sincronizar();
  }

  /// Baja módulos y lotes y los deja en SQLite. Es la "sincronización diaria":
  /// después de esto el usuario puede trabajar todo el día sin señal.
  Future<void> _refrescarDesdeServidor(Usuario u, {bool silencioso = false}) async {
    try {
      final modulos = await _auth.modulos(companyId: u.companyId);
      final conModulos = u.copyWith(modulos: modulos);
      await _sesion.guardar(conModulos);

      final lotes = await _lotesApi.descargar(modulos: modulos);
      await LocalDb.instance.guardarLotes(lotes);

      // F5: sólo se baja el catálogo con el flag encendido — la empresa que no
      // lo usa no paga el peso de un catálogo que nunca va a mostrar.
      if (conModulos.descuentaInventarioDesdeMovil) {
        await _refrescarCatalogoInventario(lotes);
      }

      await _sesion.marcarSincronizado();

      if (!mounted) return;
      setState(() {
        _usuario = conModulos;
        _lotes = lotes;
      });
    } on ApiError catch (e) {
      if (!mounted) return;
      // Sin red se sigue con la caché: es el caso normal en la granja, no un fallo.
      if (e.tipo == TipoFallo.sinRed) {
        if (!silencioso) _avisar('Sin conexión: estás viendo los datos guardados.');
        return;
      }
      if (e.tipo == TipoFallo.sesionVencida) {
        _volverAlLogin('Tu sesión venció. Ingresá de nuevo.');
        return;
      }
      _avisar(e.mensaje);
    }
  }

  /// Catálogo + existencias, sólo para las granjas de los lotes que el usuario
  /// tiene asignados — bajar la empresa entera sería peso muerto en una tablet
  /// de una sola granja.
  ///
  /// Un fallo acá NO aborta la sincronización de lotes: si el catálogo no baja,
  /// el formulario cae al escalar de hoy (falla cerrada, ver [Usuario.descuentaInventarioDesdeMovil]),
  /// que sigue siendo un registro válido.
  Future<void> _refrescarCatalogoInventario(List<Lote> lotes) async {
    try {
      final catalogo = await _inventarioApi.catalogo();
      await LocalDb.instance.guardarCatalogo(catalogo);

      final farmIds = lotes.map((l) => l.granjaId).whereType<int>().toSet();
      final existencias = <ExistenciaInventario>[];
      for (final farmId in farmIds) {
        existencias.addAll(await _inventarioApi.existencias(farmId: farmId));
      }
      await LocalDb.instance.guardarExistencias(existencias);
    } on ApiError {
      // Sin red o error del servidor: se queda con lo que ya había en caché
      // (o vacío, en el primer login). No es un fallo de la sincronización.
    }
  }

  Future<void> _logout() async {
    // La cola NO se toca: lo que el usuario anotó sigue esperando a que alguien
    // vuelva a entrar y lo suba.
    await _sesion.cerrar();
    _sync.api = null;
    _sync.reanudar();
    if (!mounted) return;
    setState(() {
      _usuario = null;
      _lotes = const [];
      _tab = 0;
      _mensajeLogin = null;
    });
  }

  void _volverAlLogin(String motivo) {
    _sync.api = null;
    setState(() {
      _usuario = null;
      _tab = 0;
      _mensajeLogin = motivo;
    });
    _sesion.cerrar();
  }

  Future<void> _nuevoSeguimiento(ModuloSeguimiento? modulo, Lote? lote) async {
    final u = _usuario;
    if (u == null) return;

    var destino = lote;
    destino ??= await mostrarSelectorLote(
      context: context, usuario: u, lotes: _lotes, moduloPreseleccionado: modulo,
    );
    if (destino == null || !mounted) return;

    if (destino.cerrado) {
      _avisar('El lote ${destino.nombre} está cerrado: no admite registros nuevos.');
      return;
    }

    await Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => SeguimientoScreen(lote: destino!, usuario: u, sync: _sync),
    ));
  }

  void _verSync() {
    Navigator.of(context).push(MaterialPageRoute(builder: (_) => SyncScreen(sync: _sync)));
  }

  void _avisar(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mensaje)));
  }

  @override
  Widget build(BuildContext context) {
    final u = _usuario;
    if (u == null) {
      return LoginScreen(onLogin: _login, auth: _auth, mensajeInicial: _mensajeLogin);
    }

    return Scaffold(
      backgroundColor: AppColors.cream,
      body: SafeArea(
        bottom: false,
        child: switch (_tab) {
          0 => HomeScreen(
            usuario: u, lotes: _lotes, sync: _sync,
            onNuevoSeguimiento: _nuevoSeguimiento,
            onVerLotes: () => setState(() { _tab = 1; _filtroLotes = null; }),
            onVerSync: _verSync,
            onPerfil: () => setState(() => _tab = 2),
          ),
          1 => LotesScreen(
            usuario: u, lotes: _lotes, filtroInicial: _filtroLotes,
            onRegistrar: (l) => _nuevoSeguimiento(l.modulo, l),
          ),
          _ => PerfilScreen(usuario: u, onLogout: _logout),
        },
      ),
      bottomNavigationBar: _BottomNav(
        index: _tab,
        onTab: (i) => setState(() { _tab = i; _filtroLotes = null; }),
        onPlus: () => _nuevoSeguimiento(null, null),
      ),
    );
  }
}

/// Barra inferior flotante con FAB central de acción rápida.
class _BottomNav extends StatelessWidget {
  const _BottomNav({required this.index, required this.onTab, required this.onPlus});

  final int index;
  final ValueChanged<int> onTab;
  final VoidCallback onPlus;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Container(
        margin: const EdgeInsets.fromLTRB(AppSpacing.s3, 0, AppSpacing.s3, AppSpacing.s3),
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s2, vertical: AppSpacing.s2),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.xl),
          border: Border.all(color: AppColors.line),
          boxShadow: AppColors.shadowLg,
        ),
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceAround, children: [
          _tab(0, Icons.home_rounded, Icons.home_outlined, 'Inicio'),
          _tab(1, Icons.layers_rounded, Icons.layers_outlined, 'Lotes'),
          // FAB — sobresale de la barra
          Transform.translate(
            offset: const Offset(0, -22),
            child: GestureDetector(
              onTap: onPlus,
              child: Container(
                width: 56, height: 56,
                decoration: BoxDecoration(
                  color: AppColors.orange500,
                  shape: BoxShape.circle,
                  boxShadow: [BoxShadow(
                    color: AppColors.orange500.withValues(alpha: 0.4),
                    blurRadius: 16, offset: const Offset(0, 6),
                  )],
                ),
                child: const Icon(Icons.add_rounded, size: 28, color: Colors.white),
              ),
            ),
          ),
          _tab(2, Icons.person_rounded, Icons.person_outline_rounded, 'Perfil'),
          const SizedBox(width: 56), // equilibra el espacio del FAB
        ]),
      ),
    );
  }

  Widget _tab(int i, IconData activo, IconData inactivo, String label) {
    final on = index == i;
    return GestureDetector(
      onTap: () => onTab(i),
      child: Container(
        constraints: const BoxConstraints(minWidth: 56, minHeight: AppTouch.min),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: on ? AppColors.green50 : Colors.transparent,
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Column(mainAxisSize: MainAxisSize.min, mainAxisAlignment: MainAxisAlignment.center, children: [
          Icon(on ? activo : inactivo, size: 22, color: on ? AppColors.green600 : AppColors.ink300),
          const SizedBox(height: 3),
          Text(label, style: TextStyle(
            fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w600,
            color: on ? AppColors.green600 : AppColors.ink300,
          )),
        ]),
      ),
    );
  }
}

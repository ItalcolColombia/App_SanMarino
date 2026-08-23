/// San Marino Zootécnico — app móvil.
/// Offline-first: todo registro se guarda en SQLite y se sincroniza cuando hay red.
library;

import 'package:flutter/material.dart';
import 'package:zootecnicoapp/design_system/app_theme.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/api/inventario_api.dart';
import 'package:zootecnicoapp/core/api/lotes_api.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/models/models_inventario.dart';
import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/platform/db_init.dart';
import 'package:zootecnicoapp/core/session/session_store.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/features/auth/pages/login_page.dart';
import 'package:zootecnicoapp/features/home/pages/home_page.dart';
import 'package:zootecnicoapp/features/lotes/pages/lotes_page.dart';
import 'package:zootecnicoapp/features/lotes/widgets/selector_lote.dart';
import 'package:zootecnicoapp/features/perfil/pages/perfil_page.dart';
import 'package:zootecnicoapp/features/sync/pages/sync_page.dart';
import 'package:zootecnicoapp/features/sync/widgets/aviso_sesion_vencida.dart';
import 'package:zootecnicoapp/features/seguimiento/pages/seguimiento_page.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // No-op fuera de web: sqflite no tiene backend nativo en el navegador, así
  // que sólo ahí hace falta darle uno (ver core/platform/).
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

class _RootShellState extends State<RootShell> with WidgetsBindingObserver {
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

  /// El token venció, pero la sesión NO se destruye: la app queda en modo
  /// **sólo captura**. Ver [_marcarSesionVencida].
  bool _sesionVencida = false;

  @override
  void initState() {
    super.initState();
    _sync.init();
    _sync.addListener(_onSync);
    // Volver a la app es la señal más confiable de "puede que ahora haya red":
    // el operario registra en el galpón, cierra, y abre de vuelta en la oficina.
    WidgetsBinding.instance.addObserver(this);
    _restaurarSesion();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state != AppLifecycleState.resumed) return;
    if (_usuario == null) return;
    if (_sync.enLinea && _sync.pendientes > 0) _sync.sincronizar();
  }

  void _onSync() {
    if (!mounted) return;
    // El token venció mientras subía la cola. NO se expulsa al login: la app
    // pasa a sólo captura y la cola queda intacta esperando (ver
    // _marcarSesionVencida).
    if (_sync.requiereRelogin && _usuario != null && !_sesionVencida) {
      _marcarSesionVencida();
      return;
    }
    setState(() {});
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
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

    if (_sync.enLinea) {
      // Lo que quedo de la sesion anterior se sube ahora, sin esperar a que
      // el usuario toque el boton ni a que la conectividad parpadee.
      if (_sync.pendientes > 0) _sync.sincronizar();
      await _refrescarDesdeServidor(u, silencioso: true);
    }
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
        _marcarSesionVencida();
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

  /// El token venció: la app pasa a **sólo captura** en vez de expulsar.
  ///
  /// Antes acá se cerraba la sesión y se volvía al login. El problema es que el
  /// login **exige red**: si el token vencía y después el operario se quedaba
  /// sin señal, quedaba afuera de su propia app — sin poder seguir registrando
  /// el día y sin siquiera ver su cola pendiente, que es justo el momento en que
  /// más necesita las dos cosas.
  ///
  /// Ahora la sesión se conserva y sigue pudiendo registrar; lo único que se
  /// suspende es **subir**, que es lo que de verdad necesita un token válido
  /// (`_sync.api = null` deja la cola quieta sin tocarla — invariante I14).
  ///
  /// No abre ninguna puerta: todo lo que se ve ya estaba en el SQLite del
  /// equipo. Cerrar sesión a mano sigue borrando la sesión de verdad.
  void _marcarSesionVencida() {
    _sync.api = null;
    setState(() => _sesionVencida = true);
  }

  /// Vuelve a autenticar sin perder lo que hay en el equipo.
  Future<void> _reingresar() async {
    final u = await Navigator.of(context).push<Usuario>(
      rutaModal((_) => LoginPage(
            onLogin: (usuario) => Navigator.of(context).pop(usuario),
            auth: _auth,
            mensajeInicial: 'Tu sesión venció. Ingresá de nuevo para subir lo que anotaste.',
          )),
    );
    if (u == null || !mounted) return;

    await _sesion.guardar(u);
    _sync.api = _segApi;
    _sync.reanudar();
    setState(() {
      _usuario = u;
      _sesionVencida = false;
    });
    await _refrescarDesdeServidor(u);
    if (_sync.enLinea) _sync.sincronizar();
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

    // Modal y no ruta normal: el seguimiento es una tarea que se abre y se
    // cierra, no un nivel más de navegación.
    await Navigator.of(context).push(rutaModal(
      (_) => SeguimientoPage(lote: destino!, usuario: u, sync: _sync),
      nombre: 'seguimiento',
    ));
  }

  void _verSync() {
    Navigator.of(context).push(rutaApp((_) => SyncPage(sync: _sync), nombre: 'sync'));
  }

  void _avisar(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mensaje)));
  }

  @override
  Widget build(BuildContext context) {
    final u = _usuario;
    if (u == null) {
      return LoginPage(onLogin: _login, auth: _auth, mensajeInicial: _mensajeLogin);
    }

    return Scaffold(
      backgroundColor: AppColors.cream,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          // Mientras esté puesto, nada de lo que anote el operario llega al
          // servidor: va arriba de todo y en todas las pestañas.
          if (_sesionVencida) AvisoSesionVencida(onReingresar: _reingresar),
          Expanded(
            // La clave es el tab: sin ella el switcher no ve el cambio cuando
            // las dos pantallas son del mismo tipo de widget.
            child: CambioSuave(
              claveDeEstado: _tab,
              child: switch (_tab) {
                0 => HomePage(
                  usuario: u, lotes: _lotes, sync: _sync,
                  onNuevoSeguimiento: _nuevoSeguimiento,
                  onVerLotes: () => setState(() { _tab = 1; _filtroLotes = null; }),
                  onVerSync: _verSync,
                  onPerfil: () => setState(() => _tab = 2),
                ),
                1 => LotesPage(
                  usuario: u, lotes: _lotes, filtroInicial: _filtroLotes,
                  onRegistrar: (l) => _nuevoSeguimiento(l.modulo, l),
                ),
                _ => PerfilPage(usuario: u, onLogout: _logout),
              },
            ),
          ),
        ]),
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
///
/// El ítem activo se marca con una pastilla que **se desliza** entre ranuras: el
/// movimiento cuenta de dónde venía el foco, que es justo lo que un cambio seco
/// de color no dice. Para que ese deslizamiento sea calculable, el riel se
/// reparte en ranuras iguales — la misma geometría que ya daba `spaceAround` con
/// ítems del mismo ancho, así que nada se corre de lugar.
class _BottomNav extends StatelessWidget {
  const _BottomNav({required this.index, required this.onTab, required this.onPlus});

  final int index;
  final ValueChanged<int> onTab;
  final VoidCallback onPlus;

  /// Diámetro del FAB y, por eso mismo, alto del riel: es el elemento más alto
  /// de la barra.
  static const double _ladoFab = AppTouch.min + AppSpacing.s3;

  /// Cuánto sobresale el FAB por encima del riel.
  static const double _saltoFab = AppSpacing.s5;

  /// Ranuras del riel: los 3 tabs, la del FAB y una vacía al final que compensa
  /// el hueco que el FAB deja a su izquierda.
  static const int _ranuras = 5;

  /// Ranura de cada tab. La 2 es la del FAB, que no es seleccionable: por eso
  /// Perfil salta a la 3.
  static const List<int> _ranuraDeTab = [0, 1, 3];

  static const double _iconoTab = AppFontSize.xl;
  static const double _iconoFab = AppSpacing.s6 + AppSpacing.s1;

  @override
  Widget build(BuildContext context) {
    final ranuraActiva = _ranuraDeTab[index.clamp(0, _ranuraDeTab.length - 1)];

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
        child: LayoutBuilder(builder: (context, restricciones) {
          final anchoRanura = restricciones.maxWidth / _ranuras;

          // El riel tiene alto fijo (es lo que hace calculable el viaje de la
          // pastilla), así que las etiquetas no pueden crecer sin techo: con la
          // fuente del sistema al máximo desbordarían. Se topean acá y sólo acá
          // — son redundantes con el ícono, y el área de toque no se achica.
          return MediaQuery.withClampedTextScaling(
            maxScaleFactor: 1.3,
            child: SizedBox(
              height: _ladoFab,
              // El FAB sobresale del riel: con el clip por defecto se cortaría.
              child: Stack(clipBehavior: Clip.none, children: [
                // La pastilla va detrás de los ítems y viaja a la ranura activa.
                AnimatedPositioned(
                  duration: AppMotion.duracion(context, AppMotion.base),
                  curve: AppMotion.entrada,
                  left: ranuraActiva * anchoRanura + AppSpacing.s1,
                  top: AppSpacing.s1,
                  bottom: AppSpacing.s1,
                  width: anchoRanura - AppSpacing.s2,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: AppColors.brand50,
                      borderRadius: BorderRadius.circular(AppRadius.lg),
                    ),
                  ),
                ),
                // `stretch` para que cada ítem ocupe el alto entero de su ranura
                // y el toque no dependa de acertarle al ícono.
                Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  Expanded(child: _item(context, 0, Icons.home_rounded, Icons.home_outlined, 'Inicio')),
                  Expanded(child: _item(context, 1, Icons.layers_rounded, Icons.layers_outlined, 'Lotes')),
                  Expanded(child: Center(child: _fab(context))),
                  Expanded(child: _item(context, 2, Icons.person_rounded, Icons.person_outline_rounded, 'Perfil')),
                  const Spacer(),
                ]),
              ]),
            ),
          );
        }),
      ),
    );
  }

  /// FAB central: la acción de la app. Naranja de marca + sombra teñida, y se
  /// hunde al presionarlo para que el toque con guante se sienta.
  Widget _fab(BuildContext context) {
    return Transform.translate(
      offset: const Offset(0, -_saltoFab),
      child: PresionHundida(
        onTap: onPlus,
        // Transparente pero opaco al hit-test: hace que responda el círculo
        // entero y no sólo el glifo del ícono.
        child: ColoredBox(
          color: Colors.transparent,
          // En una pantalla angosta la ranura puede quedar por debajo del
          // diámetro del FAB; el `FittedBox` lo achica en proporción en vez de
          // dejar que el círculo se aplaste en óvalo.
          child: FittedBox(
            child: Container(
              width: _ladoFab,
              height: _ladoFab,
              decoration: BoxDecoration(
                color: AppColors.brand500,
                shape: BoxShape.circle,
                boxShadow: AppColors.shadowBrand,
              ),
              child: const Icon(Icons.add_rounded, size: _iconoFab, color: Colors.white),
            ),
          ),
        ),
      ),
    );
  }

  Widget _item(BuildContext context, int i, IconData activo, IconData inactivo, String etiqueta) {
    final on = index == i;
    // Naranja = acción, también en el nav. El verde de antes era el patrón
    // legacy que el front web ya había abandonado.
    final color = on ? AppColors.brand600 : AppColors.ink300;

    return GestureDetector(
      // Opaco: toda la ranura recibe el toque, no sólo el ícono y el texto.
      behavior: HitTestBehavior.opaque,
      onTap: () => onTab(i),
      child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
        AnimatedScale(
          // Un 8 %: el ícono activo pesa un poco más sin llegar a saltar.
          scale: on ? 1.08 : 1,
          duration: AppMotion.duracion(context, AppMotion.base),
          curve: AppMotion.entrada,
          // El color se interpola en vez de saltar, para que acompañe a la
          // pastilla en lugar de adelantársele.
          child: TweenAnimationBuilder<Color?>(
            tween: ColorTween(end: color),
            duration: AppMotion.duracion(context, AppMotion.fast),
            curve: AppMotion.entrada,
            builder: (context, tinte, _) => Icon(on ? activo : inactivo, size: _iconoTab, color: tinte ?? color),
          ),
        ),
        const SizedBox(height: AppSpacing.s1),
        AnimatedDefaultTextStyle(
          duration: AppMotion.duracion(context, AppMotion.fast),
          curve: AppMotion.entrada,
          style: TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.xs,
            fontWeight: on ? FontWeight.w700 : FontWeight.w600,
            color: color,
          ),
          child: Text(etiqueta),
        ),
      ]),
    );
  }
}

/// Formularios de seguimiento diario — el corazón de la app.
///
/// El **editor de ítems dinámicos** (`_ItemsEditor` / `_ItemRow`) se quitó al
/// conectar los formularios con el backend: capturaba alimento por ítem de
/// inventario, pero esta fase manda el consumo como campo suelto y ese editor no
/// llegaba a ninguna parte. Cuando se implemente el descuento de stock, el
/// componente está intacto en
/// `San Marino Zootécnico — Design System/movil/lib/screens/seguimiento_screen.dart`.
/// Los campos salen de los modales del web:
///   levante      → features/lote-levante/pages/modal-create-edit
///   engorde      → features/aves-engorde/pages/modal-seguimiento-engorde
///   produccion   → features/lote-produccion/pages/modal-seguimiento-diario
///   reproductora → features/seguimiento-diario-lote-reproductora/pages/modal-seguimiento-reproductora
library;

import 'package:flutter/material.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/features/sync/widgets/sync_widgets.dart';
import 'package:zootecnicoapp/features/seguimiento/widgets/selector_items_inventario.dart';
import 'package:zootecnicoapp/features/seguimiento/funciones/alimento_obligatorio.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/features/seguimiento/funciones/items_consumo.dart';
import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/models/models_inventario.dart';
import 'package:zootecnicoapp/core/reglas/postura_calculos.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';

class SeguimientoPage extends StatefulWidget {
  const SeguimientoPage({
    super.key,
    required this.lote,
    required this.usuario,
    required this.sync,
  });

  final Lote lote;
  final Usuario usuario;
  final SyncService sync;

  @override
  State<SeguimientoPage> createState() => _SeguimientoScreenState();
}

class _SeguimientoScreenState extends State<SeguimientoPage> {
  /// Cuánto queda la confirmación en pantalla antes de volver al listado. Sin
  /// red el mensaje tiene una línea más para leer, así que dura un poco más.
  /// No cambia qué se guardó ni el resultado que devuelve la pantalla.
  static const Duration _esperaConRed = Duration(milliseconds: 900);
  static const Duration _esperaSinRed = Duration(milliseconds: 1400);

  final Map<String, TextEditingController> _c = {};
  final Map<String, bool> _abierto = {'general': true};
  DateTime _fecha = DateTime.now();
  bool _guardado = false;
  /// El servidor (o este equipo) ya tiene el día elegido. Se avisa ANTES de que
  /// llene el formulario, no al guardar.
  bool _diaYaRegistrado = false;

  /// Si al momento de guardar había red. Se congela ahí y no se vuelve a mirar:
  /// el mensaje tiene que describir lo que pasó cuando el operario apretó, no
  /// el estado de la antena dos segundos después.
  bool _sinRedAlGuardar = false;

  // ── F5: selector de ítems de inventario (sólo con el flag encendido) ──────
  final List<LineaConsumo> _itemsH = [];
  final List<LineaConsumo> _itemsM = [];
  List<ItemInventario> _catalogoAlimento = const [];
  Map<String, ExistenciaInventario> _existencias = const {};

  TextEditingController ctl(String k) => _c.putIfAbsent(k, () {
    final c = TextEditingController();
    // La barra de obligatorias del header y el badge de cada sección se
    // calculan de estos campos: sin escuchar sus cambios el progreso quedaría
    // congelado hasta que otra cosa dispare un setState.
    if (_observados.contains(k)) c.addListener(_repintar);
    return c;
  });

  void _repintar() {
    if (mounted) setState(() {});
  }

  bool abierto(String k) => _abierto[k] ?? false;
  void toggle(String k) => setState(() => _abierto[k] = !abierto(k));
  bool lleno(List<String> keys) => keys.any((k) => (_c[k]?.text ?? '').isNotEmpty);

  ModuloSeguimiento get modulo => widget.lote.modulo;

  /// Kill switch de F5: con el flag apagado esta pantalla es BYTE A BYTE la de
  /// antes — el selector ni se pinta ni se consulta el catálogo.
  bool get _usaSelectorItems => widget.usuario.descuentaInventarioDesdeMovil;

  // ── Secciones obligatorias ─────────────────────────────────────────────────

  /// Las secciones que el registro no puede dejar en blanco, con los campos que
  /// en pantalla llevan `*`. Es la ÚNICA definición: de acá salen el badge
  /// "Obligatorio" de cada sección Y la barra de progreso del header, para que
  /// no puedan decir cosas distintas.
  ///
  /// `alimento` va con la lista vacía porque su condición no es "hay algo
  /// escrito" sino la regla del backend, que resuelve [_alimentoCompleto].
  Map<String, List<String>> get _obligatorias => switch (modulo) {
    ModuloSeguimiento.levante || ModuloSeguimiento.engorde => const {
      'alimento': <String>[],
      'mort': ['mortalidadHembras', 'mortalidadMachos'],
    },
    ModuloSeguimiento.produccion || ModuloSeguimiento.reproductora => const {
      'alimento': <String>[],
      'hembras': ['mortalidadHembras', 'selH'],
      'machos': ['mortalidadMachos', 'selM'],
    },
  };

  /// Campos cuyo tecleo tiene que repintar el progreso. `late` para que se
  /// arme recién cuando ya hay `widget` disponible.
  late final Set<String> _observados = {
    for (final campos in _obligatorias.values) ...campos,
    'tipoAlimento', 'consumoKgHembras', 'consumoKgMachos',
  };

  bool _completa(String clave) => clave == 'alimento'
      ? _alimentoCompleto
      : lleno(_obligatorias[clave] ?? const []);

  int get _obligatoriasListas => _obligatorias.keys.where(_completa).length;

  /// La regla real del alimento, espejo de [AlimentoObligatorio]: algún consumo
  /// POSITIVO y el tipo indicado. Se mira en positivo y no sólo "no vacío"
  /// porque un 0 tipeado no alcanza y el guardado lo rebotaría igual.
  bool get _alimentoCompleto {
    if (_usaSelectorItems) {
      return _itemsH.any((l) => l.valida) || _itemsM.any((l) => l.valida);
    }
    final kg = _numero('consumoKgHembras') + _numero('consumoKgMachos');
    return kg > 0 && (_c['tipoAlimento']?.text ?? '').trim().isNotEmpty;
  }

  /// Lectura tolerante de un campo numérico, SÓLO para pintar el progreso: el
  /// payload lo sigue armando [PayloadSeguimiento] con su propio parseo.
  double _numero(String k) =>
      double.tryParse((_c[k]?.text ?? '').trim().replaceAll(',', '.')) ?? 0;

  /// El badge se apaga cuando la sección se completa: lo que queda en rojo es
  /// exactamente lo que falta, y el punto verde de la sección toma la posta.
  Widget? _badgeObligatorio(String clave) => _completa(clave)
      ? null
      : const AppBadge(label: 'Obligatorio', tone: BadgeTone.danger);

  @override
  void initState() {
    super.initState();
    if (_usaSelectorItems) _cargarCatalogo();
    _revisarDiaElegido();
  }

  /// Pregunta al servidor qué días ya tiene de este lote y revisa el elegido.
  ///
  /// Antes esto se sabía recién al guardar, y sólo con lo que hubiera
  /// registrado ESTE equipo: en una tablet nueva el operario llenaba todo para
  /// que el servidor lo rechazara horas después. La consulta no bloquea nada —
  /// sin red se sigue con la caché.
  Future<void> _revisarDiaElegido() async {
    await widget.sync.refrescarDiasDelServidor(widget.lote);
    final ya = await LocalDb.instance.yaHayRegistro(
        modulo: modulo.id, loteId: widget.lote.id, fecha: _fecha);
    if (!mounted || ya == _diaYaRegistrado) return;
    setState(() => _diaYaRegistrado = ya);
  }

  /// Catálogo + existencias YA cacheados por la sincronización diaria
  /// (`main.dart:_refrescarCatalogoInventario`) — no hay red acá, sólo SQLite.
  /// Un catálogo vacío (primer login sin sincronizar, o empresa que recién
  /// prendió el flag) deja el selector sin ítems para elegir: el operario lo
  /// nota en el botón "Sin catálogo disponible sin conexión", no en un error.
  Future<void> _cargarCatalogo() async {
    final catalogo = await LocalDb.instance.catalogoCacheado(soloAlimento: true);
    final existencias = await LocalDb.instance.existenciasCacheadas();
    if (!mounted) return;
    setState(() {
      _catalogoAlimento = catalogo;
      _existencias = existencias;
    });
  }

  @override
  void dispose() {
    for (final c in _c.values) { c.dispose(); }
    super.dispose();
  }

  /// El cuerpo que se le manda al backend, armado por [PayloadSeguimiento]
  /// según el módulo y el país. Los cuatro módulos tienen mapeo.
  ///
  /// Con el selector de ítems encendido (F5), `tipoAlimento` y el consumo
  /// escalar se DERIVAN de lo elegido — el operario ya no los tipea — y recién
  /// después se le agrega el array `itemsHembras`/`itemsMachos` al payload que
  /// arma [PayloadSeguimiento]: es el array cuya sola presencia dispara el
  /// descuento de inventario en el backend.
  Map<String, dynamic> _payload() {
    final campos = {for (final e in _c.entries) e.key: e.value.text};
    final u = widget.usuario;

    if (_usaSelectorItems) {
      final nombres = [..._itemsH, ..._itemsM]
          .where((l) => l.valida)
          .map((l) => l.item.nombre)
          .toSet()
          .join(', ');
      if (nombres.isNotEmpty) campos['tipoAlimento'] = nombres;
      final kgH = ItemsConsumo.kgDeAlimento(_itemsH);
      final kgM = ItemsConsumo.kgDeAlimento(_itemsM);
      if (kgH > 0) campos['consumoKgHembras'] = kgH.toString();
      if (kgM > 0) campos['consumoKgMachos'] = kgM.toString();
    }

    final payload = switch (modulo) {
      ModuloSeguimiento.engorde => PayloadSeguimiento.engorde(
          loteId: widget.lote.id,
          fecha: _fecha,
          campos: campos,
          controlAgua: u.tieneControlAgua,
          quintales: u.capturaQuintales,
          usuarioId: u.id,
        ),
      ModuloSeguimiento.reproductora => PayloadSeguimiento.reproductora(
          loteId: widget.lote.id,
          fecha: _fecha,
          campos: campos,
          controlAgua: u.tieneControlAgua,
          quintales: u.capturaQuintales,
          usuarioId: u.id,
        ),
      ModuloSeguimiento.levante => PayloadSeguimiento.levante(
          // El lote MAESTRO va como `loteId`; el id de la etapa, aparte.
          loteId: widget.lote.loteMaestroId ?? widget.lote.id,
          lotePosturaLevanteId: widget.lote.id,
          fecha: _fecha,
          campos: campos,
          controlAgua: u.tieneControlAgua,
          quintales: u.capturaQuintales,
          usuarioId: u.id,
        ),
      ModuloSeguimiento.produccion => PayloadSeguimiento.produccion(
          lotePosturaProduccionId: widget.lote.id,
          fecha: _fecha,
          campos: campos,
          controlAgua: u.tieneControlAgua,
          // La etapa se calcula desde el encasetamiento del lote.
          fechaEncaset: widget.lote.fechaEncaset,
          usuarioId: u.id,
        ),
    };

    if (_usaSelectorItems) {
      // manejaSilos: false — F5.5 del plan: el flag no se enciende para
      // empresas con inventario por silo hasta que exista el selector de
      // silo en la app (hoy ninguna empresa con ese modelo tiene el flag).
      ItemsConsumo.aplicarEn(
        payload,
        itemsHembras: ItemsConsumo.armar(lineas: _itemsH, paisId: u.paisId, manejaSilos: false),
        itemsMachos: ItemsConsumo.armar(lineas: _itemsM, paisId: u.paisId, manejaSilos: false),
        modulo: modulo,
      );
    }

    return payload;
  }

  Future<void> _guardar() async {
    final payload = _payload();

    // El alimento es obligatorio (regla del 14ago26). Se comprueba ANTES de
    // encolar: si no, el registro rebotaría al sincronizar, cuando el usuario ya
    // no tiene el lote enfrente.
    final faltaAlimento = AlimentoObligatorio.motivo(
      modulo: modulo,
      kgHembras: (payload['consumoKgHembras'] ?? payload['consumoHembras']) as double?,
      kgMachos: (payload['consumoKgMachos'] ?? payload['consumoMachos']) as double?,
      tipoAlimento: payload['tipoAlimento'] as String?,
    );
    if (faltaAlimento != null) {
      setState(() => _abierto['alimento'] = true);
      _avisar(faltaAlimento);
      return;
    }

    // El backend sólo acepta un registro por lote y día. Avisar acá evita que el
    // usuario descubra el choque horas más tarde, cuando vuelva la señal.
    final yaEsta = await LocalDb.instance.yaHayRegistro(
      modulo: modulo.id, loteId: widget.lote.id, fecha: _fecha);
    if (yaEsta) {
      if (!mounted) return;
      _avisar('${widget.lote.nombre} ya tiene un registro del ${_fmtFecha(_fecha)}.');
      return;
    }

    // NUNCA confirmar antes de que el INSERT haya resuelto. Si se pinta el chip
    // "Guardado" y después el INSERT falla (disco lleno, DB bloqueada), el
    // usuario se va convencido de que anotó el día y el registro no existe en
    // ningún lado. Encolar primero, confirmar después: es el invariante I18.
    try {
      await widget.sync.encolar(
        tipo: modulo.id,
        loteId: widget.lote.id,
        loteNombre: widget.lote.nombre,
        fecha: _fecha,
        payload: payload,
        endpoint: endpointDeModulo[modulo],
      );
    } catch (e) {
      if (!mounted) return;
      // El formulario queda editable y con los datos puestos: lo último que
      // puede pasar es que el operario pierda lo que cargó.
      _avisar('No se pudo guardar en el equipo. Revisá el espacio disponible e intentá de nuevo.');
      return;
    }

    if (!mounted) return;
    // Se lee ACÁ, con el encolado ya resuelto: es lo que decide si la
    // confirmación puede decir "Guardado" a secas o tiene que aclarar que el
    // registro por ahora vive sólo en esta tablet.
    final sinRed = !widget.sync.enLinea;
    setState(() {
      _guardado = true;
      _sinRedAlGuardar = sinRed;
    });
    await Future.delayed(sinRed ? _esperaSinRed : _esperaConRed);
    if (mounted) Navigator.of(context).pop(true);
  }

  void _avisar(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(mensaje, style: const TextStyle(
        fontFamily: 'Inter', fontSize: AppFontSize.sm,
        fontWeight: FontWeight.w600, color: AppColors.cream,
      )),
      backgroundColor: AppColors.ink900,
      behavior: SnackBarBehavior.floating,
      margin: const EdgeInsets.all(AppSpacing.s4),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
    ));
  }

  Color get _acento => switch (modulo) {
    ModuloSeguimiento.levante      => AppColors.green600,
    ModuloSeguimiento.engorde      => AppColors.brand600,
    ModuloSeguimiento.produccion   => AppColors.produccion,
    ModuloSeguimiento.reproductora => AppColors.reproductora,
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      body: SafeArea(
        child: Column(children: [
          _header(),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s4),
              // Con guantes se arrastra más de lo que se toca: bajar el teclado
              // al desplazar evita tener que apuntar al botón "listo".
              keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
              children: [
                for (final (i, seccion) in _secciones().indexed) ...[
                  EntradaEscalonada(indice: i, child: _expandible(seccion)),
                  const SizedBox(height: AppSpacing.s3),
                ],
              ],
            ),
          ),
          _footer(),
        ]),
      ),
    );
  }

  /// `AppSection` muestra su contenido con un `if (expanded)`: el alto salta de
  /// golpe. `AnimatedSize` interpola ese salto sin tocar la primitiva
  /// compartida — el chevron ya rota adentro, en la misma duración.
  Widget _expandible(Widget seccion) => AnimatedSize(
    duration: AppMotion.duracion(context, AppMotion.fast),
    curve: AppMotion.simetrica,
    alignment: Alignment.topCenter,
    child: seccion,
  );

  // ── Header pegajoso ────────────────────────────────────────────────────────

  Widget _header() {
    return Container(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s3),
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(bottom: BorderSide(color: AppColors.line)),
        boxShadow: AppColors.shadowSm,
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          IconButton(
            onPressed: () => Navigator.of(context).pop(),
            icon: const Icon(Icons.arrow_back_rounded, size: 20),
            style: IconButton.styleFrom(
              backgroundColor: AppColors.cream2, foregroundColor: AppColors.ink700,
              minimumSize: const Size(38, 38),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
            ),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(modulo.label.toUpperCase(), maxLines: 1, overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontFamily: 'Inter', fontSize: AppFontSize.xs, fontWeight: FontWeight.w700,
                letterSpacing: 0.8, color: _acento, height: 1.1,
              )),
            Text(widget.lote.nombre, maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.lg, fontWeight: FontWeight.w800,
                letterSpacing: -0.4, color: AppColors.ink900, height: 1.2,
              )),
          ])),
          const SizedBox(width: AppSpacing.s2),
          AppBadge(label: 'Día ${widget.lote.dia}', tone: switch (modulo) {
            ModuloSeguimiento.levante => BadgeTone.success,
            ModuloSeguimiento.engorde => BadgeTone.orange,
            ModuloSeguimiento.produccion => BadgeTone.info,
            ModuloSeguimiento.reproductora => BadgeTone.neutral,
          }),
        ]),
        const SizedBox(height: AppSpacing.s2),
        Row(children: [
          Expanded(child: _ubicacion()),
          // El estado de la red va en el header y no al pie: es lo que decide
          // qué significa "Guardado" cuando el operario termine. Con todo al día
          // no se pinta NADA —la ausencia es el mensaje, regla del design
          // system—, y por eso el espaciador también se saltea.
          ListenableBuilder(
            listenable: widget.sync,
            builder: (_, _) => widget.sync.todoAlDia
                ? const SizedBox.shrink()
                : Padding(
                    padding: const EdgeInsets.only(left: AppSpacing.s2),
                    child: ConnectionChip(sync: widget.sync),
                  ),
          ),
        ]),
        if (_obligatorias.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.s3),
          _progresoObligatorias(),
        ],
      ]),
    );
  }

  /// Granja · galpón · aves en una sola línea: es contexto, no dato de carga.
  Widget _ubicacion() {
    const tenue = TextStyle(fontFamily: 'Inter', fontSize: AppFontSize.xs, color: AppColors.ink300);
    const fuerte = TextStyle(
      fontFamily: 'Inter', fontSize: AppFontSize.xs, fontWeight: FontWeight.w600,
      color: AppColors.ink700, fontFeatures: [FontFeature.tabularFigures()],
    );
    return Text.rich(
      TextSpan(children: [
        TextSpan(text: widget.lote.granja, style: fuerte),
        const TextSpan(text: '  ·  ', style: tenue),
        TextSpan(text: 'Galpón ${widget.lote.galpon}', style: fuerte),
        const TextSpan(text: '  ·  ', style: tenue),
        TextSpan(text: '${_fmt(widget.lote.aves)} aves', style: fuerte),
      ]),
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
    );
  }

  /// Cuánto falta para que el día se pueda guardar. Naranja mientras es una
  /// tarea pendiente; verde recién cuando está todo, que ahí sí es un éxito.
  Widget _progresoObligatorias() {
    final total = _obligatorias.length;
    final listas = _obligatoriasListas;
    final completo = listas == total;

    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Row(children: [
        Expanded(child: Text(
          completo ? 'Secciones obligatorias completas' : 'Secciones obligatorias',
          style: TextStyle(
            fontFamily: 'Inter', fontSize: AppFontSize.xs, fontWeight: FontWeight.w600,
            color: completo ? AppColors.green600 : AppColors.ink500,
          ),
        )),
        Text('$listas/$total', style: TextStyle(
          fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.xs, fontWeight: FontWeight.w700,
          color: completo ? AppColors.green600 : AppColors.ink700,
          fontFeatures: const [FontFeature.tabularFigures()],
        )),
      ]),
      const SizedBox(height: AppSpacing.s1),
      ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        child: TweenAnimationBuilder<double>(
          tween: Tween(begin: 0, end: total == 0 ? 0 : listas / total),
          duration: AppMotion.duracion(context, AppMotion.base),
          curve: AppMotion.entrada,
          builder: (_, valor, _) => LinearProgressIndicator(
            value: valor,
            minHeight: AppSpacing.s1,
            backgroundColor: AppColors.cream2,
            color: completo ? AppColors.green500 : AppColors.brand500,
          ),
        ),
      ),
    ]);
  }

  // ── Pie: acciones y confirmación ───────────────────────────────────────────

  Widget _footer() {
    return Container(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s3),
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.line)),
      ),
      // Las dos caras del pie no miden lo mismo: se cambian con un fundido y el
      // alto se acompaña, en vez de dar el salto justo al confirmar.
      child: AnimatedSize(
        duration: AppMotion.duracion(context, AppMotion.fast),
        curve: AppMotion.simetrica,
        alignment: Alignment.topCenter,
        child: CambioSuave(
          claveDeEstado: _guardado,
          child: _guardado ? _confirmacionGuardado() : _acciones(),
        ),
      ),
    );
  }

  Widget _acciones() => Row(children: [
    AppButton(
      label: 'Cancelar', variant: AppButtonVariant.ghost,
      onPressed: () => Navigator.of(context).pop(),
    ),
    const SizedBox(width: AppSpacing.s2),
    // Naranja de marca: guardar es LA acción de esta pantalla. La sombra teñida
    // la despega del pie blanco, que es donde el pulgar la busca sin mirar.
    Expanded(child: DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.md),
        boxShadow: AppColors.shadowBrand,
      ),
      child: AppButton(
        label: 'Guardar registro',
        icon: Icons.check_rounded,
        full: true,
        variant: AppButtonVariant.primary,
        onPressed: _guardar,
      ),
    )),
  ]);

  /// El mensaje dice la verdad de dónde quedó el registro. Sin señal NO es un
  /// error —es el modo normal de trabajo en el galpón—, así que se pinta en el
  /// tono informativo, nunca en rojo ni con íconos de alarma.
  Widget _confirmacionGuardado() {
    final (fondo, tinta, titulo, detalle) = _sinRedAlGuardar
        ? (AppColors.infoBg, AppColors.info, 'Guardado en el equipo',
            'Se envía al volver la señal')
        : (AppColors.successBg, AppColors.green600, 'Guardado', null);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
      constraints: const BoxConstraints(minHeight: AppTouch.min),
      decoration: BoxDecoration(color: fondo, borderRadius: BorderRadius.circular(AppRadius.md)),
      child: Row(children: [
        TweenAnimationBuilder<double>(
          tween: Tween(begin: 0, end: 1),
          duration: AppMotion.duracion(context, AppMotion.base),
          curve: AppMotion.confirmacion,
          builder: (_, t, hijo) => Transform.scale(
            scale: t,
            // La curva de confirmación pasa de 1 al rebotar: la opacidad se
            // acota o `Opacity` revienta.
            child: Opacity(opacity: t.clamp(0.0, 1.0), child: hijo),
          ),
          child: Container(
            width: 28, height: 28,
            decoration: BoxDecoration(color: tinta, shape: BoxShape.circle),
            child: const Icon(Icons.check_rounded, size: 16, color: AppColors.surface),
          ),
        ),
        const SizedBox(width: AppSpacing.s3),
        Expanded(child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(titulo, style: TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.sm,
              fontWeight: FontWeight.w700, color: tinta,
            )),
            if (detalle != null) Text(detalle, style: TextStyle(
              fontFamily: 'Inter', fontSize: AppFontSize.xs, color: tinta, height: 1.3,
            )),
          ],
        )),
      ]),
    );
  }

  // ── Secciones por módulo ───────────────────────────────────────────────────

  List<Widget> _secciones() => switch (modulo) {
    ModuloSeguimiento.levante      => _levante(),
    ModuloSeguimiento.engorde      => _engorde(),
    ModuloSeguimiento.produccion   => _produccion(),
    ModuloSeguimiento.reproductora => _reproductora(),
  };

  Widget _general() => AppSection(
    title: 'General',
    icon: Icons.calendar_today_rounded,
    expanded: abierto('general'),
    onToggle: () => toggle('general'),
    filled: true,
    children: [
      if (_diaYaRegistrado)
        Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.s3),
          child: AppInfoBox(
            tone: InfoTone.warn,
            text: 'Este día ya está registrado para ${widget.lote.nombre}. Si lo guardás, el servidor lo va a rechazar.',
          ),
        ),
      _fechaField(),
      AppField(label: 'Lote', controller: TextEditingController(text: widget.lote.nombre), readOnly: true),
      AppField(label: 'Observaciones', controller: ctl('observaciones'),
        placeholder: 'Novedades del día…', maxLines: 3),
    ],
  );

  /// Desde cuándo se puede registrar. El backend rechaza toda fecha anterior al
  /// encasetamiento del lote, así que el picker no la ofrece: es mejor no poder
  /// elegirla que llenar el formulario y comerse el rechazo al sincronizar.
  ///
  /// El piso operativo son 30 días atrás; para un lote encasetado hace menos, el
  /// límite es su propio encasetamiento.
  DateTime get _fechaMinima {
    final piso = DateTime.now().subtract(const Duration(days: 30));
    final encaset = widget.lote.fechaEncaset;
    if (encaset == null) return piso;
    final soloDia = DateTime(encaset.year, encaset.month, encaset.day);
    return soloDia.isAfter(piso) ? soloDia : piso;
  }

  Widget _fechaField() => InkWell(
    onTap: () async {
      final minima = _fechaMinima;
      final d = await showDatePicker(
        context: context,
        initialDate: _fecha.isBefore(minima) ? minima : _fecha,
        firstDate: minima,
        lastDate: DateTime.now(),
      );
      if (d != null) {
        setState(() => _fecha = d);
        _revisarDiaElegido();
      }
    },
    child: AppField(
      label: 'Fecha', required: true, readOnly: true,
      controller: TextEditingController(text: _fmtFecha(_fecha)),
    ),
  );

  Widget _mortalidadSec({bool errSexaje = true}) => AppSection(
    title: 'Mortalidad y selección',
    icon: Icons.remove_circle_outline_rounded,
    expanded: abierto('mort'),
    onToggle: () => toggle('mort'),
    filled: _completa('mort'),
    trailing: _badgeObligatorio('mort'),
    children: [
      AppPairField(label: 'Mortalidad', required: true, suffix: 'aves',
        hController: ctl('mortalidadHembras'), mController: ctl('mortalidadMachos')),
      AppPairField(label: 'Selección (retiradas)', suffix: 'aves',
        hController: ctl('selH'), mController: ctl('selM')),
      if (errSexaje)
        AppPairField(label: 'Error de sexaje', suffix: 'aves',
          hController: ctl('errorSexajeHembras'), mController: ctl('errorSexajeMachos')),
    ],
  );

  /// ⚠️ El default es **kg**, no g. El web rotula este mismo campo
  /// (`pesoPromH`/`pesoPromM`) como "Peso promedio (kg)" con step 0.01. Cuando
  /// acá decía "g", el operario tipeaba 850 donde por web se tipea 0,85: el
  /// mismo dato entrando con un factor 1000 de diferencia, sin conversión en
  /// ningún lado. El peso del HUEVO sí va en gramos y está aparte.
  Widget _pesoSec({String suffix = 'kg', bool obligatorio = false, String? nota}) => AppSection(
    title: 'Peso y uniformidad',
    icon: Icons.monitor_weight_outlined,
    expanded: abierto('peso'),
    onToggle: () => toggle('peso'),
    filled: lleno(['pesoPromH', 'pesoPromM']),
    trailing: obligatorio ? const AppBadge(label: 'Día de pesaje', tone: BadgeTone.warning) : null,
    children: [
      if (nota != null) AppInfoBox(text: nota),
      AppPairField(label: 'Peso promedio', suffix: suffix, required: obligatorio,
        hController: ctl('pesoPromH'), mController: ctl('pesoPromM')),
      AppPairField(label: 'Uniformidad', suffix: '%',
        hController: ctl('uniformidadH'), mController: ctl('uniformidadM')),
      AppPairField(label: 'Coef. variación (CV)', suffix: '%',
        hController: ctl('cvH'), mController: ctl('cvM')),
    ],
  );

  /// Solo Ecuador y Panamá registran calidad de agua.
  /// Devuelve lista vacía cuando no aplica, para poder usar spread.
  List<Widget> _aguaSec() {
    if (!widget.usuario.tieneControlAgua) return const [];
    return [AppSection(
      title: 'Información del agua',
      icon: Icons.water_drop_outlined,
      expanded: abierto('agua'),
      onToggle: () => toggle('agua'),
      filled: lleno(['consumoAguaDiario']),
      children: [
        AppField(label: 'Consumo diario', suffix: 'L', hint: 'litros totales',
          controller: ctl('consumoAguaDiario'), placeholder: 'Ej: 1500.50',
          keyboardType: const TextInputType.numberWithOptions(decimal: true)),
        Row(children: [
          Expanded(child: AppField(label: 'pH', controller: ctl('consumoAguaPh'), placeholder: '7.0',
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'Temperatura', suffix: '°C', controller: ctl('consumoAguaTemperatura'),
            placeholder: '25.5', keyboardType: const TextInputType.numberWithOptions(decimal: true))),
        ]),
        AppField(label: 'ORP (potencial redox)', suffix: 'mV', controller: ctl('consumoAguaOrp'),
          placeholder: '650', keyboardType: const TextInputType.numberWithOptions(decimal: true)),
      ],
    )];
  }


  /// Alimento consumido en el día.
  ///
  /// **Con el flag [Usuario.descuentaInventarioDesdeMovil] apagado** (toda
  /// empresa hoy) esta sección es BYTE A BYTE la de antes: consumo directo en
  /// kg, que es lo que el backend acepta sin descontar inventario.
  ///
  /// **Encendido**, el campo de texto libre y el consumo escalar se
  /// REEMPLAZAN por el selector de ítems del catálogo real (F5.2/F0.2#4): el
  /// operario elige de lo que hay en el galpón, no lo tipea. `tipoAlimento` y
  /// el consumo escalar se siguen mandando —[_payload] los deriva de lo
  /// elegido— porque el backend y el reporte diario los siguen leyendo.
  Widget _alimentoSec({Color? acento}) => AppSection(
    title: 'Alimento',
    icon: Icons.grass_rounded,
    expanded: abierto('alimento'),
    onToggle: () => toggle('alimento'),
    filled: _alimentoCompleto,
    trailing: _badgeObligatorio('alimento'),
    children: _usaSelectorItems ? _selectorAlimentoChildren(acento) : [
      AppField(label: 'Tipo de alimento', required: true, controller: ctl('tipoAlimento'),
        placeholder: 'Ej: Iniciación, Engorde 1…'),
      AppPairField(label: 'Consumo', suffix: 'kg', required: true,
        hController: ctl('consumoKgHembras'), mController: ctl('consumoKgMachos')),
    ],
  );

  List<Widget> _selectorAlimentoChildren(Color? acento) {
    final color = acento ?? AppColors.brand600;
    final ubicacion = (
      farmId: widget.lote.granjaId,
      nucleoId: widget.lote.nucleoId,
      galponId: widget.lote.galponId,
    );
    return [
      if (widget.lote.granjaId == null)
        const AppInfoBox(
          text: 'No se pudo resolver la granja de este lote: el disponible no se puede mostrar, '
              'pero el registro se puede guardar igual.',
          tone: InfoTone.warn,
        ),
      Text('Hembras', style: TextStyle(
        fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w700, color: color,
      )),
      const SizedBox(height: AppSpacing.s2),
      SelectorItemsInventario(
        lineas: _itemsH, catalogo: _catalogoAlimento, existencias: _existencias,
        acento: color, onChanged: () => setState(() {}),
        farmId: ubicacion.farmId, nucleoId: ubicacion.nucleoId, galponId: ubicacion.galponId,
      ),
      const SizedBox(height: AppSpacing.s3),
      Text('Machos', style: TextStyle(
        fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w700, color: color,
      )),
      const SizedBox(height: AppSpacing.s2),
      SelectorItemsInventario(
        lineas: _itemsM, catalogo: _catalogoAlimento, existencias: _existencias,
        acento: color, onChanged: () => setState(() {}),
        farmId: ubicacion.farmId, nucleoId: ubicacion.nucleoId, galponId: ubicacion.galponId,
      ),
    ];
  }

  /// Quintales por categoría — sólo Panamá los captura. Devuelve lista vacía
  /// cuando no aplica, para poder usar spread.
  List<Widget> _quintalesSec() {
    if (!widget.usuario.capturaQuintales) return const [];
    return [AppSection(
      title: 'Alimento en quintales',
      icon: Icons.scale_rounded,
      expanded: abierto('qq'),
      onToggle: () => toggle('qq'),
      filled: lleno(['qqMixtas', 'qqHembras', 'qqMachos']),
      children: [
        AppField(label: 'Mixtas', suffix: 'qq', controller: ctl('qqMixtas'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true)),
        AppPairField(label: 'Por sexo', suffix: 'qq',
          hController: ctl('qqHembras'), mController: ctl('qqMachos')),
      ],
    )];
  }

  // ── LEVANTE ────────────────────────────────────────────────────────────────
  List<Widget> _levante() => [
    _general(),
    _alimentoSec(acento: AppColors.green600),
    ..._quintalesSec(),
    _mortalidadSec(),
    _pesoSec(nota: 'Solo registrar en el día de pesaje semanal.'),
    ..._aguaSec(),
  ];

  // ── POLLO ENGORDE ──────────────────────────────────────────────────────────
  List<Widget> _engorde() => [
    _general(),
    _alimentoSec(acento: AppColors.brand600),
    ..._quintalesSec(),
    _mortalidadSec(),
    _pesoSec(suffix: 'kg', obligatorio: true,
      nota: 'Peso obligatorio en los primeros 7 días y en el día de pesaje.'),
    ..._aguaSec(),
  ];

  // ── PRODUCCIÓN ─────────────────────────────────────────────────────────────
  static const _huevosIncubables = [
    ('huevoLimpio', 'Limpio'), ('huevoTratado', 'Tratado'),
  ];
  static const _huevosNoIncubables = [
    ('huevoSucio', 'Sucio'), ('huevoDeforme', 'Deforme'), ('huevoBlanco', 'Blanco'),
    ('huevoDobleYema', 'Doble yema'), ('huevoPiso', 'De piso'), ('huevoPequeno', 'Pequeño'),
    ('huevoRoto', 'Roto'), ('huevoDesecho', 'Desecho'), ('huevoOtro', 'Otro'),
  ];

  /// Los totales de la clasificadora los calcula [PosturaCalculos], no esta
  /// pantalla: son el MISMO número que viaja en el payload, y dos
  /// implementaciones del mismo número terminan divergiendo (regla del repo).
  /// Acá sólo se conservan las etiquetas, que son cosa de la UI.
  TotalesHuevos get _totales => PosturaCalculos.totalesClasificadora(
      {for (final e in _c.entries) e.key: e.value.text});

  int get _totalHuevos => _totales.total;
  int get _incubables => _totales.incubables;

  List<Widget> _produccion() => [
    AppSection(
      title: 'General',
      icon: Icons.calendar_today_rounded,
      expanded: abierto('general'), onToggle: () => toggle('general'), filled: true,
      children: [
        _fechaField(),
        _EtapaCalculada(
          etapa: PosturaCalculos.etapa(
            fechaEncaset: widget.lote.fechaEncaset, fechaRegistro: _fecha),
        ),
        AppField(label: 'Ciclo', controller: ctl('ciclo'), placeholder: 'Normal'),
        AppField(label: 'Observaciones', controller: ctl('observaciones'),
          placeholder: 'Novedades del día…', maxLines: 3),
      ],
    ),
    AppSection(
      title: 'Hembras ♀',
      icon: Icons.female_rounded,
      expanded: abierto('hembras'), onToggle: () => toggle('hembras'),
      filled: _completa('hembras'),
      trailing: _badgeObligatorio('hembras'),
      children: [
        Row(children: [
          Expanded(child: AppField(label: 'Mortalidad', required: true, controller: ctl('mortalidadHembras'),
            keyboardType: TextInputType.number)),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'SelH (retiradas)', required: true, controller: ctl('selH'),
            keyboardType: TextInputType.number)),
        ]),
        Row(children: [
          Expanded(child: AppField(label: 'Uniformidad', suffix: '%', controller: ctl('uniformidadHembras'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'CV', suffix: '%', controller: ctl('cvHembras'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
        ]),
      ],
    ),
    AppSection(
      title: 'Machos ♂',
      icon: Icons.male_rounded,
      expanded: abierto('machos'), onToggle: () => toggle('machos'),
      filled: _completa('machos'),
      trailing: _badgeObligatorio('machos'),
      children: [
        Row(children: [
          Expanded(child: AppField(label: 'Mortalidad', required: true, controller: ctl('mortalidadMachos'),
            keyboardType: TextInputType.number)),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'SelM (retiradas)', required: true, controller: ctl('selM'),
            keyboardType: TextInputType.number)),
        ]),
        Row(children: [
          Expanded(child: AppField(label: 'Uniformidad', suffix: '%', controller: ctl('uniformidadMachos'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'CV', suffix: '%', controller: ctl('cvMachos'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
        ]),
      ],
    ),
    _alimentoSec(acento: AppColors.produccion),
    AppSection(
      title: _totalHuevos > 0 ? 'Huevos clasificadora · ${_fmt(_totalHuevos)}' : 'Huevos clasificadora',
      icon: Icons.egg_outlined,
      expanded: abierto('huevos'), onToggle: () => toggle('huevos'),
      filled: _totalHuevos > 0,
      children: [
        const Text('INCUBABLES', style: TextStyle(
          fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
          letterSpacing: 0.8, color: AppColors.produccion,
        )),
        Row(children: [
          for (int i = 0; i < _huevosIncubables.length; i++) ...[
            if (i > 0) const SizedBox(width: AppSpacing.s2),
            Expanded(child: AppField(
              label: _huevosIncubables[i].$2,
              controller: ctl(_huevosIncubables[i].$1),
              keyboardType: TextInputType.number,
              onChanged: (_) => setState(() {}),
            )),
          ],
        ]),
        const Text('NO INCUBABLES', style: TextStyle(
          fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
          letterSpacing: 0.8, color: AppColors.ink500,
        )),
        GridView.count(
          crossAxisCount: 3, shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: AppSpacing.s2, crossAxisSpacing: AppSpacing.s2,
          childAspectRatio: 1.55,
          children: [
            for (final h in _huevosNoIncubables)
              AppField(label: h.$2, controller: ctl(h.$1),
                keyboardType: TextInputType.number, onChanged: (_) => setState(() {})),
          ],
        ),
        AppField(label: 'Peso promedio huevo', suffix: 'g', required: true,
          controller: ctl('pesoHuevo'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true)),
        if (_totalHuevos > 0)
          Row(children: [
            Expanded(child: AppStatTile(label: 'Total', value: _fmt(_totalHuevos), background: AppColors.cream2)),
            const SizedBox(width: AppSpacing.s2),
            Expanded(child: AppStatTile(label: 'Incubables', value: _fmt(_incubables),
              color: AppColors.green600, background: AppColors.successBg)),
          ]),
      ],
    ),
    AppSection(
      title: 'Pesaje semanal',
      icon: Icons.monitor_weight_outlined,
      expanded: abierto('pesaje'), onToggle: () => toggle('pesaje'),
      filled: lleno(['pesoH', 'pesoM']),
      children: [
        const AppInfoBox(text: 'Solo en el día de pesaje semanal asignado.'),
        AppPairField(label: 'Peso aves', suffix: 'kg',
          hController: ctl('pesoH'), mController: ctl('pesoM')),
        Row(children: [
          Expanded(child: AppField(label: 'Uniformidad global', suffix: '%', controller: ctl('uniformidad'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'CV', suffix: '%', controller: ctl('coeficienteVariacion'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true))),
        ]),
      ],
    ),
    ..._aguaSec(),
  ];

  // ── REPRODUCTORA ───────────────────────────────────────────────────────────
  List<Widget> _reproductora() => [
    AppSection(
      title: 'General',
      icon: Icons.calendar_today_rounded,
      expanded: abierto('general'), onToggle: () => toggle('general'), filled: true,
      children: [
        _fechaField(),
        // El lote seleccionado YA es la reproductora: pedirla de nuevo abriría la
        // puerta a que no coincidan y el registro caiga en otro lote.
        AppField(label: 'Reproductora', readOnly: true,
          controller: TextEditingController(text: widget.lote.nombre)),
        AppField(label: 'Ciclo', controller: ctl('ciclo'), placeholder: 'Normal'),
        AppField(label: 'Observaciones', controller: ctl('observaciones'),
          placeholder: 'Novedades del día…', maxLines: 3),
      ],
    ),
    _alimentoSec(acento: AppColors.reproductora),
    ..._quintalesSec(),
    AppSection(
      title: 'Hembras ♀ — bajas',
      icon: Icons.female_rounded,
      expanded: abierto('hembras'), onToggle: () => toggle('hembras'),
      filled: _completa('hembras'),
      trailing: _badgeObligatorio('hembras'),
      children: [
        Row(children: [
          Expanded(child: AppField(label: 'Mortalidad', required: true, controller: ctl('mortalidadHembras'),
            keyboardType: TextInputType.number)),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'Selección', required: true, controller: ctl('selH'),
            keyboardType: TextInputType.number)),
        ]),
        AppField(label: 'Error de sexaje', required: true, controller: ctl('errorSexajeHembras'),
          keyboardType: TextInputType.number),
      ],
    ),
    AppSection(
      title: 'Machos ♂ — bajas',
      icon: Icons.male_rounded,
      expanded: abierto('machos'), onToggle: () => toggle('machos'),
      filled: _completa('machos'),
      trailing: _badgeObligatorio('machos'),
      children: [
        Row(children: [
          Expanded(child: AppField(label: 'Mortalidad', required: true, controller: ctl('mortalidadMachos'),
            keyboardType: TextInputType.number)),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: AppField(label: 'Selección', required: true, controller: ctl('selM'),
            keyboardType: TextInputType.number)),
        ]),
        AppField(label: 'Error de sexaje', required: true, controller: ctl('errorSexajeMachos'),
          keyboardType: TextInputType.number),
      ],
    ),
    AppSection(
      title: 'Peso y uniformidad',
      icon: Icons.monitor_weight_outlined,
      expanded: abierto('peso'), onToggle: () => toggle('peso'),
      filled: lleno(['pesoPromH', 'pesoPromM']),
      children: [
        AppPairField(label: 'Peso promedio', suffix: 'kg',
          hController: ctl('pesoPromH'), mController: ctl('pesoPromM')),
        AppPairField(label: 'Uniformidad', suffix: '%',
          hController: ctl('uniformidadH'), mController: ctl('uniformidadM')),
        AppPairField(label: 'CV', suffix: '%',
          hController: ctl('cvH'), mController: ctl('cvM')),
      ],
    ),
    ..._aguaSec(),
  ];
}

// ═══════════════════════════════════════════════════════════════════════════
// Editor de ítems dinámicos (alimento, medicamentos…)
// ═══════════════════════════════════════════════════════════════════════════


/// La etapa NO se elige: se deriva del encasetamiento y la fecha del registro,
/// igual que en el web (que deshabilita el campo y lo calcula). Un desplegable
/// editable dejaba mandar una etapa que no corresponde al día, y el reporte
/// semanal agrupa por ese número.
///
/// El rango correcto es **26**-33 / 34-50 / >50 — no 25-33, como decía este
/// mismo campo antes: el cálculo que produce el dato hace `max(26, …)`.
class _EtapaCalculada extends StatelessWidget {
  const _EtapaCalculada({required this.etapa});

  final int etapa;

  static const _rangos = {
    1: 'semana 26–33',
    2: 'semana 34–50',
    3: 'semana >50',
  };

  @override
  Widget build(BuildContext context) {
    return AppField(
      label: 'Etapa de producción',
      readOnly: true,
      controller: TextEditingController(
        text: 'Etapa $etapa (${_rangos[etapa] ?? '—'})',
      ),
      hint: 'Se calcula desde el encasetamiento del lote',
    );
  }
}

// ── Formato es-CO: miles con punto ───────────────────────────────────────────
String _fmt(int n) => n.toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.');

String _fmtFecha(DateTime d) {
  const meses = ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'];
  return '${d.day} ${meses[d.month - 1]} ${d.year}';
}

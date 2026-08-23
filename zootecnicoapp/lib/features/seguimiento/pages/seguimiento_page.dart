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
  final Map<String, TextEditingController> _c = {};
  final Map<String, bool> _abierto = {'general': true};
  DateTime _fecha = DateTime.now();
  bool _guardado = false;

  // ── F5: selector de ítems de inventario (sólo con el flag encendido) ──────
  final List<LineaConsumo> _itemsH = [];
  final List<LineaConsumo> _itemsM = [];
  List<ItemInventario> _catalogoAlimento = const [];
  Map<String, ExistenciaInventario> _existencias = const {};

  TextEditingController ctl(String k) => _c.putIfAbsent(k, () => TextEditingController());
  bool abierto(String k) => _abierto[k] ?? false;
  void toggle(String k) => setState(() => _abierto[k] = !abierto(k));
  bool lleno(List<String> keys) => keys.any((k) => (_c[k]?.text ?? '').isNotEmpty);

  ModuloSeguimiento get modulo => widget.lote.modulo;

  /// Kill switch de F5: con el flag apagado esta pantalla es BYTE A BYTE la de
  /// antes — el selector ni se pinta ni se consulta el catálogo.
  bool get _usaSelectorItems => widget.usuario.descuentaInventarioDesdeMovil;

  @override
  void initState() {
    super.initState();
    if (_usaSelectorItems) _cargarCatalogo();
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
    setState(() => _guardado = true);
    await Future.delayed(const Duration(milliseconds: 900));
    if (mounted) Navigator.of(context).pop(true);
  }

  void _avisar(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mensaje)));
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
              children: [
                for (final w in _secciones()) ...[w, const SizedBox(height: AppSpacing.s3)],
              ],
            ),
          ),
          _footer(),
        ]),
      ),
    );
  }

  Widget _header() {
    return Container(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s3),
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(bottom: BorderSide(color: AppColors.line)),
      ),
      child: Column(children: [
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
            Text('Seguimiento diario · ${modulo.label}'.toUpperCase(), style: TextStyle(
              fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
              letterSpacing: 0.8, color: _acento,
            )),
            Text(widget.lote.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 17, fontWeight: FontWeight.w800,
              letterSpacing: -0.4, color: AppColors.ink900,
            )),
          ])),
          AppBadge(label: 'Día ${widget.lote.dia}', tone: switch (modulo) {
            ModuloSeguimiento.levante => BadgeTone.success,
            ModuloSeguimiento.engorde => BadgeTone.orange,
            ModuloSeguimiento.produccion => BadgeTone.info,
            ModuloSeguimiento.reproductora => BadgeTone.neutral,
          }),
        ]),
        const SizedBox(height: AppSpacing.s2),
        Row(children: [
          _meta('Aves', _fmt(widget.lote.aves)),
          const SizedBox(width: AppSpacing.s4),
          _meta('Granja', widget.lote.granja),
          const SizedBox(width: AppSpacing.s4),
          _meta('Galpón', widget.lote.galpon),
        ]),
      ]),
    );
  }

  Widget _meta(String l, String v) => Row(children: [
    Text('$l ', style: const TextStyle(fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500)),
    Text(v, style: const TextStyle(
      fontFamily: 'PlusJakartaSans', fontSize: 11, fontWeight: FontWeight.w700,
      color: AppColors.ink900, fontFeatures: [FontFeature.tabularFigures()],
    )),
  ]);

  Widget _footer() {
    return Container(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s3),
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.line)),
      ),
      child: Row(children: [
        if (_guardado) const AppSavedChip()
        else AppButton(
          label: 'Cancelar', variant: AppButtonVariant.ghost,
          onPressed: () => Navigator.of(context).pop(),
        ),
        const SizedBox(width: AppSpacing.s2),
        Expanded(child: AppButton(
          label: _guardado ? 'Guardado' : 'Guardar registro',
          icon: Icons.check_rounded,
          full: true,
          variant: modulo == ModuloSeguimiento.engorde ? AppButtonVariant.accent : AppButtonVariant.primary,
          onPressed: _guardado ? null : _guardar,
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
      if (d != null) setState(() => _fecha = d);
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
    filled: lleno(['mortalidadHembras', 'mortalidadMachos']),
    trailing: const AppBadge(label: 'Obligatorio', tone: BadgeTone.danger),
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

  Widget _pesoSec({String suffix = 'g', bool obligatorio = false, String? nota}) => AppSection(
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
    filled: _usaSelectorItems
        ? (_itemsH.any((l) => l.valida) || _itemsM.any((l) => l.valida))
        : lleno(['consumoKgHembras', 'consumoKgMachos']),
    trailing: const AppBadge(label: 'Obligatorio', tone: BadgeTone.danger),
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
      filled: lleno(['mortalidadHembras', 'selH']),
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
      filled: lleno(['mortalidadMachos', 'selM']),
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
        AppPairField(label: 'Peso aves', suffix: 'g',
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
      filled: lleno(['mortalidadHembras', 'selH']),
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
      filled: lleno(['mortalidadMachos', 'selM']),
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

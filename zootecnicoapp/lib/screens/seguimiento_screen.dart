/// Formularios de seguimiento diario — el corazón de la app.
/// Los campos salen de los modales del web:
///   levante      → features/lote-levante/pages/modal-create-edit
///   engorde      → features/aves-engorde/pages/modal-seguimiento-engorde
///   produccion   → features/lote-produccion/pages/modal-seguimiento-diario
///   reproductora → features/seguimiento-diario-lote-reproductora/pages/modal-seguimiento-reproductora
library;

import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import '../widgets/app_widgets.dart';
import '../core/alimento_obligatorio.dart';
import '../core/api/seguimientos_api.dart';
import '../core/local_db.dart';
import '../core/models.dart';
import '../core/sync_service.dart';

class SeguimientoScreen extends StatefulWidget {
  const SeguimientoScreen({
    super.key,
    required this.lote,
    required this.usuario,
    required this.sync,
  });

  final Lote lote;
  final Usuario usuario;
  final SyncService sync;

  @override
  State<SeguimientoScreen> createState() => _SeguimientoScreenState();
}

class _SeguimientoScreenState extends State<SeguimientoScreen> {
  final Map<String, TextEditingController> _c = {};
  final Map<String, bool> _abierto = {'general': true};
  final List<ItemSeguimiento> _itemsH = [];
  final List<ItemSeguimiento> _itemsM = [];
  final List<ItemSeguimiento> _itemsG = [];
  DateTime _fecha = DateTime.now();
  bool _guardado = false;

  TextEditingController ctl(String k) => _c.putIfAbsent(k, () => TextEditingController());
  bool abierto(String k) => _abierto[k] ?? false;
  void toggle(String k) => setState(() => _abierto[k] = !abierto(k));
  bool lleno(List<String> keys) => keys.any((k) => (_c[k]?.text ?? '').isNotEmpty);

  ModuloSeguimiento get modulo => widget.lote.modulo;

  @override
  void dispose() {
    for (final c in _c.values) { c.dispose(); }
    super.dispose();
  }

  /// El cuerpo que se le manda al backend, armado por [PayloadSeguimiento]
  /// según el módulo y el país. Devuelve null para los módulos que esta versión
  /// todavía no sabe enviar (levante y producción): la UI existe, el mapeo no.
  Map<String, dynamic>? _payload() {
    final campos = {for (final e in _c.entries) e.key: e.value.text};
    final u = widget.usuario;

    return switch (modulo) {
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
      _ => null,
    };
  }

  Future<void> _guardar() async {
    final payload = _payload();
    if (payload == null) {
      _avisar('Esta versión todavía no envía ${modulo.label}. Registralo desde la web.');
      return;
    }

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

    // Optimista: confirmamos primero, sincronizamos después.
    setState(() => _guardado = true);
    await widget.sync.encolar(
      tipo: modulo.id,
      loteId: widget.lote.id,
      loteNombre: widget.lote.nombre,
      fecha: _fecha,
      payload: payload,
      endpoint: endpointDeModulo[modulo],
    );
    if (!mounted) return;
    await Future.delayed(const Duration(milliseconds: 900));
    if (mounted) Navigator.of(context).pop(true);
  }

  void _avisar(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mensaje)));
  }

  Color get _acento => switch (modulo) {
    ModuloSeguimiento.levante      => AppColors.green600,
    ModuloSeguimiento.engorde      => AppColors.orange600,
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
  /// Va como consumo directo en kg (`consumoKgHembras`/`consumoKgMachos`), que es
  /// lo que el backend acepta sin catálogo de inventario. El editor de ítems con
  /// descuento de stock necesita el catálogo de `item_inventario_ecuador` con
  /// existencias por galpón — queda para la fase siguiente; ponerlo ahora sería
  /// un formulario que se llena y no descuenta nada.
  Widget _alimentoSec({Color? acento}) => AppSection(
    title: 'Alimento',
    icon: Icons.grass_rounded,
    expanded: abierto('alimento'),
    onToggle: () => toggle('alimento'),
    filled: lleno(['consumoKgHembras', 'consumoKgMachos']),
    trailing: const AppBadge(label: 'Obligatorio', tone: BadgeTone.danger),
    children: [
      AppField(label: 'Tipo de alimento', required: true, controller: ctl('tipoAlimento'),
        placeholder: 'Ej: Iniciación, Engorde 1…'),
      AppPairField(label: 'Consumo', suffix: 'kg', required: true,
        hController: ctl('consumoKgHembras'), mController: ctl('consumoKgMachos')),
    ],
  );

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

  Widget _itemsSec(String key, String title, List<ItemSeguimiento> items, {Color? acento, String? nota}) => AppSection(
    title: items.isEmpty ? title : '$title (${items.length})',
    icon: Icons.inventory_2_outlined,
    expanded: abierto(key),
    onToggle: () => toggle(key),
    filled: items.isNotEmpty,
    children: [
      if (nota != null) AppInfoBox(text: nota),
      _ItemsEditor(items: items, acento: acento ?? AppColors.ink700, onChanged: () => setState(() {})),
    ],
  );

  // ── LEVANTE ────────────────────────────────────────────────────────────────
  List<Widget> _levante() => [
    _general(),
    _itemsSec('itemsH', 'Ítems Hembras ♀', _itemsH, acento: AppColors.hembra),
    _itemsSec('itemsM', 'Ítems Machos ♂', _itemsM, acento: AppColors.macho),
    _itemsSec('itemsG', 'Ítems generales del lote', _itemsG,
      nota: 'Medicamentos, biológicos y accesorios no asignados a un sexo.'),
    _mortalidadSec(),
    _pesoSec(nota: 'Solo registrar en el día de pesaje semanal.'),
    ..._aguaSec(),
  ];

  // ── POLLO ENGORDE ──────────────────────────────────────────────────────────
  List<Widget> _engorde() => [
    _general(),
    _alimentoSec(acento: AppColors.orange600),
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

  int get _totalHuevos => [..._huevosIncubables, ..._huevosNoIncubables]
      .map((h) => int.tryParse(_c[h.$1]?.text ?? '') ?? 0).fold(0, (a, b) => a + b);
  int get _incubables => _huevosIncubables
      .map((h) => int.tryParse(_c[h.$1]?.text ?? '') ?? 0).fold(0, (a, b) => a + b);

  List<Widget> _produccion() => [
    AppSection(
      title: 'General',
      icon: Icons.calendar_today_rounded,
      expanded: abierto('general'), onToggle: () => toggle('general'), filled: true,
      children: [
        _fechaField(),
        _EtapaSelector(controller: ctl('etapa')),
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
        _ItemsEditor(items: _itemsH, acento: AppColors.hembra, onChanged: () => setState(() {})),
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
        _ItemsEditor(items: _itemsM, acento: AppColors.macho, onChanged: () => setState(() {})),
      ],
    ),
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

class _ItemsEditor extends StatelessWidget {
  const _ItemsEditor({required this.items, required this.acento, required this.onChanged});

  final List<ItemSeguimiento> items;
  final Color acento;
  final VoidCallback onChanged;

  static const _tipos = ['alimento', 'medicamento', 'suplemento', 'biológico', 'otro'];

  @override
  Widget build(BuildContext context) {
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
        Text('Ítems', style: TextStyle(
          fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: acento,
        )),
        TextButton(
          onPressed: () { items.add(ItemSeguimiento()); onChanged(); },
          style: TextButton.styleFrom(
            foregroundColor: acento, padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            minimumSize: Size.zero, tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            side: BorderSide(color: acento.withValues(alpha: 0.4)),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.xs)),
          ),
          child: const Text('+ Agregar', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700)),
        ),
      ]),
      const SizedBox(height: AppSpacing.s2),
      if (items.isEmpty)
        Container(
          padding: const EdgeInsets.symmetric(vertical: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.sm),
            border: Border.all(color: AppColors.line),
          ),
          alignment: Alignment.center,
          child: const Text('Sin ítems registrados', style: TextStyle(
            fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
          )),
        ),
      for (int i = 0; i < items.length; i++) ...[
        if (i > 0) const SizedBox(height: AppSpacing.s2),
        _ItemRow(item: items[i], index: i, tipos: _tipos,
          onRemove: () { items.removeAt(i); onChanged(); }, onChanged: onChanged),
      ],
    ]);
  }
}

class _ItemRow extends StatelessWidget {
  const _ItemRow({required this.item, required this.index, required this.tipos,
    required this.onRemove, required this.onChanged});

  final ItemSeguimiento item;
  final int index;
  final List<String> tipos;
  final VoidCallback onRemove;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.s3),
      decoration: BoxDecoration(color: AppColors.cream, borderRadius: BorderRadius.circular(AppRadius.sm)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          Text('Ítem ${index + 1}', style: const TextStyle(
            fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w700, color: AppColors.ink700,
          )),
          GestureDetector(
            onTap: onRemove,
            child: const Text('Eliminar', style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600, color: AppColors.danger,
            )),
          ),
        ]),
        const SizedBox(height: AppSpacing.s2),
        DropdownButtonFormField<String>(
          initialValue: item.tipo.isEmpty ? null : item.tipo,
          decoration: const InputDecoration(labelText: 'Tipo', isDense: true),
          items: [for (final t in tipos) DropdownMenuItem(value: t, child: Text('${t[0].toUpperCase()}${t.substring(1)}'))],
          onChanged: (v) { item.tipo = v ?? ''; onChanged(); },
        ),
        const SizedBox(height: AppSpacing.s2),
        Row(children: [
        Expanded(child: AppField(label: 'Cantidad',
            onChanged: (v) => item.cantidad = v,
            keyboardType: const TextInputType.numberWithOptions(decimal: true), placeholder: '0')),
          const SizedBox(width: AppSpacing.s2),
          SizedBox(width: 92, child: DropdownButtonFormField<String>(
            initialValue: item.unidad,
            decoration: const InputDecoration(labelText: 'Unidad', isDense: true),
            items: const [
              DropdownMenuItem(value: 'kg', child: Text('kg')),
              DropdownMenuItem(value: 'g', child: Text('g')),
              DropdownMenuItem(value: 'L', child: Text('L')),
              DropdownMenuItem(value: 'unidades', child: Text('uds')),
              DropdownMenuItem(value: 'dosis', child: Text('dosis')),
            ],
            onChanged: (v) { item.unidad = v ?? 'kg'; onChanged(); },
          )),
        ]),
      ]),
    );
  }
}

class _EtapaSelector extends StatelessWidget {
  const _EtapaSelector({required this.controller});

  final TextEditingController controller;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String>(
      initialValue: controller.text.isEmpty ? '1' : controller.text,
      decoration: const InputDecoration(labelText: 'Etapa de producción'),
      items: const [
        DropdownMenuItem(value: '1', child: Text('Etapa 1 (semana 25–33)')),
        DropdownMenuItem(value: '2', child: Text('Etapa 2 (semana 34–50)')),
        DropdownMenuItem(value: '3', child: Text('Etapa 3 (semana >50)')),
      ],
      onChanged: (v) => controller.text = v ?? '1',
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

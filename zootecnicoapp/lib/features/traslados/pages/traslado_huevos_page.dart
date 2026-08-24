/// Traslado de huevos: del galpón a la planta.
///
/// El operario acaba de contar los huevos en el seguimiento del día; acá mueve
/// lo que sale para la planta, con **las mismas 11 categorías** que ya tipeó.
///
/// Offline-first como el resto: se encola y sube cuando hay señal. La
/// disponibilidad se consulta al abrir —si hay red— para que no cargue más de lo
/// que existe y se entere recién al sincronizar.
///
/// El contrato con el backend (por qué `tipoDestino: 'Planta'`, por qué no hay
/// traslado entre granjas) está en `core/api/traslados_api.dart`.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/api/traslados_api.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/sync/widgets/sync_widgets.dart';
import 'package:zootecnicoapp/shared/formato.dart';

class TrasladoHuevosPage extends StatefulWidget {
  const TrasladoHuevosPage({
    super.key,
    required this.lote,
    required this.sync,
    required this.api,
  });

  /// Lote de producción de origen. Su `id` es el `lotePosturaProduccionId`.
  final Lote lote;
  final SyncService sync;
  final TrasladosApi api;

  @override
  State<TrasladoHuevosPage> createState() => _TrasladoHuevosPageState();
}

class _TrasladoHuevosPageState extends State<TrasladoHuevosPage> {
  final Map<String, TextEditingController> _c = {};
  final TextEditingController _observaciones = TextEditingController();

  DateTime _fecha = DateTime.now();
  DisponibilidadHuevos? _disponible;
  bool _consultando = true;
  bool _guardado = false;

  TextEditingController _ctl(String k) =>
      _c.putIfAbsent(k, () => TextEditingController());

  int _cargado(String clave) => int.tryParse(_c[clave]?.text.trim() ?? '') ?? 0;

  Map<String, int> get _cantidades => {
        for (final c in categoriasHuevo)
          if (_cargado(c.clave) > 0) c.clave: _cargado(c.clave),
      };

  int get _total => _cantidades.values.fold(0, (a, b) => a + b);

  /// Categorías donde se cargó más de lo que hay. Sólo se puede afirmar con una
  /// consulta de disponibilidad hecha: sin red no se bloquea nada.
  List<String> get _sobregiro {
    final d = _disponible;
    if (d == null) return const [];
    return [
      for (final c in categoriasHuevo)
        if (_cargado(c.clave) > d.de(c.clave)) c.etiqueta,
    ];
  }

  @override
  void initState() {
    super.initState();
    _consultarDisponibilidad();
  }

  @override
  void dispose() {
    for (final c in _c.values) {
      c.dispose();
    }
    _observaciones.dispose();
    super.dispose();
  }

  /// Sin red no es un error: se captura igual y el backend valida al subir.
  Future<void> _consultarDisponibilidad() async {
    if (!widget.sync.enLinea) {
      if (mounted) setState(() => _consultando = false);
      return;
    }
    try {
      final d = await widget.api.disponibilidad(widget.lote.id);
      if (!mounted) return;
      setState(() {
        _disponible = d;
        _consultando = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _consultando = false);
    }
  }

  Future<void> _guardar() async {
    if (_total <= 0) {
      _avisar('Cargá al menos un huevo para trasladar.');
      return;
    }
    if (_sobregiro.isNotEmpty) {
      _avisar('No hay tantos huevos disponibles en: ${_sobregiro.join(', ')}.');
      return;
    }

    // Encolar primero, confirmar después: si el INSERT falla, el operario no
    // puede irse creyendo que lo movió (invariante I18).
    try {
      await widget.sync.encolar(
        tipo: 'movimiento-huevos',
        loteId: widget.lote.id,
        loteNombre: widget.lote.nombre,
        fecha: _fecha,
        payload: TrasladosApi.payload(
          lotePosturaProduccionId: widget.lote.id,
          fecha: _fecha,
          cantidades: _cantidades,
          observaciones: _observaciones.text,
        ),
        endpoint: endpointTrasladoHuevos,
        // Un traslado es N por día: marcar el día descartaría el segundo como
        // duplicado y marcaría el seguimiento del lote sin estarlo.
        marcaElDia: false,
      );
    } catch (_) {
      if (!mounted) return;
      _avisar('No se pudo guardar en el equipo. Revisá el espacio e intentá de nuevo.');
      return;
    }

    if (!mounted) return;
    setState(() => _guardado = true);
    await Future<void>.delayed(const Duration(milliseconds: 900));
    if (mounted) Navigator.of(context).pop(true);
  }

  void _avisar(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mensaje)));
  }

  @override
  Widget build(BuildContext context) {
    final l = widget.lote;

    return Scaffold(
      backgroundColor: AppColors.cream,
      appBar: AppBar(
        title: const Text('Traslado de huevos'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: AppSpacing.s3),
            child: Center(child: ConnectionChip(sync: widget.sync)),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
            AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, AppSpacing.s10),
        children: [
          _Encabezado(lote: l, fecha: _fecha, onFecha: _elegirFecha),
          const SizedBox(height: AppSpacing.s4),

          if (_consultando)
            const AppInfoBox(text: 'Consultando qué hay disponible…')
          else if (_disponible == null)
            const AppInfoBox(
              tone: InfoTone.info,
              text: 'Sin conexión: no se pudo consultar el disponible. '
                  'Podés cargar igual — se valida al subir.',
            ),
          if (!_consultando) const SizedBox(height: AppSpacing.s3),

          AppSection(
            title: 'Huevos a trasladar',
            icon: Icons.egg_outlined,
            expanded: true,
            filled: _total > 0,
            children: [
              for (final c in categoriasHuevo)
                Padding(
                  padding: const EdgeInsets.only(bottom: AppSpacing.s2),
                  child: AppField(
                    label: c.etiqueta,
                    controller: _ctl(c.clave),
                    keyboardType: TextInputType.number,
                    hint: _disponible == null
                        ? null
                        : 'hay ${fmtMiles(_disponible!.de(c.clave))}',
                    onChanged: (_) => setState(() {}),
                  ),
                ),
              AppField(
                label: 'Observaciones',
                controller: _observaciones,
                placeholder: 'Novedades del traslado…',
                maxLines: 2,
              ),
            ],
          ),

          const SizedBox(height: AppSpacing.s4),
          _Total(total: _total, sobregiro: _sobregiro),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: AnimatedSwitcher(
            duration: AppMotion.duracion(context, AppMotion.fast),
            child: _guardado
                ? const Center(key: ValueKey('ok'), child: AppSavedChip())
                : AppButton(
                    key: const ValueKey('guardar'),
                    label: 'Registrar traslado',
                    icon: Icons.check_rounded,
                    size: AppButtonSize.lg,
                    full: true,
                    onPressed: _total > 0 ? _guardar : null,
                  ),
          ),
        ),
      ),
    );
  }

  Future<void> _elegirFecha() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _fecha,
      firstDate: DateTime.now().subtract(const Duration(days: 30)),
      lastDate: DateTime.now(),
    );
    if (d != null) setState(() => _fecha = d);
  }
}

class _Encabezado extends StatelessWidget {
  const _Encabezado({required this.lote, required this.fecha, required this.onFecha});

  final Lote lote;
  final DateTime fecha;
  final VoidCallback onFecha;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.s4),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.shadowSm,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(lote.nombre,
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans',
                fontSize: AppFontSize.md,
                fontWeight: FontWeight.w800,
                color: AppColors.ink900,
              )),
          const SizedBox(height: 2),
          Text('${lote.granja} · ${lote.galpon}',
              style: const TextStyle(
                fontFamily: 'Inter',
                fontSize: AppFontSize.xs,
                color: AppColors.ink500,
              )),
          const SizedBox(height: AppSpacing.s3),
          // Destino fijo: es el único que el reporte contable cuenta, y el único
          // que el backend acredita de verdad. No se ofrece elegirlo.
          Row(children: [
            const Icon(Icons.factory_outlined,
                size: AppFontSize.md, color: AppColors.ink300),
            const SizedBox(width: AppSpacing.s2),
            const Text('Destino: Planta',
                style: TextStyle(
                  fontFamily: 'Inter',
                  fontSize: AppFontSize.sm,
                  color: AppColors.ink700,
                )),
            const Spacer(),
            PresionHundida(
              onTap: onFecha,
              child: Container(
                padding: const EdgeInsets.symmetric(
                    horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
                decoration: BoxDecoration(
                  color: AppColors.brand50,
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: Row(mainAxisSize: MainAxisSize.min, children: [
                  const Icon(Icons.calendar_today_rounded,
                      size: AppFontSize.sm, color: AppColors.brand600),
                  const SizedBox(width: AppSpacing.s2),
                  Text(
                    '${fecha.day.toString().padLeft(2, '0')}/'
                    '${fecha.month.toString().padLeft(2, '0')}',
                    style: const TextStyle(
                      fontFamily: 'Inter',
                      fontSize: AppFontSize.sm,
                      fontWeight: FontWeight.w700,
                      color: AppColors.brand700,
                    ),
                  ),
                ]),
              ),
            ),
          ]),
        ],
      ),
    );
  }
}

class _Total extends StatelessWidget {
  const _Total({required this.total, required this.sobregiro});

  final int total;
  final List<String> sobregiro;

  @override
  Widget build(BuildContext context) {
    if (sobregiro.isNotEmpty) {
      return AppInfoBox(
        tone: InfoTone.warn,
        text: 'Cargaste más de lo disponible en: ${sobregiro.join(', ')}. '
            'El servidor lo va a rechazar.',
      );
    }
    return Container(
      padding: const EdgeInsets.all(AppSpacing.s4),
      decoration: BoxDecoration(
        color: AppColors.brand50,
        borderRadius: BorderRadius.circular(AppRadius.lg),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          const Text('Total a trasladar',
              style: TextStyle(
                fontFamily: 'Inter',
                fontSize: AppFontSize.sm,
                fontWeight: FontWeight.w600,
                color: AppColors.ink700,
              )),
          Text(fmtMiles(total),
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans',
                fontSize: AppFontSize.lg,
                fontWeight: FontWeight.w800,
                color: AppColors.brand700,
              )),
        ],
      ),
    );
  }
}

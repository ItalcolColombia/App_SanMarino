/// Hoja modal para elegir módulo y lote antes de abrir un seguimiento.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/shared/formato.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';


Future<Lote?> mostrarSelectorLote({
  required BuildContext context,
  required Usuario usuario,
  required List<Lote> lotes,
  ModuloSeguimiento? moduloPreseleccionado,
}) {
  return showModalBottomSheet<Lote>(
    context: context,
    backgroundColor: AppColors.cream,
    isScrollControlled: true,
    builder: (_) => _SelectorSheet(
      usuario: usuario, lotes: lotes, moduloInicial: moduloPreseleccionado,
    ),
  );
}

class _SelectorSheet extends StatefulWidget {
  const _SelectorSheet({required this.usuario, required this.lotes, this.moduloInicial});

  final Usuario usuario;
  final List<Lote> lotes;
  final ModuloSeguimiento? moduloInicial;

  @override
  State<_SelectorSheet> createState() => _SelectorSheetState();
}

class _SelectorSheetState extends State<_SelectorSheet> {
  ModuloSeguimiento? _modulo;

  @override
  void initState() { super.initState(); _modulo = widget.moduloInicial; }

  @override
  Widget build(BuildContext context) {
    final lotesModulo = _modulo == null
      ? const <Lote>[]
      : widget.lotes.where((l) => l.modulo == _modulo).toList();

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.s5, AppSpacing.s3, AppSpacing.s5, AppSpacing.s5),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Center(child: Container(
            width: AppSpacing.s8, height: AppSpacing.s1,
            decoration: BoxDecoration(color: AppColors.ink200, borderRadius: BorderRadius.circular(AppRadius.pill)),
          )),
          const SizedBox(height: AppSpacing.s4),
          Row(children: [
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(_modulo == null ? 'Nuevo seguimiento' : 'Selecciona un lote', style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 18, fontWeight: FontWeight.w800,
                letterSpacing: -0.4, color: AppColors.ink900,
              )),
              Text(_modulo?.label ?? 'Elige el módulo', style: const TextStyle(
                fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
              )),
            ])),
            IconButton(
              onPressed: () => Navigator.of(context).pop(),
              icon: const Icon(Icons.close_rounded, size: 20),
              style: IconButton.styleFrom(
                backgroundColor: AppColors.cream2, foregroundColor: AppColors.ink700,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.sm)),
              ),
            ),
          ]),
          const SizedBox(height: AppSpacing.s4),

          // Flexible + scroll: con la hoja en `isScrollControlled` y un usuario
          // con muchos lotes, la Column se pasaba del alto de la pantalla y
          // reventaba por overflow. Con pocas opciones sigue ajustándose al
          // contenido, así que la hoja no crece de más.
          Flexible(
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: _modulo == null ? _opcionesModulo() : _opcionesLote(lotesModulo),
              ),
            ),
          ),
        ]),
      ),
    );
  }

  /// Paso 1: los módulos del usuario.
  List<Widget> _opcionesModulo() {
    final modulos = widget.usuario.modulos;
    final items = <Widget>[];

    for (var i = 0; i < modulos.length; i++) {
      final m = modulos[i];
      if (i > 0) items.add(const SizedBox(height: AppSpacing.s2));
      items.add(EntradaEscalonada(
        // La clave incluye el paso: sin ella las dos listas comparten posición
        // y la de lotes entraría sin animar, de golpe.
        key: ValueKey('modulo-${m.id}'),
        indice: i,
        child: _opcion(
          emoji: m.emoji,
          titulo: m.label,
          sub: '${widget.lotes.where((l) => l.modulo == m).length} lotes asignados',
          color: switch (m) {
            ModuloSeguimiento.levante      => AppColors.levante,
            ModuloSeguimiento.engorde      => AppColors.engorde,
            ModuloSeguimiento.produccion   => AppColors.produccion,
            ModuloSeguimiento.reproductora => AppColors.reproductora,
          },
          onTap: () {
            final ls = widget.lotes.where((l) => l.modulo == m).toList();
            if (ls.length == 1) { Navigator.of(context).pop(ls.first); }
            else { setState(() => _modulo = m); }
          },
        ),
      ));
    }
    return items;
  }

  /// Paso 2: los lotes del módulo elegido.
  List<Widget> _opcionesLote(List<Lote> lotes) {
    final items = <Widget>[_volver(), const SizedBox(height: AppSpacing.s2)];

    if (lotes.isEmpty) {
      items.add(_sinLotes());
      return items;
    }

    for (var i = 0; i < lotes.length; i++) {
      if (i > 0) items.add(const SizedBox(height: AppSpacing.s2));
      items.add(EntradaEscalonada(
        key: ValueKey('lote-${lotes[i].id}'),
        indice: i,
        child: _opcionLote(lotes[i]),
      ));
    }
    return items;
  }

  Widget _volver() => Align(
    alignment: Alignment.centerLeft,
    child: TextButton.icon(
      onPressed: () => setState(() => _modulo = null),
      icon: const Icon(Icons.arrow_back_rounded, size: 16),
      label: const Text('Cambiar módulo'),
      style: TextButton.styleFrom(
        // Volver es una acción de navegación → naranja, como todo lo accionable.
        foregroundColor: AppColors.brand500,
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s2, vertical: AppSpacing.s2),
        minimumSize: Size.zero,
        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
        textStyle: const TextStyle(
          fontFamily: 'Inter', fontSize: AppFontSize.sm, fontWeight: FontWeight.w600,
        ),
      ),
    ),
  );

  /// Vacío del módulo: informativo, no un error — el usuario no hizo nada mal.
  Widget _sinLotes() => Padding(
    padding: const EdgeInsets.symmetric(vertical: AppSpacing.s6),
    child: Column(children: [
      Container(
        width: AppSpacing.s9, height: AppSpacing.s9,
        decoration: const BoxDecoration(color: AppColors.cream2, shape: BoxShape.circle),
        child: const Icon(Icons.inbox_rounded, color: AppColors.ink300),
      ),
      const SizedBox(height: AppSpacing.s3),
      const Text('No tienes lotes asignados para este módulo.',
        textAlign: TextAlign.center,
        style: TextStyle(fontFamily: 'Inter', fontSize: AppFontSize.sm, color: AppColors.ink500)),
    ]),
  );

  Widget _opcion({required String emoji, required String titulo, required String sub,
    required Color color, required VoidCallback onTap}) {
    // Ver nota en _LoteCard: borderRadius + Border de colores no uniformes no
    // lo soporta BoxDecoration — el clip del radius baja a un ClipRRect.
    // `PresionHundida` reemplaza al InkWell: da la realimentación táctil que se
    // siente con guantes, donde el ripple pasa desapercibido.
    return PresionHundida(
      onTap: onTap,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s4),
          decoration: BoxDecoration(
            color: AppColors.surface,
            border: Border(
              left: BorderSide(color: color, width: 4),
              top: BorderSide(color: AppColors.line),
              right: BorderSide(color: AppColors.line),
              bottom: BorderSide(color: AppColors.line),
            ),
          ),
          child: Row(children: [
            Text(emoji, style: const TextStyle(fontSize: AppFontSize.xl)),
            const SizedBox(width: AppSpacing.s4),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(titulo, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base, fontWeight: FontWeight.w700, color: AppColors.ink900,
              )),
              const SizedBox(height: AppSpacing.s1),
              Text(sub, style: const TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),
            ])),
            const Icon(Icons.chevron_right_rounded, size: 20, color: AppColors.ink200),
          ]),
        ),
      ),
    );
  }

  Widget _opcionLote(Lote l) {
    return PresionHundida(
      onTap: () => Navigator.of(context).pop(l),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s4),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: Border.all(color: AppColors.line),
        ),
        child: Row(children: [
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(l.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base, fontWeight: FontWeight.w700, color: AppColors.ink900,
            )),
            Text('${l.granja} · ${l.galpon} · Día ${l.dia}', style: const TextStyle(
              fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
            )),
          ])),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(fmtMiles(l.aves), style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 14, fontWeight: FontWeight.w700,
              color: AppColors.ink900, fontFeatures: [FontFeature.tabularFigures()],
            )),
            const Text('aves', style: TextStyle(
              fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500,
            )),
          ]),
          const SizedBox(width: AppSpacing.s2),
          const Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.ink200),
        ]),
      ),
    );
  }
}

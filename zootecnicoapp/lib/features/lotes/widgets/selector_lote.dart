/// Hoja modal para elegir módulo y lote antes de abrir un seguimiento.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/shared/formato.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
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
            width: 38, height: 4,
            decoration: BoxDecoration(color: AppColors.ink200, borderRadius: BorderRadius.circular(2)),
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

          if (_modulo == null)
            for (final m in widget.usuario.modulos) ...[
              _opcion(
                emoji: m.emoji, titulo: m.label,
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
              const SizedBox(height: AppSpacing.s2),
            ]
          else ...[
            TextButton(
              onPressed: () => setState(() => _modulo = null),
              style: TextButton.styleFrom(
                foregroundColor: AppColors.ink500, alignment: Alignment.centerLeft,
                padding: EdgeInsets.zero, minimumSize: Size.zero,
              ),
              child: const Text('← Cambiar módulo'),
            ),
            const SizedBox(height: AppSpacing.s2),
            if (lotesModulo.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: AppSpacing.s6),
                child: Text('No tienes lotes asignados para este módulo.',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontFamily: 'Inter', fontSize: 13, color: AppColors.ink500)),
              )
            else
              for (final l in lotesModulo) ...[
                _opcionLote(l),
                const SizedBox(height: AppSpacing.s2),
              ],
          ],
        ]),
      ),
    );
  }

  Widget _opcion({required String emoji, required String titulo, required String sub,
    required Color color, required VoidCallback onTap}) {
    // Ver nota en _LoteCard: borderRadius + Border de colores no uniformes no
    // lo soporta BoxDecoration — el clip del radius baja a un ClipRRect.
    return ClipRRect(
      borderRadius: BorderRadius.circular(AppRadius.lg),
      child: InkWell(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 15),
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
            Text(emoji, style: const TextStyle(fontSize: 26)),
            const SizedBox(width: AppSpacing.s4),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(titulo, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.ink900,
              )),
              const SizedBox(height: 2),
              Text(sub, style: const TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),
            ])),
            const Icon(Icons.chevron_right_rounded, size: 20, color: AppColors.ink200),
          ]),
        ),
      ),
    );
  }

  Widget _opcionLote(Lote l) {
    return InkWell(
      onTap: () => Navigator.of(context).pop(l),
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: Border.all(color: AppColors.line),
        ),
        child: Row(children: [
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(l.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.ink900,
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

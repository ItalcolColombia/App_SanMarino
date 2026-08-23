/// Lotes asignados al usuario: listado, búsqueda y filtro por módulo.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/shared/formato.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';


class LotesPage extends StatefulWidget {
  const LotesPage({
    super.key,
    required this.usuario,
    required this.lotes,
    required this.onRegistrar,
    this.filtroInicial,
  });

  final Usuario usuario;
  final List<Lote> lotes;
  final ValueChanged<Lote> onRegistrar;
  final ModuloSeguimiento? filtroInicial;

  @override
  State<LotesPage> createState() => _LotesScreenState();
}

class _LotesScreenState extends State<LotesPage> {
  ModuloSeguimiento? _filtro;
  String _query = '';

  @override
  void initState() { super.initState(); _filtro = widget.filtroInicial; }

  List<Lote> get _visibles => widget.lotes.where((l) {
    if (_filtro != null && l.modulo != _filtro) return false;
    if (_query.isNotEmpty) {
      final t = '${l.nombre} ${l.granja} ${l.galpon}'.toLowerCase();
      if (!t.contains(_query.toLowerCase())) return false;
    }
    return true;
  }).toList();

  @override
  Widget build(BuildContext context) {
    final v = _visibles;
    return Column(children: [
      Container(
        color: AppColors.surface,
        padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, AppSpacing.s3),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Mis lotes', style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 22, fontWeight: FontWeight.w800,
            letterSpacing: -0.5, color: AppColors.ink900,
          )),
          Text('${widget.lotes.length} asignados a tus granjas', style: const TextStyle(
            fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
          )),
          const SizedBox(height: AppSpacing.s3),
          TextField(
            onChanged: (t) => setState(() => _query = t),
            style: const TextStyle(fontFamily: 'Inter', fontSize: 14),
            decoration: InputDecoration(
              hintText: 'Buscar lote, granja, galpón…',
              prefixIcon: const Icon(Icons.search_rounded, size: 18, color: AppColors.ink300),
              fillColor: AppColors.cream,
              isDense: true,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(AppRadius.sm),
                borderSide: BorderSide(color: AppColors.line),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(AppRadius.sm),
                borderSide: BorderSide(color: AppColors.line),
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.s3),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(children: [
              _chip('Todos', _filtro == null, null),
              for (final m in widget.usuario.modulos) ...[
                const SizedBox(width: 6),
                _chip(m.label, _filtro == m, m),
              ],
            ]),
          ),
        ]),
      ),
      Expanded(
        child: v.isEmpty
          ? const Center(child: Text('Sin lotes encontrados', style: TextStyle(
              fontFamily: 'Inter', fontSize: 14, color: AppColors.ink500,
            )))
          : ListView.separated(
              padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 96),
              itemCount: v.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.s3),
              itemBuilder: (_, i) => _LoteCard(lote: v[i], onRegistrar: () => widget.onRegistrar(v[i])),
            ),
      ),
    ]);
  }

  Widget _chip(String label, bool activo, ModuloSeguimiento? m) {
    final color = m == null ? AppColors.ink900 : switch (m) {
      ModuloSeguimiento.levante      => AppColors.levante,
      ModuloSeguimiento.engorde      => AppColors.engorde,
      ModuloSeguimiento.produccion   => AppColors.produccion,
      ModuloSeguimiento.reproductora => AppColors.reproductora,
    };
    return GestureDetector(
      onTap: () => setState(() => _filtro = m),
      child: Container(
        height: 32,
        padding: const EdgeInsets.symmetric(horizontal: 12),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: activo ? color : Colors.transparent,
          borderRadius: BorderRadius.circular(AppRadius.pill),
          border: Border.all(color: activo ? color : AppColors.line),
        ),
        child: Text(label, style: TextStyle(
          fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600,
          color: activo ? Colors.white : AppColors.ink700,
        )),
      ),
    );
  }
}

class _LoteCard extends StatelessWidget {
  const _LoteCard({required this.lote, required this.onRegistrar});

  final Lote lote;
  final VoidCallback onRegistrar;

  @override
  Widget build(BuildContext context) {
    final (color, tone) = switch (lote.modulo) {
      ModuloSeguimiento.levante      => (AppColors.levante, BadgeTone.success),
      ModuloSeguimiento.engorde      => (AppColors.engorde, BadgeTone.orange),
      ModuloSeguimiento.produccion   => (AppColors.produccion, BadgeTone.info),
      ModuloSeguimiento.reproductora => (AppColors.reproductora, BadgeTone.neutral),
    };

    return Container(
      // borderRadius + un Border con colores distintos por lado no lo soporta
      // BoxDecoration (lanza "A borderRadius can only be given on borders with
      // uniform colors" en paint()) — el radius va acá para la sombra, y el
      // borde/relleno/clip del contenido bajan al ClipRRect+Container interno.
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        boxShadow: AppColors.shadowSm,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: InkWell(
          onTap: onRegistrar,
          child: Container(
            padding: const EdgeInsets.all(AppSpacing.s4),
            decoration: BoxDecoration(
              color: AppColors.surface,
              border: Border(
                left: BorderSide(color: color, width: 4),
                top: BorderSide(color: AppColors.line),
                right: BorderSide(color: AppColors.line),
                bottom: BorderSide(color: AppColors.line),
              ),
            ),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Row(children: [
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Text(lote.nombre, style: const TextStyle(
                  fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
                )),
                const SizedBox(width: AppSpacing.s2),
                AppBadge(label: lote.modulo.label, tone: tone),
              ]),
              const SizedBox(height: 3),
              Text('${lote.granja} · ${lote.galpon}', style: const TextStyle(
                fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
              )),
            ])),
            const Icon(Icons.chevron_right_rounded, size: 20, color: AppColors.ink200),
          ]),
          const SizedBox(height: AppSpacing.s3),
          Row(children: [
            Expanded(child: AppStatTile(label: 'Aves', value: fmtMiles(lote.aves))),
            const SizedBox(width: 6),
            Expanded(child: AppStatTile(label: 'Día', value: '${lote.dia}')),
            const SizedBox(width: 6),
            Expanded(child: AppStatTile(
              label: 'Viabilidad',
              value: lote.viabilidad != null ? '${lote.viabilidad!.toStringAsFixed(1).replaceAll('.', ',')}%' : '—',
              color: (lote.viabilidad ?? 0) >= 95 ? AppColors.green600 : const Color(0xFF9A7626),
            )),
          ]),
          const SizedBox(height: AppSpacing.s3),
          Align(
            alignment: Alignment.centerRight,
            child: AppButton(label: 'Registrar día', size: AppButtonSize.sm,
              icon: Icons.add_rounded, onPressed: onRegistrar),
          ),
        ]),
          ),
        ),
      ),
    );
  }
}


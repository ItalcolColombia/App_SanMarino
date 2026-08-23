/// Inicio: saludo, estado del día, módulos habilitados y lotes recientes.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/home/widgets/gallina.dart';
import 'package:zootecnicoapp/features/sync/widgets/sync_widgets.dart';


class HomePage extends StatelessWidget {
  const HomePage({
    super.key,
    required this.usuario,
    required this.lotes,
    required this.sync,
    required this.onNuevoSeguimiento,
    required this.onVerLotes,
    required this.onVerSync,
    required this.onPerfil,
  });

  final Usuario usuario;
  final List<Lote> lotes;
  final SyncService sync;
  final void Function(ModuloSeguimiento? modulo, Lote? lote) onNuevoSeguimiento;
  final VoidCallback onVerLotes;
  final VoidCallback onVerSync;
  final VoidCallback onPerfil;

  @override
  Widget build(BuildContext context) {
    return Stack(children: [
      ListView(
        padding: const EdgeInsets.only(bottom: 96),
        children: [
          _topBar(context),
          _bienvenida(context),
          if (usuario.modulos.isNotEmpty) _modulos(),
          if (lotes.isNotEmpty) _misLotes(),
          if (sync.pendientes > 0 || !sync.enLinea) _pendientes(),
        ],
      ),
      SyncRibbon(sync: sync),
    ]);
  }

  Widget _topBar(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s5, AppSpacing.s5, AppSpacing.s5, AppSpacing.s3),
      child: Row(children: [
        GestureDetector(
          onTap: onPerfil,
          child: Stack(clipBehavior: Clip.none, children: [
            Container(
              width: 44, height: 44,
              decoration: BoxDecoration(
                color: AppColors.green500, borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              alignment: Alignment.center,
              child: Text(usuario.iniciales, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w800, color: Colors.white,
              )),
            ),
            Positioned(top: -2, right: -2, child: AmbientDot(sync: sync)),
          ]),
        ),
        const SizedBox(width: AppSpacing.s3),
        Expanded(child: GestureDetector(
          onTap: onPerfil,
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Buen día,', style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
            )),
            Text(usuario.nombre, maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.ink900,
              )),
            Text('${usuario.cargo} · ${usuario.granja}', maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500)),
          ]),
        )),
        ConnectionChip(sync: sync, onTap: onVerSync),
      ]),
    );
  }

  Widget _bienvenida(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s5),
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.xl),
          border: Border.all(color: AppColors.line),
          boxShadow: AppColors.shadowSm,
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(children: [
          Container(
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft, end: Alignment.bottomRight,
                colors: [AppColors.green50, Color(0xFFE0EDE0)],
              ),
            ),
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s5, AppSpacing.s4, AppSpacing.s2),
            child: const GallinaAnimacion(),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(18, 14, 18, 18),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('¡Listo para registrar hoy!', style: TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 18, fontWeight: FontWeight.w800,
                letterSpacing: -0.4, color: AppColors.ink900,
              )),
              const SizedBox(height: 4),
              Text(_fechaLarga(DateTime.now()), style: const TextStyle(
                fontFamily: 'Inter', fontSize: 12, height: 1.5, color: AppColors.ink500,
              )),
            ]),
          ),
        ]),
      ),
    );
  }

  Widget _modulos() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s5),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          const Text('Seguimiento diario', style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
          )),
          TextButton(
            onPressed: () => onNuevoSeguimiento(null, null),
            style: TextButton.styleFrom(padding: EdgeInsets.zero, minimumSize: Size.zero),
            child: const Text('+ Nuevo'),
          ),
        ]),
        const SizedBox(height: AppSpacing.s2),
        Row(children: [
          for (int i = 0; i < usuario.modulos.length; i++) ...[
            if (i > 0) const SizedBox(width: AppSpacing.s2),
            Expanded(child: _ModuloCard(
              modulo: usuario.modulos[i],
              lotes: lotes.where((l) => l.modulo == usuario.modulos[i]).length,
              onTap: () => onNuevoSeguimiento(usuario.modulos[i], null),
            )),
          ],
        ]),
      ]),
    );
  }

  Widget _misLotes() {
    final visibles = lotes.take(3).toList();
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s5),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          const Text('Mis lotes', style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
          )),
          TextButton(
            onPressed: onVerLotes,
            style: TextButton.styleFrom(padding: EdgeInsets.zero, minimumSize: Size.zero),
            child: const Text('Ver todos'),
          ),
        ]),
        const SizedBox(height: AppSpacing.s2),
        Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border.all(color: AppColors.line),
          ),
          clipBehavior: Clip.antiAlias,
          child: Column(children: [
            for (int i = 0; i < visibles.length; i++) ...[
              if (i > 0) Divider(height: 1, color: AppColors.line),
              _LoteRow(lote: visibles[i], onTap: () => onNuevoSeguimiento(visibles[i].modulo, visibles[i])),
            ],
          ]),
        ),
      ]),
    );
  }

  Widget _pendientes() {
    final offline = !sync.enLinea;
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
      child: InkWell(
        onTap: onVerSync,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border(
              left: BorderSide(color: offline ? AppColors.ink700 : AppColors.brand500, width: 3),
              top: BorderSide(color: AppColors.line),
              right: BorderSide(color: AppColors.line),
              bottom: BorderSide(color: AppColors.line),
            ),
          ),
          child: Row(children: [
            Container(
              width: 32, height: 32,
              decoration: BoxDecoration(
                color: offline ? AppColors.ink100 : AppColors.brand50,
                borderRadius: BorderRadius.circular(AppRadius.sm),
              ),
              child: Icon(offline ? Icons.wifi_off_rounded : Icons.schedule_rounded,
                size: 16, color: offline ? AppColors.ink700 : AppColors.brand600),
            ),
            const SizedBox(width: AppSpacing.s3),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(
                sync.pendientes == 1
                  ? '1 registro guardado aquí'
                  : '${sync.pendientes} registros guardados aquí',
                style: const TextStyle(
                  fontFamily: 'PlusJakartaSans', fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.ink900,
                ),
              ),
              const SizedBox(height: 2),
              Text(offline ? 'Se enviarán cuando vuelva la red' : 'Toca para revisar la cola',
                style: const TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),
            ])),
            if (!offline)
              AppButton(label: 'Sincronizar', size: AppButtonSize.sm,
                variant: AppButtonVariant.accent, onPressed: sync.sincronizar),
          ]),
        ),
      ),
    );
  }
}

class _ModuloCard extends StatelessWidget {
  const _ModuloCard({required this.modulo, required this.lotes, required this.onTap});

  final ModuloSeguimiento modulo;
  final int lotes;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (modulo) {
      ModuloSeguimiento.levante      => (AppColors.green50, AppColors.green600),
      ModuloSeguimiento.engorde      => (AppColors.brand50, AppColors.brand600),
      ModuloSeguimiento.produccion   => (AppColors.infoBg, const Color(0xFF3F668A)),
      ModuloSeguimiento.reproductora => (const Color(0xFFF3EAF3), const Color(0xFF7A4D7A)),
    };

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(AppRadius.lg),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 14),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(modulo.emoji, style: const TextStyle(fontSize: 22)),
            const SizedBox(height: 6),
            Text(modulo.label, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w700,
              height: 1.2, color: AppColors.ink900,
            )),
            const SizedBox(height: 4),
            Text('$lotes lotes', style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600, color: fg,
            )),
          ]),
        ),
      ),
    );
  }
}

class _LoteRow extends StatelessWidget {
  const _LoteRow({required this.lote, required this.onTap});

  final Lote lote;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = switch (lote.modulo) {
      ModuloSeguimiento.levante      => AppColors.levante,
      ModuloSeguimiento.engorde      => AppColors.engorde,
      ModuloSeguimiento.produccion   => AppColors.produccion,
      ModuloSeguimiento.reproductora => AppColors.reproductora,
    };

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
        child: Row(children: [
          Container(width: 8, height: 8, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(lote.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.ink900,
            )),
            Text('${lote.granja} · Día ${lote.dia}', style: const TextStyle(
              fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
            )),
          ])),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(_fmtMiles(lote.aves), style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w700,
              color: AppColors.ink900, fontFeatures: [FontFeature.tabularFigures()],
            )),
            Text(lote.modulo.label, style: TextStyle(
              fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w600, color: color,
            )),
          ]),
          const SizedBox(width: AppSpacing.s2),
          const Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.ink200),
        ]),
      ),
    );
  }
}

String _fmtMiles(int n) => n.toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.');

String _fechaLarga(DateTime d) {
  const dias = ['lunes','martes','miércoles','jueves','viernes','sábado','domingo'];
  const meses = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
  final dia = dias[d.weekday - 1];
  return '${dia[0].toUpperCase()}${dia.substring(1)} ${d.day} de ${meses[d.month - 1]}, ${d.year}';
}

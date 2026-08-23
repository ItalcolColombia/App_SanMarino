/// Lotes asignados, cola de sincronización, perfil y selector de módulo.
library;

import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import '../widgets/app_widgets.dart';
import '../widgets/sync_widgets.dart';
import '../core/models.dart';
import '../core/sync_service.dart';
import '../core/local_db.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Lotes
// ═══════════════════════════════════════════════════════════════════════════

class LotesScreen extends StatefulWidget {
  const LotesScreen({
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
  State<LotesScreen> createState() => _LotesScreenState();
}

class _LotesScreenState extends State<LotesScreen> {
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
            Expanded(child: AppStatTile(label: 'Aves', value: _fmtMiles(lote.aves))),
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

// ═══════════════════════════════════════════════════════════════════════════
// Cola de sincronización
// ═══════════════════════════════════════════════════════════════════════════

class SyncScreen extends StatelessWidget {
  const SyncScreen({super.key, required this.sync});

  final SyncService sync;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      appBar: AppBar(title: const Text('Sincronización')),
      body: FutureBuilder<List<RegistroPendiente>>(
        future: LocalDb.instance.pendientes(),
        builder: (context, snap) {
          final cola = snap.data ?? const <RegistroPendiente>[];
          return ListView(
            padding: const EdgeInsets.all(AppSpacing.s4),
            children: [
              _resumen(context, cola),
              const SizedBox(height: AppSpacing.s5),
              if (cola.isEmpty)
                Column(children: [
                  Container(
                    width: 60, height: 60,
                    decoration: BoxDecoration(
                      color: AppColors.successBg, borderRadius: BorderRadius.circular(AppRadius.xl),
                    ),
                    child: const Icon(Icons.check_rounded, size: 28, color: AppColors.green600),
                  ),
                  const SizedBox(height: AppSpacing.s3),
                  const Text('Todo sincronizado', style: TextStyle(
                    fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
                  )),
                  const SizedBox(height: 4),
                  const Text('No hay registros pendientes de enviar.',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),
                ])
              else ...[
                const Text('EN COLA', style: TextStyle(
                  fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
                  letterSpacing: 0.8, color: AppColors.ink500,
                )),
                const SizedBox(height: AppSpacing.s2),
                for (final r in cola) ...[
                  _ColaItem(registro: r),
                  const SizedBox(height: AppSpacing.s2),
                ],
              ],
              const SizedBox(height: AppSpacing.s4),
              const Text('CONFIGURACIÓN', style: TextStyle(
                fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
                letterSpacing: 0.8, color: AppColors.ink500,
              )),
              const SizedBox(height: AppSpacing.s2),
              Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppRadius.lg),
                  border: Border.all(color: AppColors.line),
                ),
                child: SwitchListTile(
                  value: sync.autoSync,
                  onChanged: (v) => sync.autoSync = v,
                  title: const Text('Sincronización automática', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 13, color: AppColors.ink900,
                  )),
                  subtitle: const Text('Al recuperar conexión', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
                  )),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _resumen(BuildContext context, List<RegistroPendiente> cola) {
    final offline = !sync.enLinea;
    return Container(
      padding: const EdgeInsets.all(AppSpacing.s4),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft, end: Alignment.bottomRight,
          colors: offline
            ? [AppColors.ink700, AppColors.ink900]
            : [AppColors.orange500, AppColors.orange600],
        ),
        borderRadius: BorderRadius.circular(AppRadius.xl),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(offline ? 'Sin conexión · acumulados' : 'Pendientes de enviar', style: TextStyle(
          fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600,
          color: Colors.white.withValues(alpha: 0.9),
        )),
        const SizedBox(height: 4),
        Text('${cola.length}', style: const TextStyle(
          fontFamily: 'PlusJakartaSans', fontSize: 30, fontWeight: FontWeight.w800,
          letterSpacing: -0.6, height: 1, color: Colors.white,
          fontFeatures: [FontFeature.tabularFigures()],
        )),
        const SizedBox(height: 2),
        Text(cola.length == 1 ? 'registro' : 'registros', style: TextStyle(
          fontFamily: 'Inter', fontSize: 12, color: Colors.white.withValues(alpha: 0.85),
        )),
        const SizedBox(height: AppSpacing.s4),
        AppButton(
          label: offline ? 'Sin conexión' : 'Sincronizar ahora',
          icon: Icons.sync_rounded, full: true, size: AppButtonSize.md,
          onPressed: offline || cola.isEmpty ? null : sync.sincronizar,
        ),
      ]),
    );
  }
}

class _ColaItem extends StatelessWidget {
  const _ColaItem({required this.registro});

  final RegistroPendiente registro;

  @override
  Widget build(BuildContext context) {
    final estado = switch (registro.estado) {
      EstadoSync.pending => SyncDotState.pending,
      EstadoSync.syncing => SyncDotState.syncing,
      EstadoSync.synced  => SyncDotState.synced,
      // El servidor ya tenía ese día: el registro NO se perdió, está guardado.
      EstadoSync.duplicado => SyncDotState.synced,
      EstadoSync.error   => SyncDotState.pending,
    };
    final modulo = ModuloSeguimiento.fromId(registro.tipo);
    final titulo = modulo != null
      ? 'Seguimiento · ${registro.loteNombre} · ${modulo.label}'
      : '${_tituloMovimiento(registro.tipo)} · ${registro.loteNombre}';

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.line),
      ),
      child: Row(children: [
        SyncDot(state: estado),
        const SizedBox(width: AppSpacing.s3),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(titulo, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w600, color: AppColors.ink900,
          )),
          Text(_hace(registro.createdAt), style: const TextStyle(
            fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
          )),
        ])),
        if (registro.estado == EstadoSync.error)
          const AppBadge(label: 'Resolver', tone: BadgeTone.danger),
      ]),
    );
  }

  String _tituloMovimiento(String tipo) => switch (tipo) {
    'venta-aves' => 'Venta de aves',
    'traslado-aves' => 'Traslado de aves',
    'movimiento-huevos' => 'Movimiento de huevos',
    _ => 'Registro',
  };

  String _hace(DateTime d) {
    final min = DateTime.now().difference(d).inMinutes;
    if (min < 1) return 'Ahora';
    if (min < 60) return 'Hace $min min';
    final h = min ~/ 60;
    if (h < 24) return 'Hace $h h';
    return 'Hace ${h ~/ 24} d';
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Perfil
// ═══════════════════════════════════════════════════════════════════════════

class PerfilScreen extends StatelessWidget {
  const PerfilScreen({super.key, required this.usuario, required this.onLogout});

  final Usuario usuario;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.only(bottom: 96),
      children: [
        Container(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter, end: Alignment.bottomCenter,
              colors: [AppColors.green50, AppColors.cream],
            ),
          ),
          padding: const EdgeInsets.fromLTRB(AppSpacing.s5, AppSpacing.s7, AppSpacing.s5, AppSpacing.s5),
          child: Column(children: [
            Container(
              width: 80, height: 80,
              decoration: BoxDecoration(
                color: AppColors.green500,
                borderRadius: BorderRadius.circular(AppRadius.xl),
                boxShadow: AppColors.shadowMd,
              ),
              alignment: Alignment.center,
              child: Text(usuario.iniciales, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 26, fontWeight: FontWeight.w800, color: Colors.white,
              )),
            ),
            const SizedBox(height: AppSpacing.s4),
            Text(usuario.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 20, fontWeight: FontWeight.w800,
              letterSpacing: -0.4, color: AppColors.ink900,
            )),
            const SizedBox(height: 4),
            Text('${usuario.cargo} · ${usuario.granja}', style: const TextStyle(
              fontFamily: 'Inter', fontSize: 13, color: AppColors.ink500,
            )),
            const SizedBox(height: AppSpacing.s3),
            Row(mainAxisAlignment: MainAxisAlignment.center, children: [
              const AppBadge(label: 'Activo', tone: BadgeTone.success, dot: true),
              const SizedBox(width: 6),
              AppBadge(label: usuario.pais, tone: usuario.tieneControlAgua ? BadgeTone.info : BadgeTone.neutral),
            ]),
          ]),
        ),

        // Módulos habilitados
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, AppSpacing.s2),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('MÓDULOS ASIGNADOS', style: TextStyle(
              fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
              letterSpacing: 0.8, color: AppColors.ink500,
            )),
            const SizedBox(height: AppSpacing.s2),
            Wrap(spacing: 6, runSpacing: 6, children: [
              for (final m in usuario.modulos)
                AppBadge(label: '${m.emoji} ${m.label}', tone: BadgeTone.neutral),
            ]),
            if (usuario.tieneControlAgua) ...[
              const SizedBox(height: AppSpacing.s2),
              const AppBadge(label: 'Control de agua habilitado', tone: BadgeTone.info),
            ],
          ]),
        ),

        _seccion('CUENTA', [
          _fila(Icons.mail_outline_rounded, 'Correo', usuario.email),
          _fila(Icons.lock_outline_rounded, 'Cambiar contraseña', null, onTap: () {}),
          _fila(Icons.notifications_outlined, 'Notificaciones', 'Activadas', onTap: () {}),
        ]),

        _seccion('DATOS', [
          _fila(Icons.sync_rounded, 'Sincronizar ahora', null, onTap: () {}),
          _fila(Icons.download_outlined, 'Descargar guías genéticas', null, onTap: () {}),
        ]),

        _seccion('SESIÓN', [
          _fila(Icons.logout_rounded, 'Cerrar sesión', null, onTap: onLogout, danger: true),
        ]),

        Padding(
          padding: const EdgeInsets.all(AppSpacing.s6),
          child: Opacity(
            opacity: 0.5,
            child: Column(children: [
              Image.asset('assets/images/brand/logo-italfoods-zootecnico.png', height: 42),
              const SizedBox(height: 6),
              const Text('© 2026 Italfoods · v2.1.0', style: TextStyle(
                fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500,
              )),
            ]),
          ),
        ),
      ],
    );
  }

  Widget _seccion(String titulo, List<Widget> filas) => Padding(
    padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, 0),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(titulo, style: const TextStyle(
        fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
        letterSpacing: 0.8, color: AppColors.ink500,
      )),
      const SizedBox(height: AppSpacing.s2),
      Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.lg),
          border: Border.all(color: AppColors.line),
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(children: [
          for (int i = 0; i < filas.length; i++) ...[
            if (i > 0) Divider(height: 1, color: AppColors.line),
            filas[i],
          ],
        ]),
      ),
    ]),
  );

  Widget _fila(IconData icon, String label, String? value, {VoidCallback? onTap, bool danger = false}) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: 13),
        child: Row(children: [
          Container(
            width: 32, height: 32,
            decoration: BoxDecoration(
              color: danger ? AppColors.dangerBg : AppColors.cream2,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: Icon(icon, size: 15, color: danger ? const Color(0xFF9A4035) : AppColors.ink700),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Text(label, style: TextStyle(
            fontFamily: 'Inter', fontSize: 13, fontWeight: FontWeight.w500,
            color: danger ? const Color(0xFF9A4035) : AppColors.ink900,
          ))),
          if (value != null) Text(value, style: const TextStyle(
            fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
          )),
          if (onTap != null && !danger) ...[
            const SizedBox(width: AppSpacing.s2),
            const Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.ink200),
          ],
        ]),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Selector de módulo y lote (bottom sheet)
// ═══════════════════════════════════════════════════════════════════════════

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
            Text(_fmtMiles(l.aves), style: const TextStyle(
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

String _fmtMiles(int n) => n.toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.');

/// San Marino Zootécnico — app móvil.
/// Offline-first: todo registro se guarda en SQLite y se sincroniza cuando hay red.
library;

import 'package:flutter/material.dart';
import 'theme/app_theme.dart';
import 'theme/app_colors.dart';
import 'theme/app_spacing.dart';
import 'core/models.dart';
import 'core/local_db.dart';
import 'core/sync_service.dart';
import 'screens/login_screen.dart';
import 'screens/home_screen.dart';
import 'screens/app_screens.dart';
import 'screens/seguimiento_screen.dart';

void main() {
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

class _RootShellState extends State<RootShell> {
  final SyncService _sync = SyncService();
  Usuario? _usuario;
  List<Lote> _lotes = const [];
  int _tab = 0;
  ModuloSeguimiento? _filtroLotes;

  @override
  void initState() {
    super.initState();
    _sync.init();
    _sync.addListener(_onSync);
  }

  void _onSync() { if (mounted) setState(() {}); }

  @override
  void dispose() {
    _sync.removeListener(_onSync);
    _sync.dispose();
    super.dispose();
  }

  Future<void> _login(Usuario u) async {
    // TODO: los lotes vienen del backend según granjas asignadas.
    // Se cachean en SQLite para poder trabajar sin red desde el arranque.
    final lotes = _lotesDemo.where((l) => u.loteIds.contains(l.id)).toList();
    await LocalDb.instance.guardarLotes(lotes);
    if (!mounted) return;
    setState(() { _usuario = u; _lotes = lotes; _tab = 0; });
  }

  void _logout() => setState(() { _usuario = null; _tab = 0; });

  Future<void> _nuevoSeguimiento(ModuloSeguimiento? modulo, Lote? lote) async {
    final u = _usuario;
    if (u == null) return;

    var destino = lote;
    destino ??= await mostrarSelectorLote(
      context: context, usuario: u, lotes: _lotes, moduloPreseleccionado: modulo,
    );
    if (destino == null || !mounted) return;

    await Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => SeguimientoScreen(lote: destino!, usuario: u, sync: _sync),
    ));
  }

  void _verSync() {
    Navigator.of(context).push(MaterialPageRoute(builder: (_) => SyncScreen(sync: _sync)));
  }

  @override
  Widget build(BuildContext context) {
    final u = _usuario;
    if (u == null) return LoginScreen(onLogin: _login);

    return Scaffold(
      backgroundColor: AppColors.cream,
      body: SafeArea(
        bottom: false,
        child: switch (_tab) {
          0 => HomeScreen(
            usuario: u, lotes: _lotes, sync: _sync,
            onNuevoSeguimiento: _nuevoSeguimiento,
            onVerLotes: () => setState(() { _tab = 1; _filtroLotes = null; }),
            onVerSync: _verSync,
            onPerfil: () => setState(() => _tab = 2),
          ),
          1 => LotesScreen(
            usuario: u, lotes: _lotes, filtroInicial: _filtroLotes,
            onRegistrar: (l) => _nuevoSeguimiento(l.modulo, l),
          ),
          _ => PerfilScreen(usuario: u, onLogout: _logout),
        },
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
class _BottomNav extends StatelessWidget {
  const _BottomNav({required this.index, required this.onTab, required this.onPlus});

  final int index;
  final ValueChanged<int> onTab;
  final VoidCallback onPlus;

  @override
  Widget build(BuildContext context) {
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
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceAround, children: [
          _tab(0, Icons.home_rounded, Icons.home_outlined, 'Inicio'),
          _tab(1, Icons.layers_rounded, Icons.layers_outlined, 'Lotes'),
          // FAB — sobresale de la barra
          Transform.translate(
            offset: const Offset(0, -22),
            child: GestureDetector(
              onTap: onPlus,
              child: Container(
                width: 56, height: 56,
                decoration: BoxDecoration(
                  color: AppColors.orange500,
                  shape: BoxShape.circle,
                  boxShadow: [BoxShadow(
                    color: AppColors.orange500.withValues(alpha: 0.4),
                    blurRadius: 16, offset: const Offset(0, 6),
                  )],
                ),
                child: const Icon(Icons.add_rounded, size: 28, color: Colors.white),
              ),
            ),
          ),
          _tab(2, Icons.person_rounded, Icons.person_outline_rounded, 'Perfil'),
          const SizedBox(width: 56), // equilibra el espacio del FAB
        ]),
      ),
    );
  }

  Widget _tab(int i, IconData activo, IconData inactivo, String label) {
    final on = index == i;
    return GestureDetector(
      onTap: () => onTab(i),
      child: Container(
        constraints: const BoxConstraints(minWidth: 56, minHeight: AppTouch.min),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: on ? AppColors.green50 : Colors.transparent,
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
          Icon(on ? activo : inactivo, size: 22, color: on ? AppColors.green600 : AppColors.ink300),
          const SizedBox(height: 3),
          Text(label, style: TextStyle(
            fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w600,
            color: on ? AppColors.green600 : AppColors.ink300,
          )),
        ]),
      ),
    );
  }
}

/// Datos de demostración. Reemplazar por el endpoint de lotes asignados.
const _lotesDemo = <Lote>[
  Lote(id: 1, nombre: 'Lote 4', granja: 'Las Palmas', galpon: 'Galpón A',
    modulo: ModuloSeguimiento.levante, dia: 142, aves: 8420, viabilidad: 96.4,
    raza: 'Ross 308', anoTablaGenetica: 2026),
  Lote(id: 2, nombre: 'Lote 7', granja: 'Las Palmas', galpon: 'Galpón C',
    modulo: ModuloSeguimiento.engorde, dia: 35, aves: 4060, viabilidad: 98.1),
  Lote(id: 3, nombre: 'Lote 12', granja: 'San Antonio', galpon: 'Galpón B',
    modulo: ModuloSeguimiento.produccion, dia: 218, aves: 6280, viabilidad: 94.8),
  Lote(id: 4, nombre: 'Lote 3R', granja: 'San Antonio', galpon: 'Galpón D',
    modulo: ModuloSeguimiento.reproductora, dia: 88, aves: 3200, viabilidad: 97.2),
  Lote(id: 5, nombre: 'Lote 9', granja: 'El Rosal', galpon: 'Galpón A',
    modulo: ModuloSeguimiento.levante, dia: 70, aves: 5840, viabilidad: 97.8),
];

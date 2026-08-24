/// Lotes asignados al usuario: listado, búsqueda y filtro por módulo.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/shared/formato.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';

/// Viabilidad a partir de la cual la cifra se lee como sana.
const double _umbralViabilidad = 95;

/// Color categórico del módulo: identifica de qué módulo es el lote, no señala
/// un estado. Por eso Levante puede ser verde sin romper la regla de marca.
Color _colorModulo(ModuloSeguimiento m) => switch (m) {
  ModuloSeguimiento.levante      => AppColors.levante,
  ModuloSeguimiento.engorde      => AppColors.engorde,
  ModuloSeguimiento.produccion   => AppColors.produccion,
  ModuloSeguimiento.reproductora => AppColors.reproductora,
};

/// Tinta de la cifra de viabilidad bajo el umbral.
///
/// Sale del ámbar semántico del sistema oscurecido hacia la tinta principal:
/// `AppColors.warning` puro no aguanta la lectura de una cifra a pleno sol
/// sobre crema, y clavar un hex acá dejaría el único color de alerta de la
/// pantalla fuera de los tokens.
final Color _tintaAlerta = Color.lerp(AppColors.warning, AppColors.ink900, 0.35)!;

class LotesPage extends StatefulWidget {
  const LotesPage({
    super.key,
    required this.usuario,
    required this.lotes,
    required this.onRegistrar,
    this.onTrasladarHuevos,
    this.filtroInicial,
  });

  final Usuario usuario;
  final List<Lote> lotes;
  final ValueChanged<Lote> onRegistrar;

  /// Solo se ofrece en lotes de PRODUCCION: es el unico modulo que produce
  /// huevos para mover. Null = la accion no esta disponible.
  final ValueChanged<Lote>? onTrasladarHuevos;
  final ModuloSeguimiento? filtroInicial;

  @override
  State<LotesPage> createState() => _LotesScreenState();
}

class _LotesScreenState extends State<LotesPage> {
  /// El controller existe solo para que el botón de limpiar pueda vaciar el
  /// campo; el filtrado sigue saliendo de `_query`.
  final TextEditingController _buscador = TextEditingController();

  ModuloSeguimiento? _filtro;
  String _query = '';

  @override
  void initState() { super.initState(); _filtro = widget.filtroInicial; }

  @override
  void dispose() { _buscador.dispose(); super.dispose(); }

  List<Lote> get _visibles => widget.lotes.where((l) {
    if (_filtro != null && l.modulo != _filtro) return false;
    if (_query.isNotEmpty) {
      final t = '${l.nombre} ${l.granja} ${l.galpon}'.toLowerCase();
      if (!t.contains(_query.toLowerCase())) return false;
    }
    return true;
  }).toList();

  void _limpiarBusqueda() {
    _buscador.clear();
    setState(() => _query = '');
  }

  /// Salida del estado vacío: devuelve la vista a "todos". Sin esto el usuario
  /// se queda mirando una lista en blanco sin saber qué la está escondiendo.
  void _limpiarFiltros() {
    _buscador.clear();
    setState(() { _query = ''; _filtro = null; });
  }

  @override
  Widget build(BuildContext context) {
    final v = _visibles;
    final filtrando = _query.isNotEmpty || _filtro != null;

    return Column(children: [
      _encabezado(),
      Expanded(
        child: v.isEmpty
          ? EntradaEscalonada(
              indice: 0,
              child: _VacioLotes(
                query: _query,
                modulo: _filtro,
                hayLotes: widget.lotes.isNotEmpty,
                onLimpiar: filtrando ? _limpiarFiltros : null,
              ),
            )
          : ListView.separated(
              // El colchón inferior deja pasar la barra de navegación y el FAB.
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s10 + AppSpacing.s7,
              ),
              itemCount: v.length,
              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.s3),
              itemBuilder: (_, i) => EntradaEscalonada(
                indice: i,
                child: _LoteCard(
                  lote: v[i],
                  onRegistrar: () => widget.onRegistrar(v[i]),
                  onTrasladarHuevos: widget.onTrasladarHuevos == null
                      ? null
                      : () => widget.onTrasladarHuevos!(v[i]),
                ),
              ),
            ),
      ),
    ]);
  }

  Widget _encabezado() {
    return Container(
      // Una línea separa el bloque de filtros de la lista sin agregar una
      // sombra que compita con la de las tarjetas.
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border(bottom: BorderSide(color: AppColors.line)),
      ),
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
          controller: _buscador,
          onChanged: (t) => setState(() => _query = t),
          textInputAction: TextInputAction.search,
          style: const TextStyle(fontFamily: 'Inter', fontSize: 14),
          decoration: InputDecoration(
            hintText: 'Buscar lote, granja, galpón…',
            // Crema sobre la superficie blanca del encabezado: el campo se lee
            // como un buscador y no como un input de captura.
            fillColor: AppColors.cream,
            isDense: true,
            prefixIcon: const Icon(Icons.search_rounded, size: 18, color: AppColors.ink500),
            suffixIcon: _query.isEmpty ? null : IconButton(
              onPressed: _limpiarBusqueda,
              icon: const Icon(Icons.close_rounded, size: 18),
              color: AppColors.ink500,
              tooltip: 'Limpiar búsqueda',
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(minWidth: AppTouch.min, minHeight: AppTouch.min),
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.s3),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(children: [
            _chip('Todos', _filtro == null, null),
            for (final m in widget.usuario.modulos) ...[
              const SizedBox(width: AppSpacing.s2),
              _chip(m.label, _filtro == m, m),
            ],
          ]),
        ),
      ]),
    );
  }

  Widget _chip(String label, bool activo, ModuloSeguimiento? m) {
    final color = m == null ? AppColors.ink900 : _colorModulo(m);
    final duracion = AppMotion.duracion(context, AppMotion.fast);

    return PresionHundida(
      onTap: () => setState(() => _filtro = m),
      child: AnimatedContainer(
        duration: duracion,
        curve: AppMotion.simetrica,
        // Alto de dedo con guante: el filtro se toca de pie en el galpón.
        height: AppTouch.min,
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: activo ? color : AppColors.cream2,
          borderRadius: BorderRadius.circular(AppRadius.pill),
          border: Border.all(color: activo ? color : AppColors.line),
        ),
        child: AnimatedDefaultTextStyle(
          duration: duracion,
          curve: AppMotion.simetrica,
          style: TextStyle(
            fontFamily: 'Inter', fontSize: AppFontSize.sm,
            fontWeight: activo ? FontWeight.w700 : FontWeight.w600,
            color: activo ? Colors.white : AppColors.ink700,
          ),
          child: Text(label),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Tarjeta de lote
// ═══════════════════════════════════════════════════════════════════════════

class _LoteCard extends StatelessWidget {
  const _LoteCard({required this.lote, required this.onRegistrar, this.onTrasladarHuevos});

  final Lote lote;
  final VoidCallback onRegistrar;
  final VoidCallback? onTrasladarHuevos;

  @override
  Widget build(BuildContext context) {
    final color = _colorModulo(lote.modulo);
    final tone = switch (lote.modulo) {
      ModuloSeguimiento.levante      => BadgeTone.success,
      ModuloSeguimiento.engorde      => BadgeTone.orange,
      ModuloSeguimiento.produccion   => BadgeTone.info,
      ModuloSeguimiento.reproductora => BadgeTone.neutral,
    };

    // La tarjeta entera es el objetivo táctil; `PresionHundida` reemplaza al
    // ripple de Material, que en una tarjeta con borde de acento se recortaba
    // mal y no daba realimentación con guantes.
    return PresionHundida(
      onTap: onRegistrar,
      child: Container(
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
                  const SizedBox(height: AppSpacing.s1),
                  Text('${lote.granja} · ${lote.galpon}', style: const TextStyle(
                    fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
                  )),
                ])),
                const Icon(Icons.chevron_right_rounded, size: 20, color: AppColors.ink200),
              ]),
              const SizedBox(height: AppSpacing.s3),
              Row(children: [
                Expanded(child: AppStatTile(label: 'Aves', value: fmtMiles(lote.aves))),
                const SizedBox(width: AppSpacing.s2),
                Expanded(child: AppStatTile(label: 'Día', value: '${lote.dia}')),
                const SizedBox(width: AppSpacing.s2),
                Expanded(child: AppStatTile(
                  label: 'Viabilidad',
                  value: lote.viabilidad != null ? '${lote.viabilidad!.toStringAsFixed(1).replaceAll('.', ',')}%' : '—',
                  color: (lote.viabilidad ?? 0) >= _umbralViabilidad ? AppColors.green600 : _tintaAlerta,
                )),
              ]),
              const SizedBox(height: AppSpacing.s3),
              Row(mainAxisAlignment: MainAxisAlignment.end, children: [
                // Mover huevos solo tiene sentido en produccion, y es accion
                // secundaria: la principal de la tarjeta sigue siendo el dia.
                if (onTrasladarHuevos != null &&
                    lote.modulo == ModuloSeguimiento.produccion) ...[
                  AppButton(label: 'Trasladar huevos', size: AppButtonSize.sm,
                    variant: AppButtonVariant.secondary,
                    icon: Icons.egg_outlined, onPressed: onTrasladarHuevos),
                  const SizedBox(width: AppSpacing.s2),
                ],
                // Naranja: registrar el día es LA acción de la tarjeta. El verde
                // queda reservado al éxito y al color del módulo Levante.
                AppButton(label: 'Registrar día', size: AppButtonSize.sm,
                  variant: AppButtonVariant.primary,
                  icon: Icons.add_rounded, onPressed: onRegistrar),
              ]),
            ]),
          ),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Estado vacío
// ═══════════════════════════════════════════════════════════════════════════

/// Vacío con salida: dice QUÉ está escondiendo los lotes y ofrece quitarlo.
class _VacioLotes extends StatelessWidget {
  const _VacioLotes({
    required this.query,
    required this.modulo,
    required this.hayLotes,
    this.onLimpiar,
  });

  final String query;
  final ModuloSeguimiento? modulo;

  /// Si el usuario no tiene ningún lote asignado, el vacío no es culpa de un
  /// filtro y ofrecer "limpiar" sería mandarlo a otra pantalla en blanco.
  final bool hayLotes;
  final VoidCallback? onLimpiar;

  @override
  Widget build(BuildContext context) {
    final porFiltro = hayLotes && onLimpiar != null;

    final (icono, titulo, detalle) = switch ((porFiltro, query.isNotEmpty)) {
      (false, _) => (
        Icons.inbox_rounded,
        'Todavía no tienes lotes',
        'Cuando te asignen lotes en tus granjas van a aparecer acá.',
      ),
      (true, true) => (
        Icons.search_off_rounded,
        'No hay lotes que coincidan con «$query»',
        modulo == null
          ? 'Revisá el nombre del lote, la granja o el galpón.'
          : 'La búsqueda está limitada al módulo ${modulo!.label}.',
      ),
      (true, false) => (
        Icons.filter_alt_off_rounded,
        'No hay lotes de ${modulo?.label ?? ''}',
        'Ninguno de tus lotes pertenece a este módulo.',
      ),
    };

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s7, vertical: AppSpacing.s6),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Container(
            width: AppSpacing.s10, height: AppSpacing.s10,
            decoration: const BoxDecoration(color: AppColors.cream2, shape: BoxShape.circle),
            child: Icon(icono, color: AppColors.ink300),
          ),
          const SizedBox(height: AppSpacing.s4),
          Text(titulo, textAlign: TextAlign.center, style: const TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base,
            fontWeight: FontWeight.w700, color: AppColors.ink900,
          )),
          const SizedBox(height: AppSpacing.s2),
          Text(detalle, textAlign: TextAlign.center, style: const TextStyle(
            fontFamily: 'Inter', fontSize: AppFontSize.sm, height: 1.5, color: AppColors.ink500,
          )),
          if (porFiltro) ...[
            const SizedBox(height: AppSpacing.s5),
            AppButton(
              label: 'Limpiar filtros',
              size: AppButtonSize.sm,
              variant: AppButtonVariant.primary,
              icon: Icons.filter_alt_off_rounded,
              onPressed: onLimpiar,
            ),
          ],
        ]),
      ),
    );
  }
}

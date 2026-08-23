/// Inicio: saludo, estado del día, módulos habilitados y lotes recientes.
///
/// Jerarquía de la pantalla (de mayor a menor peso): tarjeta de bienvenida →
/// encabezados de sección → tarjetas/filas → notas al pie. El saludo y la
/// bienvenida son el ancla: es lo primero que mira el supervisor al abrir la
/// app parado en el galpón.
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
        // Aire al final: el bottom nav con su botón central tapa la última fila.
        padding: const EdgeInsets.only(bottom: AppSpacing.s10 + AppSpacing.s7),
        children: [
          _topBar(),
          _bienvenida(),
          // Las secciones se pintan SIEMPRE: antes desaparecían al no haber
          // datos y la pantalla quedaba muda, sin decir qué faltaba.
          _modulos(),
          _misLotes(),
          if (sync.pendientes > 0 || !sync.enLinea) _pendientes(),
        ],
      ),
      SyncRibbon(sync: sync),
    ]);
  }

  // ══ ENCABEZADO ═══════════════════════════════════════════════════════════

  Widget _topBar() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s5, AppSpacing.s4, AppSpacing.s5),
      child: Row(children: [
        PresionHundida(
          onTap: onPerfil,
          child: Stack(clipBehavior: Clip.none, children: [
            Container(
              width: AppTouch.min, height: AppTouch.min,
              decoration: BoxDecoration(
                // El avatar es IDENTIDAD de la persona, no una acción: va en
                // neutro cálido y deja el naranja libre para los botones.
                color: AppColors.cream2,
                borderRadius: BorderRadius.circular(AppRadius.md),
                border: Border.all(color: AppColors.lineStrong),
              ),
              alignment: Alignment.center,
              child: Text(usuario.iniciales, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base,
                fontWeight: FontWeight.w800, color: AppColors.ink700,
              )),
            ),
            Positioned(
              top: -AppSpacing.s1 / 2, right: -AppSpacing.s1 / 2,
              child: AmbientDot(sync: sync),
            ),
          ]),
        ),
        const SizedBox(width: AppSpacing.s3),
        Expanded(child: GestureDetector(
          onTap: onPerfil,
          // Opaque: el bloque entero es tocable, no solo los renglones de texto.
          behavior: HitTestBehavior.opaque,
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Buen día,', style: TextStyle(
              fontFamily: 'Inter', fontSize: AppFontSize.xs, color: AppColors.ink500,
            )),
            Text(usuario.nombre, maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.md,
                fontWeight: FontWeight.w800, letterSpacing: -0.3, color: AppColors.ink900,
              )),
            Text('${usuario.cargo} · ${usuario.granja}', maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontFamily: 'Inter', fontSize: AppFontSize.xs, color: AppColors.ink500)),
          ]),
        )),
        const SizedBox(width: AppSpacing.s2),
        _EstadoConexion(sync: sync, onTap: onVerSync),
      ]),
    );
  }

  // ══ BIENVENIDA (ancla visual) ════════════════════════════════════════════

  Widget _bienvenida() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s6),
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.xl),
          border: Border.all(color: AppColors.line),
          // Sombra media (antes `sm`): es la tarjeta que manda en la pantalla.
          boxShadow: AppColors.shadowMd,
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(children: [
          Container(
            decoration: const BoxDecoration(
              // Cálido, no verde: el verde está reservado a éxito y a Levante.
              // El degradado también le da contraste al huevo blanco y a las
              // patas naranjas de la gallina, que sobre verde se apagaban.
              gradient: LinearGradient(
                begin: Alignment.topLeft, end: Alignment.bottomRight,
                colors: [AppColors.brand50, AppColors.brand200],
              ),
            ),
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s6, AppSpacing.s4, AppSpacing.s3),
            child: const GallinaAnimacion(),
          ),
          Padding(
            padding: const EdgeInsets.all(AppSpacing.s5),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('¡Listo para registrar hoy!', style: TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.xl,
                fontWeight: FontWeight.w800, height: 1.15, letterSpacing: -0.6,
                color: AppColors.ink900,
              )),
              const SizedBox(height: AppSpacing.s2),
              Text(_fechaLarga(DateTime.now()), style: const TextStyle(
                fontFamily: 'Inter', fontSize: AppFontSize.sm, height: 1.4, color: AppColors.ink500,
              )),
            ]),
          ),
        ]),
      ),
    );
  }

  // ══ SEGUIMIENTO DIARIO ═══════════════════════════════════════════════════

  Widget _modulos() {
    final mods = usuario.modulos;
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s6),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        _EncabezadoSeccion(
          titulo: 'Seguimiento diario',
          // Sin módulos no hay nada que crear: la acción se esconde.
          accionLabel: mods.isEmpty ? null : 'Nuevo',
          accionIcono: Icons.add_rounded,
          onAccion: () => onNuevoSeguimiento(null, null),
        ),
        const SizedBox(height: AppSpacing.s3),
        if (mods.isEmpty)
          const _EstadoVacio(
            icono: Icons.grid_view_rounded,
            titulo: 'Sin módulos habilitados',
            mensaje: 'Tu usuario todavía no tiene módulos de seguimiento asignados. '
                'Pedile al administrador que te habilite al menos uno.',
          )
        else
          _grillaModulos(mods),
      ]),
    );
  }

  /// Hasta 3 módulos entran en una fila; con 4 se arma una grilla 2×2 para que
  /// el nombre completo quepa y la tarjeta siga siendo tocable con guantes.
  Widget _grillaModulos(List<ModuloSeguimiento> mods) {
    final porFila = mods.length <= 3 ? mods.length : 2;
    final filas = <Widget>[];

    for (int inicio = 0; inicio < mods.length; inicio += porFila) {
      final celdas = <Widget>[];
      for (int col = 0; col < porFila; col++) {
        final i = inicio + col;
        if (col > 0) celdas.add(const SizedBox(width: AppSpacing.s3));
        celdas.add(Expanded(
          child: i >= mods.length
              // Hueco de la última fila incompleta: mantiene el ancho de las
              // tarjetas en vez de estirar la que quedó sola.
              ? const SizedBox.shrink()
              : EntradaEscalonada(
                  indice: i,
                  child: _ModuloCard(
                    modulo: mods[i],
                    lotes: lotes.where((l) => l.modulo == mods[i]).length,
                    onTap: () => onNuevoSeguimiento(mods[i], null),
                  ),
                ),
        ));
      }
      if (filas.isNotEmpty) filas.add(const SizedBox(height: AppSpacing.s3));
      // IntrinsicHeight + stretch: todas las tarjetas de la fila miden lo mismo
      // aunque una etiqueta ocupe dos renglones.
      filas.add(IntrinsicHeight(
        child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: celdas),
      ));
    }

    return Column(children: filas);
  }

  // ══ MIS LOTES ════════════════════════════════════════════════════════════

  Widget _misLotes() {
    final visibles = lotes.take(3).toList();
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s6),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        _EncabezadoSeccion(
          titulo: 'Mis lotes',
          accionLabel: visibles.isEmpty ? null : 'Ver todos',
          accionIcono: Icons.chevron_right_rounded,
          iconoAlFinal: true,
          onAccion: onVerLotes,
        ),
        const SizedBox(height: AppSpacing.s3),
        if (visibles.isEmpty)
          _EstadoVacio(
            icono: Icons.inbox_rounded,
            titulo: 'Todavía no hay lotes',
            // Sin señal no es una falla: se dice qué va a pasar, no se alarma.
            mensaje: 'Los lotes se descargan del servidor cuando la app sincroniza. '
                'Si estás sin señal, aparecerán al volver la red.',
            accionLabel: 'Revisar sincronización',
            onAccion: onVerSync,
          )
        else
          Container(
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppRadius.lg),
              border: Border.all(color: AppColors.line),
              boxShadow: AppColors.shadowSm,
            ),
            clipBehavior: Clip.antiAlias,
            child: Column(children: [
              for (int i = 0; i < visibles.length; i++) ...[
                if (i > 0) const Divider(),
                EntradaEscalonada(
                  indice: i,
                  child: _LoteRow(
                    lote: visibles[i],
                    onTap: () => onNuevoSeguimiento(visibles[i].modulo, visibles[i]),
                  ),
                ),
              ],
            ]),
          ),
      ]),
    );
  }

  // ══ COLA PENDIENTE ═══════════════════════════════════════════════════════

  Widget _pendientes() {
    final offline = !sync.enLinea;
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
      // El radio va en un ClipRRect y el borde adentro: un `Border` de lados
      // distintos MÁS `borderRadius` en el mismo `BoxDecoration` no falla al
      // compilar, revienta al pintar (trampa documentada en el CLAUDE.md).
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Material(
          color: AppColors.surface,
          child: InkWell(
            onTap: onVerSync,
            child: Container(
              padding: const EdgeInsets.all(AppSpacing.s4),
              decoration: BoxDecoration(
                border: Border(
                  // Sin conexión ⇒ tinta neutra, nunca rojo: es un modo de
                  // trabajo válido, no un error del operario.
                  left: BorderSide(color: offline ? AppColors.ink700 : AppColors.brand500, width: 3),
                  top: BorderSide(color: AppColors.line),
                  right: BorderSide(color: AppColors.line),
                  bottom: BorderSide(color: AppColors.line),
                ),
              ),
              child: Row(children: [
                Container(
                  width: AppSpacing.s7, height: AppSpacing.s7,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: offline ? AppColors.ink100 : AppColors.brand50,
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  child: Icon(offline ? Icons.wifi_off_rounded : Icons.schedule_rounded,
                    size: AppFontSize.md, color: offline ? AppColors.ink700 : AppColors.brand600),
                ),
                const SizedBox(width: AppSpacing.s3),
                Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(
                    sync.pendientes == 1
                      ? '1 registro guardado aquí'
                      : '${sync.pendientes} registros guardados aquí',
                    style: const TextStyle(
                      fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base,
                      fontWeight: FontWeight.w700, color: AppColors.ink900,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.s1 / 2),
                  Text(offline ? 'Se enviarán cuando vuelva la red' : 'Toca para revisar la cola',
                    style: const TextStyle(fontFamily: 'Inter', fontSize: AppFontSize.sm, color: AppColors.ink500)),
                ])),
                if (!offline) ...[
                  const SizedBox(width: AppSpacing.s2),
                  AppButton(label: 'Sincronizar', size: AppButtonSize.sm,
                    variant: AppButtonVariant.primary, onPressed: sync.sincronizar),
                ],
              ]),
            ),
          ),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Estado de conexión — nunca queda en blanco
// ═══════════════════════════════════════════════════════════════════════════

class _EstadoConexion extends StatelessWidget {
  const _EstadoConexion({required this.sync, required this.onTap});

  final SyncService sync;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // `ConnectionChip` se esconde solo cuando no hay nada que decir. Ahí se
    // pinta el chip "Al día" para que el encabezado siempre informe el estado
    // de la conexión, en vez de dejar un hueco que se lee como "no sé".
    if (!sync.todoAlDia) return ConnectionChip(sync: sync, onTap: onTap);

    return PresionHundida(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
        decoration: BoxDecoration(
          // Verde = éxito. Es el único uso permitido fuera del módulo Levante.
          color: AppColors.successBg,
          borderRadius: BorderRadius.circular(AppRadius.pill),
        ),
        child: const Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(Icons.check_rounded, size: AppFontSize.sm, color: AppColors.success),
          SizedBox(width: AppSpacing.s1),
          Text('Al día', style: TextStyle(
            fontFamily: 'Inter', fontSize: AppFontSize.xs,
            fontWeight: FontWeight.w700, color: AppColors.green700,
          )),
        ]),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Encabezado de sección + su acción
// ═══════════════════════════════════════════════════════════════════════════

class _EncabezadoSeccion extends StatelessWidget {
  const _EncabezadoSeccion({
    required this.titulo,
    this.accionLabel,
    this.accionIcono,
    this.onAccion,
    this.iconoAlFinal = false,
  });

  final String titulo;
  final String? accionLabel;
  final IconData? accionIcono;
  final VoidCallback? onAccion;
  final bool iconoAlFinal;

  @override
  Widget build(BuildContext context) {
    final label = accionLabel;
    final accion = onAccion;

    return Row(children: [
      Expanded(child: Text(titulo, style: const TextStyle(
        fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.md,
        fontWeight: FontWeight.w800, letterSpacing: -0.2, color: AppColors.ink900,
      ))),
      if (label != null && accion != null)
        _AccionSeccion(label: label, icono: accionIcono, iconoAlFinal: iconoAlFinal, onTap: accion),
    ]);
  }
}

class _AccionSeccion extends StatelessWidget {
  const _AccionSeccion({
    required this.label,
    required this.onTap,
    this.icono,
    this.iconoAlFinal = false,
  });

  final String label;
  final VoidCallback onTap;
  final IconData? icono;
  final bool iconoAlFinal;

  @override
  Widget build(BuildContext context) {
    final icono = this.icono;
    final icon = icono == null
        ? null
        : Icon(icono, size: AppFontSize.md, color: AppColors.brand700);

    return PresionHundida(
      onTap: onTap,
      child: Container(
        // Naranja = acción (regla de marca). Pastilla clara para no competir
        // con el CTA del bottom nav, con alto suficiente para tocarla con guantes.
        constraints: const BoxConstraints(minHeight: AppTouch.min - AppSpacing.s2),
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
        decoration: BoxDecoration(
          color: AppColors.brand50,
          borderRadius: BorderRadius.circular(AppRadius.pill),
          border: Border.all(color: AppColors.brand100),
        ),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          if (icon != null && !iconoAlFinal) ...[icon, const SizedBox(width: AppSpacing.s1)],
          Text(label, style: const TextStyle(
            fontFamily: 'Inter', fontSize: AppFontSize.sm,
            fontWeight: FontWeight.w700, color: AppColors.brand700,
          )),
          if (icon != null && iconoAlFinal) ...[const SizedBox(width: AppSpacing.s1), icon],
        ]),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Estado vacío
// ═══════════════════════════════════════════════════════════════════════════

class _EstadoVacio extends StatelessWidget {
  const _EstadoVacio({
    required this.icono,
    required this.titulo,
    required this.mensaje,
    this.accionLabel,
    this.onAccion,
  });

  final IconData icono;
  final String titulo;
  final String mensaje;
  final String? accionLabel;
  final VoidCallback? onAccion;

  @override
  Widget build(BuildContext context) {
    final label = accionLabel;
    final accion = onAccion;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s5, vertical: AppSpacing.s6),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
      ),
      child: Column(children: [
        Container(
          width: AppSpacing.s9, height: AppSpacing.s9,
          alignment: Alignment.center,
          decoration: const BoxDecoration(color: AppColors.cream2, shape: BoxShape.circle),
          child: Icon(icono, size: AppFontSize.lg, color: AppColors.ink300),
        ),
        const SizedBox(height: AppSpacing.s3),
        Text(titulo, textAlign: TextAlign.center, style: const TextStyle(
          fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base,
          fontWeight: FontWeight.w700, color: AppColors.ink900,
        )),
        const SizedBox(height: AppSpacing.s1),
        Text(mensaje, textAlign: TextAlign.center, style: const TextStyle(
          fontFamily: 'Inter', fontSize: AppFontSize.sm, height: 1.45, color: AppColors.ink500,
        )),
        if (label != null && accion != null) ...[
          const SizedBox(height: AppSpacing.s4),
          // `accent` = naranja. `primary`/`secondary` del DS son verdes y el
          // verde no puede ser una acción.
          AppButton(
            label: label, size: AppButtonSize.sm,
            variant: AppButtonVariant.primary, onPressed: accion,
          ),
        ],
      ]),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Tarjeta de módulo
// ═══════════════════════════════════════════════════════════════════════════

class _ModuloCard extends StatelessWidget {
  const _ModuloCard({required this.modulo, required this.lotes, required this.onTap});

  final ModuloSeguimiento modulo;
  final int lotes;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = _colorModulo(modulo);

    return PresionHundida(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s4),
        decoration: BoxDecoration(
          // El tinte se deriva del color categórico del módulo: un solo origen
          // para los cuatro, sin hex sueltos por tarjeta.
          color: color.withValues(alpha: 0.10),
          borderRadius: BorderRadius.circular(AppRadius.lg),
          border: Border.all(color: color.withValues(alpha: 0.22)),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(modulo.emoji, style: const TextStyle(fontSize: AppFontSize.xl)),
          const SizedBox(height: AppSpacing.s2),
          Text(modulo.label, maxLines: 2, style: const TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.sm,
            fontWeight: FontWeight.w700, height: 1.2, color: AppColors.ink900,
          )),
          const SizedBox(height: AppSpacing.s2),
          Row(children: [
            Container(
              width: AppSpacing.s2, height: AppSpacing.s2,
              decoration: BoxDecoration(color: color, shape: BoxShape.circle),
            ),
            const SizedBox(width: AppSpacing.s1),
            // El conteo va en tinta y el color del módulo queda en el punto:
            // sobre el tinte claro, dos de los cuatro colores no alcanzaban
            // contraste para leerse al sol.
            Flexible(child: Text('$lotes lotes', maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'Inter', fontSize: AppFontSize.xs,
                fontWeight: FontWeight.w600, color: AppColors.ink700,
              ))),
          ]),
        ]),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Fila de lote
// ═══════════════════════════════════════════════════════════════════════════

class _LoteRow extends StatelessWidget {
  const _LoteRow({required this.lote, required this.onTap});

  final Lote lote;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = _colorModulo(lote.modulo);

    return PresionHundida(
      onTap: onTap,
      child: Container(
        // Fondo explícito: `PresionHundida` detecta el toque sobre su hijo, y
        // sin un color sólido los huecos entre los textos no responderían.
        color: AppColors.surface,
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
        child: Row(children: [
          Container(
            width: AppSpacing.s2, height: AppSpacing.s2,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(lote.nombre, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base,
              fontWeight: FontWeight.w700, color: AppColors.ink900,
            )),
            const SizedBox(height: AppSpacing.s1 / 2),
            Text('${lote.granja} · Día ${lote.dia}', maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'Inter', fontSize: AppFontSize.xs, color: AppColors.ink500,
              )),
          ])),
          const SizedBox(width: AppSpacing.s2),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(_fmtMiles(lote.aves), style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: AppFontSize.base, fontWeight: FontWeight.w700,
              color: AppColors.ink900, fontFeatures: [FontFeature.tabularFigures()],
            )),
            const SizedBox(height: AppSpacing.s1 / 2),
            Text(lote.modulo.label, style: TextStyle(
              fontFamily: 'Inter', fontSize: AppFontSize.xs, fontWeight: FontWeight.w600, color: color,
            )),
          ]),
          const SizedBox(width: AppSpacing.s2),
          const Icon(Icons.chevron_right_rounded, size: AppFontSize.lg, color: AppColors.ink300),
        ]),
      ),
    );
  }
}

/// Color categórico del módulo. Eje distinto del semántico: identifica el
/// módulo, no un estado — por eso Levante puede ser verde sin romper la regla
/// de marca.
Color _colorModulo(ModuloSeguimiento m) => switch (m) {
  ModuloSeguimiento.levante      => AppColors.levante,
  ModuloSeguimiento.engorde      => AppColors.engorde,
  ModuloSeguimiento.produccion   => AppColors.produccion,
  ModuloSeguimiento.reproductora => AppColors.reproductora,
};

String _fmtMiles(int n) => n.toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.');

String _fechaLarga(DateTime d) {
  const dias = ['lunes','martes','miércoles','jueves','viernes','sábado','domingo'];
  const meses = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
  final dia = dias[d.weekday - 1];
  return '${dia[0].toUpperCase()}${dia.substring(1)} ${d.day} de ${meses[d.month - 1]}, ${d.year}';
}

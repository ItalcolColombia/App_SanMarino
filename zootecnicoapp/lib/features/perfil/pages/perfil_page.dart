/// Perfil del usuario: datos de cuenta, módulos asignados y cierre de sesión.
///
/// El pie de página mostraba `logo-italfoods-zootecnico.png`, un asset que ya
/// no existe en el repo: abrir Perfil reventaba la app con un error de asset.
/// Ahora la marca sale de [LogoMarca] / [DivisorMarca], que son la referencia
/// validada contra el login web.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/session/session_store.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/components/marca.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
// Cruza de feature a propósito, por el mismo motivo declarado para
// `features/sync/widgets/`: el estado de sincronización es global de la app, no
// del dominio Perfil. Acá está la única lista de "DATOS" del usuario, así que es
// donde el historial tiene que estar a mano.
import 'package:zootecnicoapp/features/sync/pages/historial_page.dart';

/// Lado del cuadrito de ícono de cada fila. Sale de la escala 4pt (32) para no
/// introducir una medida suelta: de él se deriva el sangrado de los divisores.
const double _ladoIcono = AppSpacing.s7;

/// Sangrado del divisor: arranca donde arranca el texto, no en el borde.
const double _sangriaDivisor = AppSpacing.s4 + _ladoIcono + AppSpacing.s3;

class PerfilPage extends StatelessWidget {
  const PerfilPage({super.key, required this.usuario, required this.onLogout});

  final Usuario usuario;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    // La sincronización ya se venía calculando y no se mostraba en ninguna
    // pantalla: el supervisor no tenía forma de saber con qué datos trabaja.
    final sesion = SessionStore.instance;
    final ultimaSync = sesion.ultimaSync;
    final sincronizadoHoy = sesion.sincronizadoHoy;

    return ListView(
      padding: const EdgeInsets.only(bottom: AppSpacing.s10 + AppSpacing.s7),
      children: [
        EntradaEscalonada(indice: 0, child: _cabecera()),
        EntradaEscalonada(indice: 1, child: _modulos()),

        EntradaEscalonada(
          indice: 2,
          child: _seccion('CUENTA', [
            _fila(Icons.mail_outline_rounded, 'Correo', usuario.email),
            _fila(Icons.lock_outline_rounded, 'Cambiar contraseña', null, onTap: () {}),
            _fila(Icons.notifications_outlined, 'Notificaciones', 'Activadas', onTap: () {}),
          ]),
        ),

        EntradaEscalonada(
          indice: 3,
          child: _seccion('DATOS', [
            _fila(Icons.sync_rounded, 'Sincronizar ahora', null, onTap: () {}),
            _fila(
              // Sin sincronizar NO es un error: es un modo de trabajo válido en el
              // galpón. El estado se comunica con el tinte, nunca con rojo. El
              // ícono va fijo: un IconData elegido en runtime no sobrevive al
              // tree-shaking de íconos y se pinta en blanco.
              Icons.sync_rounded,
              'Última sincronización',
              _textoUltimaSync(ultimaSync, sincronizadoHoy),
              tinteIcono: sincronizadoHoy ? AppColors.successBg : AppColors.warningBg,
              colorIcono: sincronizadoHoy ? AppColors.green600 : AppColors.warning,
            ),
            // Lo ya enviado vivía sólo en SQLite, sin pantalla: una vez que un
            // registro se sincronizaba desaparecía de la vista del usuario.
            _fila(
              Icons.history_rounded,
              'Historial de registros',
              null,
              onTap: () => Navigator.of(context).push(
                rutaApp((_) => const HistorialPage(), nombre: 'historial'),
              ),
            ),
            _fila(Icons.download_outlined, 'Descargar guías genéticas', null, onTap: () {}),
          ]),
        ),

        EntradaEscalonada(
          indice: 4,
          child: _seccion('SESIÓN', [
            _fila(Icons.logout_rounded, 'Cerrar sesión', null, onTap: onLogout, danger: true),
          ]),
        ),

        EntradaEscalonada(indice: 5, child: _pie()),
      ],
    );
  }

  // ══ CABECERA ═════════════════════════════════════════════════════════════

  Widget _cabecera() {
    // El fondo pasa de verde a naranja tenue: el verde es del módulo Levante y
    // del éxito, no de la identidad de la app (regla de marca).
    return Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [AppColors.brand50, AppColors.cream],
        ),
      ),
      padding: const EdgeInsets.fromLTRB(AppSpacing.s5, AppSpacing.s7, AppSpacing.s5, AppSpacing.s6),
      child: Column(children: [
        Container(
          width: AppSpacing.s10 + AppSpacing.s6,
          height: AppSpacing.s10 + AppSpacing.s6,
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: AppColors.brandGradient,
            ),
            borderRadius: BorderRadius.circular(AppRadius.xl),
            border: Border.all(color: AppColors.surface, width: 3),
            boxShadow: AppColors.shadowBrand,
          ),
          alignment: Alignment.center,
          child: Text(usuario.iniciales, style: const TextStyle(
            fontFamily: 'PlusJakartaSans',
            fontSize: AppFontSize.xxl,
            fontWeight: FontWeight.w800,
            letterSpacing: 0.5,
            color: Colors.white,
          )),
        ),
        const SizedBox(height: AppSpacing.s4),
        Text(usuario.nombre, textAlign: TextAlign.center, style: const TextStyle(
          fontFamily: 'PlusJakartaSans',
          fontSize: AppFontSize.lg,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.4,
          color: AppColors.ink900,
        )),
        const SizedBox(height: AppSpacing.s1),
        Text(_cargoYGranja(), textAlign: TextAlign.center, style: const TextStyle(
          fontFamily: 'Inter', fontSize: AppFontSize.sm, color: AppColors.ink500,
        )),
        const SizedBox(height: AppSpacing.s3),
        Wrap(alignment: WrapAlignment.center, spacing: AppSpacing.s2, runSpacing: AppSpacing.s1, children: [
          const AppBadge(label: 'Activo', tone: BadgeTone.success, dot: true),
          AppBadge(label: usuario.pais, tone: usuario.tieneControlAgua ? BadgeTone.info : BadgeTone.neutral),
        ]),
      ]),
    );
  }

  /// El cargo y la granja se unen sólo si hay ambos: la granja viene vacía
  /// cuando el usuario tiene varias asignadas, y el separador quedaba colgando.
  String _cargoYGranja() =>
      [usuario.cargo, usuario.granja].where((t) => t.trim().isNotEmpty).join(' · ');

  // ══ MÓDULOS ══════════════════════════════════════════════════════════════

  Widget _modulos() => _seccionEnvoltorio(
    'MÓDULOS ASIGNADOS',
    Container(
      width: double.infinity,
      padding: const EdgeInsets.all(AppSpacing.s4),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Wrap(spacing: AppSpacing.s2, runSpacing: AppSpacing.s2, children: [
          for (final m in usuario.modulos) _chipModulo(m),
        ]),
        if (usuario.tieneControlAgua) ...[
          const SizedBox(height: AppSpacing.s3),
          const AppBadge(label: 'Control de agua habilitado', tone: BadgeTone.info),
        ],
      ]),
    ),
  );

  /// Chip del módulo con su color categórico (eje distinto del semántico: no es
  /// un estado, identifica el módulo — por eso Levante puede ser verde).
  Widget _chipModulo(ModuloSeguimiento m) {
    final color = switch (m) {
      ModuloSeguimiento.levante => AppColors.levante,
      ModuloSeguimiento.engorde => AppColors.engorde,
      ModuloSeguimiento.produccion => AppColors.produccion,
      ModuloSeguimiento.reproductora => AppColors.reproductora,
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(AppRadius.pill),
        border: Border.all(color: color.withValues(alpha: 0.28)),
      ),
      child: Text('${m.emoji}  ${m.label}', style: const TextStyle(
        fontFamily: 'Inter',
        fontSize: AppFontSize.sm,
        fontWeight: FontWeight.w600,
        color: AppColors.ink900,
      )),
    );
  }

  // ══ SECCIONES Y FILAS ════════════════════════════════════════════════════

  Widget _seccion(String titulo, List<Widget> filas) => _seccionEnvoltorio(
    titulo,
    Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(children: [
        for (int i = 0; i < filas.length; i++) ...[
          if (i > 0) Divider(height: 1, indent: _sangriaDivisor, color: AppColors.line),
          filas[i],
        ],
      ]),
    ),
  );

  Widget _seccionEnvoltorio(String titulo, Widget contenido) => Padding(
    padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s5, AppSpacing.s4, 0),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Padding(
        padding: const EdgeInsets.only(left: AppSpacing.s1),
        child: Text(titulo, style: const TextStyle(
          fontFamily: 'Inter',
          fontSize: AppFontSize.xs,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.8,
          color: AppColors.ink500,
        )),
      ),
      const SizedBox(height: AppSpacing.s2),
      contenido,
    ]),
  );

  Widget _fila(
    IconData icon,
    String label,
    String? value, {
    VoidCallback? onTap,
    bool danger = false,
    Color? tinteIcono,
    Color? colorIcono,
  }) {
    // Destructivo: se marca con el rojo de peligro en el ícono y la etiqueta,
    // sin fondo rojo de fila — tiene que leerse claro, no gritar.
    final fondoIcono = danger ? AppColors.dangerBg : (tinteIcono ?? AppColors.cream2);
    final tintaIcono = danger ? AppColors.danger : (colorIcono ?? AppColors.ink700);

    final contenido = Padding(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
      child: ConstrainedBox(
        // Se usa con guantes: la fila nunca baja del objetivo táctil mínimo.
        constraints: const BoxConstraints(minHeight: AppTouch.min),
        child: Row(children: [
          Container(
            width: _ladoIcono,
            height: _ladoIcono,
            decoration: BoxDecoration(
              color: fondoIcono,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: Icon(icon, size: AppSpacing.s4, color: tintaIcono),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Text(label, style: TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.sm,
            fontWeight: danger ? FontWeight.w600 : FontWeight.w500,
            color: danger ? AppColors.danger : AppColors.ink900,
          ))),
          if (value != null) ...[
            const SizedBox(width: AppSpacing.s2),
            Flexible(child: Text(
              value,
              textAlign: TextAlign.right,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'Inter', fontSize: AppFontSize.sm, color: AppColors.ink500,
              ),
            )),
          ],
          if (onTap != null && !danger) ...[
            const SizedBox(width: AppSpacing.s2),
            const Icon(Icons.chevron_right_rounded, size: AppSpacing.s5, color: AppColors.ink300),
          ],
        ]),
      ),
    );

    if (onTap == null) return contenido;

    // `PresionHundida` en vez de `InkWell`: la fila no tiene fondo propio, así
    // que el hundido se ve sobre la tarjeta y da realimentación con guantes.
    return PresionHundida(onTap: onTap, escala: 0.985, child: contenido);
  }

  // ══ PIE ══════════════════════════════════════════════════════════════════

  Widget _pie() => Padding(
    padding: const EdgeInsets.fromLTRB(AppSpacing.s6, AppSpacing.s8, AppSpacing.s6, AppSpacing.s6),
    child: Column(children: [
      const DivisorMarca(),
      const SizedBox(height: AppSpacing.s5),
      const Opacity(
        opacity: 0.7,
        child: LogoMarca(mostrarTagline: false, alturaItalcol: 22, alturaSanMarino: 30),
      ),
      const SizedBox(height: AppSpacing.s4),
      const Text('© 2026 Italcol · v2.1.0', style: TextStyle(
        fontFamily: 'Inter', fontSize: AppFontSize.xs, color: AppColors.ink300,
      )),
    ]),
  );

  /// Fecha corta de la última bajada de datos. Se formatea a mano en vez de con
  /// `intl` porque la app no inicializa los datos de locale y `DateFormat` con
  /// meses en español fallaría en runtime.
  String _textoUltimaSync(DateTime? cuando, bool sincronizadoHoy) {
    if (cuando == null) return 'Nunca';

    final l = cuando.toLocal();
    final hh = l.hour.toString().padLeft(2, '0');
    final mm = l.minute.toString().padLeft(2, '0');
    if (sincronizadoHoy) return 'Hoy $hh:$mm';

    final dd = l.day.toString().padLeft(2, '0');
    final mes = l.month.toString().padLeft(2, '0');
    final anio = (l.year % 100).toString().padLeft(2, '0');
    return '$dd/$mes/$anio $hh:$mm';
  }
}

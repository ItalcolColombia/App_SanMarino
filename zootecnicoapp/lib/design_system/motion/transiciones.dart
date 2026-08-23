/// Transiciones de navegación y entradas reutilizables.
///
/// Reemplazan al `MaterialPageRoute` pelado que usaba la app: la transición por
/// defecto de Material en Android es un fade largo y en web no hay ninguna, y
/// eso es buena parte de lo que hacía que la app se sintiera menos terminada.
///
/// Todas respetan "Reducir movimiento" vía [AppMotion.duracion].
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/design_system/motion/app_motion.dart';

/// Ruta estándar de la app: fade + un desplazamiento corto hacia arriba.
///
/// Sustituye a `MaterialPageRoute` en todo push de pantalla completa.
/// ```dart
/// Navigator.of(context).push(rutaApp((_) => SeguimientoPage(...)));
/// ```
Route<T> rutaApp<T>(WidgetBuilder builder, {String? nombre}) {
  return PageRouteBuilder<T>(
    settings: RouteSettings(name: nombre),
    transitionDuration: AppMotion.page,
    reverseTransitionDuration: AppMotion.fast,
    pageBuilder: (context, animation, secondaryAnimation) => builder(context),
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      if (AppMotion.reducido(context)) return child;

      final entrada = CurvedAnimation(parent: animation, curve: AppMotion.entrada);
      return FadeTransition(
        opacity: entrada,
        child: SlideTransition(
          position: Tween<Offset>(
            // El offset va en fracción del alto de la pantalla; se convierte
            // desde píxeles para que el desplazamiento se vea igual en
            // cualquier tamaño de pantalla.
            begin: Offset(0, AppMotion.offsetPagina / MediaQuery.sizeOf(context).height),
            end: Offset.zero,
          ).animate(entrada),
          child: child,
        ),
      );
    },
  );
}

/// Ruta para pantallas modales de pantalla completa (formularios): entra desde
/// abajo, más marcada, porque es una tarea que el usuario "abre" y "cierra".
Route<T> rutaModal<T>(WidgetBuilder builder, {String? nombre}) {
  return PageRouteBuilder<T>(
    settings: RouteSettings(name: nombre),
    transitionDuration: AppMotion.slow,
    reverseTransitionDuration: AppMotion.base,
    pageBuilder: (context, animation, secondaryAnimation) => builder(context),
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      if (AppMotion.reducido(context)) return child;

      final entrada = CurvedAnimation(parent: animation, curve: AppMotion.entrada);
      return SlideTransition(
        position: Tween<Offset>(begin: const Offset(0, 0.06), end: Offset.zero).animate(entrada),
        child: FadeTransition(opacity: entrada, child: child),
      );
    },
  );
}

/// Entrada escalonada para ítems de lista.
///
/// El escalonado se corta en [AppMotion.staggerMax]: a partir de ahí el usuario
/// ya no lo percibe y solo siente que la lista tarda en aparecer.
class EntradaEscalonada extends StatelessWidget {
  const EntradaEscalonada({
    super.key,
    required this.indice,
    required this.child,
    this.desplazamiento = 12,
  });

  final int indice;
  final Widget child;
  final double desplazamiento;

  @override
  Widget build(BuildContext context) {
    if (AppMotion.reducido(context)) return child;

    final pasos = indice.clamp(0, AppMotion.staggerMax);
    final retardo = AppMotion.stagger * pasos;

    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: AppMotion.base + retardo,
      curve: Interval(
        // El retardo se expresa como tramo inicial de la curva en vez de con un
        // Future.delayed: así no hay timers sueltos que sobrevivan al dispose.
        retardo.inMilliseconds / (AppMotion.base + retardo).inMilliseconds,
        1,
        curve: AppMotion.entrada,
      ),
      builder: (context, t, hijo) => Opacity(
        opacity: t,
        child: Transform.translate(offset: Offset(0, (1 - t) * desplazamiento), child: hijo),
      ),
      child: child,
    );
  }
}

/// Cambio de contenido con fade + deslizamiento corto. Para el cambio de tab.
class CambioSuave extends StatelessWidget {
  const CambioSuave({super.key, required this.child, this.claveDeEstado});

  final Widget child;

  /// Qué identifica "otro contenido". Sin esto, `AnimatedSwitcher` no detecta
  /// el cambio cuando el widget es del mismo tipo (que es justo el caso al
  /// cambiar de tab).
  final Object? claveDeEstado;

  @override
  Widget build(BuildContext context) {
    return AnimatedSwitcher(
      duration: AppMotion.duracion(context, AppMotion.fast),
      switchInCurve: AppMotion.entrada,
      switchOutCurve: AppMotion.salida,
      layoutBuilder: (actual, anteriores) => Stack(
        alignment: Alignment.topCenter,
        children: [...anteriores, ?actual],
      ),
      transitionBuilder: (hijo, animacion) => FadeTransition(
        opacity: animacion,
        child: SlideTransition(
          position: Tween<Offset>(
            begin: const Offset(0, 0.012),
            end: Offset.zero,
          ).animate(animacion),
          child: hijo,
        ),
      ),
      child: KeyedSubtree(key: ValueKey(claveDeEstado), child: child),
    );
  }
}

/// Envoltorio que hunde levemente su hijo al presionarlo.
///
/// Da la realimentación táctil que a la app le faltaba en las tarjetas y los
/// botones propios (los de Material ya traen ripple).
class PresionHundida extends StatefulWidget {
  const PresionHundida({super.key, required this.child, this.onTap, this.escala});

  final Widget child;
  final VoidCallback? onTap;
  final double? escala;

  @override
  State<PresionHundida> createState() => _PresionHundidaState();
}

class _PresionHundidaState extends State<PresionHundida> {
  bool _presionado = false;

  void _set(bool v) {
    if (widget.onTap == null || _presionado == v) return;
    setState(() => _presionado = v);
  }

  @override
  Widget build(BuildContext context) {
    final escala = _presionado ? (widget.escala ?? AppMotion.escalaPresionado) : 1.0;

    return GestureDetector(
      onTap: widget.onTap,
      onTapDown: (_) => _set(true),
      onTapUp: (_) => _set(false),
      onTapCancel: () => _set(false),
      child: AnimatedScale(
        scale: escala,
        duration: AppMotion.duracion(context, AppMotion.instant),
        curve: AppMotion.tactil,
        child: widget.child,
      ),
    );
  }
}

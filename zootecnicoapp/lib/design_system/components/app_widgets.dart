/// Widgets base del design system. Todo lo demás se construye con estos.
///
/// ── Contrato de estas primitivas ─────────────────────────────────────────────
/// * **Color:** todo sale de [AppColors]. Los pocos tonos que no existen como
///   token global viven acá con nombre (`_TonoTexto`), nunca como `Color(0x…)`
///   suelto dentro de un `build`.
/// * **Métrica:** lo que cae en la escala 4pt sale de [AppSpacing]/[AppRadius];
///   el resto (alturas de control, padding de input) vive en `_Med`.
/// * **Movimiento:** toda animación pasa por `AppMotion.duracion(context, …)`,
///   así respeta "Reducir movimiento" del sistema.
/// * **Marca:** naranja = acción, verde = sólo éxito. Si ves un botón de acción
///   en verde acá, es una regresión (ver `CLAUDE.md` de la app).
///
/// Las firmas públicas son estables: las consumen todas las pantallas. Se puede
/// cambiar el interior, no los nombres ni el orden de los parámetros.
library;

import 'package:flutter/material.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Tokens locales del archivo
// ═══════════════════════════════════════════════════════════════════════════

/// Variantes **oscuras** de los semánticos, para texto e íconos sobre sus
/// fondos suaves (`warningBg`, `dangerBg`, `infoBg`).
///
/// Existen porque el token claro (`AppColors.warning`, `AppColors.info`) no
/// llega al contraste necesario sobre esos fondos: leído bajo sol, un badge
/// ámbar sobre crema ámbar desaparece.
class _TonoTexto {
  _TonoTexto._();

  static const Color sobreWarning = Color(0xFF9A7626);
  static const Color sobreDanger = Color(0xFF9A4035);
  static const Color sobreInfo = Color(0xFF3F668A);
}

/// Métricas internas de las primitivas.
///
/// Sólo valores que **no** caen en la escala 4pt de [AppSpacing] ni en
/// [AppFontSize]: alturas de control, padding de input y tamaños de texto
/// intermedios. Con nombre y en un solo lugar, en vez de repartidos como
/// números sueltos por cada `build` del archivo.
class _Med {
  _Med._();

  // Botón
  static const double altoBotonSm = 38;
  static const double altoBotonMd = 48;
  static const double altoBotonLg = 56;
  static const double padBotonSm = 14;
  static const double padBotonMd = 18;
  static const double padBotonLg = 22;
  static const double fuenteBotonSm = 13;
  static const double fuenteBotonMd = 14;
  static const double fuenteBotonLg = 15;
  static const double radioBotonLg = 16;
  static const double bordeBoton = 1.5;

  /// El ícono va apenas por encima del texto para que no se vea hundido.
  static const double saltoIcono = 3;

  // Campos
  static const double fuenteEtiqueta = 12;
  static const double fuenteAuxiliar = 10;
  static const double fuenteValor = 16;
  static const double fuenteValorGrande = 22;
  static const double gapEtiqueta = 6;

  /// Coincide con el `contentPadding` del `inputDecorationTheme`: los campos de
  /// las primitivas y los `TextField` pelados de las pantallas tienen que
  /// alinearse entre sí.
  static const double padCampoH = 14;
  static const double padCampoV = 13;
  static const double padCampoVGrande = 17;
  static const double padSexoH = 8;
  static const double padSimboloIzq = 11;
  static const double padSimboloDer = 4;
  static const double fuenteSimboloSexo = 15;
  static const double desenfoqueHalo = 10;
  static const double bordeError = 1;
  static const double bordeErrorFoco = 1.6;

  // Sección
  static const double padSeccionV = 14;
  static const double padSeccionAbajo = 18;
  static const double chipIcono = 32;
  static const double iconoSeccion = 16;
  static const double fuenteTitulo = 14;
  static const double puntoLleno = 8;
  static const double chevron = 20;

  // Badge / info / stat / chip
  static const double padBadgeH = 9;
  static const double padBadgeV = 3;
  static const double puntoBadge = 5;
  static const double padInfoH = 14;
  static const double padInfoV = 10;
  static const double fuenteInfo = 12;
  static const double alturaLineaInfo = 1.5;
  static const double padStatH = 10;
  static const double padStatV = 8;
  static const double fuenteStatEtiqueta = 9;
  static const double espaciadoStat = 0.5;
  static const double padChipH = 12;
  static const double padChipV = 7;
  static const double iconoChip = 13;
  static const double gapCorto = 5;
  static const double gapMinimo = 2;
}

// ═══════════════════════════════════════════════════════════════════════════
// AppButton
// ═══════════════════════════════════════════════════════════════════════════

enum AppButtonVariant { primary, accent, secondary, ghost, danger }

enum AppButtonSize { sm, md, lg }

/// Botón de la app.
///
/// `primary` es el CTA de la pantalla: naranja de marca con la sombra teñida,
/// que lo separa del resto sin necesidad de agrandarlo. `accent` es el dorado
/// del logo, para la acción secundaria que igual tiene que verse. Hasta
/// ago-2026 `primary` era **verde** — el patrón viejo que la regla de marca
/// prohíbe: el verde es sólo éxito.
class AppButton extends StatefulWidget {
  const AppButton({
    super.key,
    required this.label,
    this.onPressed,
    this.variant = AppButtonVariant.primary,
    this.size = AppButtonSize.md,
    this.icon,
    this.full = false,
    this.loading = false,
  });

  final String label;
  final VoidCallback? onPressed;
  final AppButtonVariant variant;
  final AppButtonSize size;
  final IconData? icon;
  final bool full;
  final bool loading;

  @override
  State<AppButton> createState() => _AppButtonState();
}

class _AppButtonState extends State<AppButton> {
  bool _presionado = false;

  void _presion(bool activo) {
    if (_presionado == activo) return;
    setState(() => _presionado = activo);
  }

  /// Fondo, texto, borde y sombra según variante y estado.
  (Color, Color, Color?, List<BoxShadow>?) _paleta(bool apagado) {
    if (apagado) {
      // Apagado ≠ invisible: el `Opacity(0.5)` que había dejaba texto blanco
      // sobre naranja lavado, ilegible al sol. Se apaga con neutros que sí se
      // leen, y el rótulo sigue diciendo qué haría el botón.
      return switch (widget.variant) {
        AppButtonVariant.secondary => (Colors.transparent, AppColors.ink500, AppColors.ink200, null),
        AppButtonVariant.ghost => (Colors.transparent, AppColors.ink500, null, null),
        _ => (AppColors.ink100, AppColors.ink500, null, null),
      };
    }
    return switch (widget.variant) {
      AppButtonVariant.primary => (AppColors.brand500, Colors.white, null, AppColors.shadowBrand),
      AppButtonVariant.accent => (AppColors.gold500, AppColors.ink900, null, null),
      AppButtonVariant.secondary => (Colors.transparent, AppColors.brand600, AppColors.brand200, null),
      AppButtonVariant.ghost => (Colors.transparent, AppColors.ink700, null, null),
      AppButtonVariant.danger => (AppColors.danger, Colors.white, null, null),
    };
  }

  (double, double, double, double) _medidas() => switch (widget.size) {
    AppButtonSize.sm => (_Med.altoBotonSm, _Med.padBotonSm, _Med.fuenteBotonSm, AppRadius.sm),
    AppButtonSize.md => (_Med.altoBotonMd, _Med.padBotonMd, _Med.fuenteBotonMd, AppRadius.md),
    AppButtonSize.lg => (_Med.altoBotonLg, _Med.padBotonLg, _Med.fuenteBotonLg, _Med.radioBotonLg),
  };

  @override
  Widget build(BuildContext context) {
    // `loading` bloquea el toque igual que un `onPressed` nulo — así el doble
    // tap no encola dos veces —, pero NO se pinta apagado: el botón sigue
    // siendo el CTA, sólo que ocupado.
    final cargando = widget.loading;
    final bloqueado = widget.onPressed == null || cargando;
    final apagado = widget.onPressed == null && !cargando;

    final (fondo, texto, borde, sombra) = _paleta(apagado);
    final (alto, padH, fuente, radio) = _medidas();
    final formaRadio = BorderRadius.circular(radio);
    final tamIcono = fuente + _Med.saltoIcono;

    final Widget? guia = cargando
        ? SizedBox(
            width: tamIcono,
            height: tamIcono,
            child: CircularProgressIndicator(
              strokeWidth: 2,
              color: texto,
              semanticsLabel: 'Cargando',
            ),
          )
        : widget.icon != null
        ? Icon(widget.icon, size: tamIcono, color: texto)
        : null;

    return AnimatedScale(
      // Micro-reacción táctil: el botón se hunde mientras el dedo está encima.
      // Con guantes el ripple casi no se ve; el cambio de tamaño sí.
      //
      // El estado sale de `onHighlightChanged` del propio InkWell y no de un
      // `GestureDetector` envolvente: dos reconocedores de tap anidados
      // compiten en la arena de gestos, y el de afuera termina disparando
      // `onTapDown` tarde para después cancelar — el hundido parpadea.
      scale: _presionado ? AppMotion.escalaPresionado : 1,
      duration: AppMotion.duracion(context, AppMotion.instant),
      curve: AppMotion.tactil,
      child: AnimatedContainer(
        duration: AppMotion.duracion(context, AppMotion.fast),
        curve: AppMotion.tactil,
        height: alto,
        width: widget.full ? double.infinity : null,
        decoration: BoxDecoration(
          color: fondo,
          borderRadius: formaRadio,
          border: borde != null ? Border.all(color: borde, width: _Med.bordeBoton) : null,
          // Al presionar se apaga el brillo: refuerza la sensación de hundido.
          boxShadow: _presionado ? null : sombra,
        ),
        child: Material(
          color: Colors.transparent,
          borderRadius: formaRadio,
          child: InkWell(
            onTap: bloqueado ? null : widget.onPressed,
            onHighlightChanged: bloqueado ? null : _presion,
            borderRadius: formaRadio,
            splashColor: texto.withValues(alpha: 0.10),
            highlightColor: texto.withValues(alpha: 0.06),
            child: Padding(
              padding: EdgeInsets.symmetric(horizontal: padH),
              child: Row(
                mainAxisSize: widget.full ? MainAxisSize.max : MainAxisSize.min,
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  // El hueco del ícono se abre y se cierra animado: si no, el
                  // rótulo pega un salto lateral al empezar a cargar.
                  AnimatedSize(
                    duration: AppMotion.duracion(context, AppMotion.fast),
                    curve: AppMotion.tactil,
                    child: guia == null
                        ? const SizedBox.shrink()
                        : Padding(
                            padding: const EdgeInsets.only(right: AppSpacing.s2),
                            child: AnimatedSwitcher(
                              duration: AppMotion.duracion(context, AppMotion.fast),
                              child: KeyedSubtree(key: ValueKey(cargando), child: guia),
                            ),
                          ),
                  ),
                  Flexible(
                    child: AnimatedDefaultTextStyle(
                      duration: AppMotion.duracion(context, AppMotion.fast),
                      style: TextStyle(
                        fontFamily: 'Inter',
                        fontSize: fuente,
                        fontWeight: FontWeight.w700,
                        color: texto,
                      ),
                      child: Text(
                        widget.label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppBadge
// ═══════════════════════════════════════════════════════════════════════════

enum BadgeTone { success, warning, danger, info, neutral, orange }

class AppBadge extends StatelessWidget {
  const AppBadge({super.key, required this.label, this.tone = BadgeTone.success, this.dot = false});

  final String label;
  final BadgeTone tone;
  final bool dot;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (tone) {
      BadgeTone.success => (AppColors.successBg, AppColors.green600),
      BadgeTone.warning => (AppColors.warningBg, _TonoTexto.sobreWarning),
      BadgeTone.danger => (AppColors.dangerBg, _TonoTexto.sobreDanger),
      BadgeTone.info => (AppColors.infoBg, _TonoTexto.sobreInfo),
      BadgeTone.neutral => (AppColors.cream2, AppColors.ink700),
      BadgeTone.orange => (AppColors.brand100, AppColors.brand600),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: _Med.padBadgeH, vertical: _Med.padBadgeV),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.pill)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (dot) ...[
            Container(
              width: _Med.puntoBadge,
              height: _Med.puntoBadge,
              decoration: BoxDecoration(color: fg, shape: BoxShape.circle),
            ),
            const SizedBox(width: _Med.gapCorto),
          ],
          Text(
            label,
            style: TextStyle(
              fontFamily: 'Inter',
              fontSize: AppFontSize.xs,
              fontWeight: FontWeight.w600,
              color: fg,
            ),
          ),
        ],
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppField — input con etiqueta, unidad y foco naranja
// ═══════════════════════════════════════════════════════════════════════════

class AppField extends StatelessWidget {
  const AppField({
    super.key,
    this.label,
    this.controller,
    this.onChanged,
    this.suffix,
    this.hint,
    this.placeholder,
    this.required = false,
    this.large = false,
    this.readOnly = false,
    this.keyboardType,
    this.maxLines = 1,
    this.errorText,
  });

  final String? label;
  final TextEditingController? controller;
  final ValueChanged<String>? onChanged;

  /// Unidad: kg, %, aves, g…
  final String? suffix;

  /// Texto auxiliar a la derecha de la etiqueta (metas, saldos disponibles).
  final String? hint;
  final String? placeholder;
  final bool required;
  final bool large;
  final bool readOnly;
  final TextInputType? keyboardType;
  final int maxLines;

  /// Mensaje de validación. Entra animado debajo del campo y empuja el layout
  /// de forma suave; `null` o vacío = sin error.
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final hayError = errorText != null && errorText!.isNotEmpty;

    return _CampoEnfocable(
      constructor: (context, foco, enfocado) {
        // Un campo de sólo lectura recibe foco al tocarlo pero no se escribe:
        // no tiene por qué encenderse como si el operario estuviera cargando.
        final activo = enfocado && !readOnly;

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (label != null) ...[
              _EtiquetaCampo(
                texto: label!,
                hint: hint,
                requerido: required,
                activo: activo,
                error: hayError,
              ),
              const SizedBox(height: _Med.gapEtiqueta),
            ],
            _Halo(
              activo: activo,
              child: TextField(
                controller: controller,
                focusNode: foco,
                onChanged: onChanged,
                readOnly: readOnly,
                keyboardType: keyboardType,
                maxLines: maxLines,
                style: TextStyle(
                  fontFamily: 'PlusJakartaSans',
                  fontSize: large ? _Med.fuenteValorGrande : _Med.fuenteValor,
                  fontWeight: FontWeight.w600,
                  color: AppColors.ink900,
                  fontFeatures: const [FontFeature.tabularFigures()],
                ),
                decoration: InputDecoration(
                  hintText: placeholder,
                  fillColor: readOnly ? AppColors.cream2 : AppColors.surface,
                  suffixText: suffix,
                  suffixStyle: const TextStyle(
                    fontFamily: 'Inter',
                    fontSize: _Med.fuenteEtiqueta,
                    fontWeight: FontWeight.w600,
                    color: AppColors.ink500,
                  ),
                  contentPadding: EdgeInsets.symmetric(
                    horizontal: _Med.padCampoH,
                    vertical: large ? _Med.padCampoVGrande : _Med.padCampoV,
                  ),
                  // El error se dibuja abajo y no dentro del `InputDecorator`:
                  // así el alto del campo no cambia y la fila vecina no salta.
                  enabledBorder: hayError ? _bordeError(_Med.bordeError) : null,
                  focusedBorder: hayError ? _bordeError(_Med.bordeErrorFoco) : null,
                ),
              ),
            ),
            _MensajeError(texto: hayError ? errorText : null),
          ],
        );
      },
    );
  }

  static OutlineInputBorder _bordeError(double ancho) => OutlineInputBorder(
    borderRadius: BorderRadius.circular(AppRadius.md),
    borderSide: BorderSide(color: AppColors.danger, width: ancho),
  );
}

// ═══════════════════════════════════════════════════════════════════════════
// AppPairField — par ♀/♂. El patrón central de todo el seguimiento diario.
// ═══════════════════════════════════════════════════════════════════════════

class AppPairField extends StatelessWidget {
  const AppPairField({
    super.key,
    required this.label,
    this.hController,
    this.mController,
    this.onHChanged,
    this.onMChanged,
    this.suffix,
    this.hint,
    this.required = false,
  });

  final String label;
  final TextEditingController? hController;
  final TextEditingController? mController;
  final ValueChanged<String>? onHChanged;
  final ValueChanged<String>? onMChanged;
  final String? suffix;
  final String? hint;
  final bool required;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _EtiquetaCampo(texto: label, hint: hint, requerido: required),
        const SizedBox(height: _Med.gapEtiqueta),
        Row(
          children: [
            Expanded(
              child: _SexInput(
                controller: hController,
                onChanged: onHChanged,
                symbol: '♀',
                color: AppColors.hembra,
                suffix: suffix,
              ),
            ),
            const SizedBox(width: AppSpacing.s2),
            Expanded(
              child: _SexInput(
                controller: mController,
                onChanged: onMChanged,
                symbol: '♂',
                color: AppColors.macho,
                suffix: suffix,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _SexInput extends StatelessWidget {
  const _SexInput({this.controller, this.onChanged, required this.symbol, required this.color, this.suffix});

  final TextEditingController? controller;
  final ValueChanged<String>? onChanged;
  final String symbol;
  final Color color;
  final String? suffix;

  @override
  Widget build(BuildContext context) {
    return _CampoEnfocable(
      constructor: (context, foco, enfocado) => _Halo(
        activo: enfocado,
        child: TextField(
          controller: controller,
          focusNode: foco,
          onChanged: onChanged,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          style: TextStyle(
            fontFamily: 'PlusJakartaSans',
            fontSize: _Med.fuenteValor,
            fontWeight: FontWeight.w700,
            color: AppColors.ink900,
            fontFeatures: const [FontFeature.tabularFigures()],
          ),
          decoration: InputDecoration(
            hintText: '0',
            prefixIcon: Padding(
              padding: const EdgeInsets.only(left: _Med.padSimboloIzq, right: _Med.padSimboloDer),
              child: Text(
                symbol,
                style: TextStyle(
                  fontSize: _Med.fuenteSimboloSexo,
                  fontWeight: FontWeight.w700,
                  color: color,
                ),
              ),
            ),
            prefixIconConstraints: const BoxConstraints(minWidth: 0, minHeight: 0),
            suffixText: suffix,
            suffixStyle: const TextStyle(
              fontFamily: 'Inter',
              fontSize: _Med.fuenteAuxiliar,
              fontWeight: FontWeight.w600,
              color: AppColors.ink500,
            ),
            contentPadding: const EdgeInsets.symmetric(
              horizontal: _Med.padSexoH,
              vertical: _Med.padCampoV,
            ),
          ),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppSection — acordeón. Los formularios largos se dividen en estas.
// ═══════════════════════════════════════════════════════════════════════════

class AppSection extends StatefulWidget {
  const AppSection({
    super.key,
    required this.title,
    required this.children,
    this.icon,
    this.expanded = false,
    this.onToggle,
    this.filled = false,
    this.trailing,
  });

  final String title;
  final List<Widget> children;
  final IconData? icon;
  final bool expanded;
  final VoidCallback? onToggle;

  /// Punto verde que indica que la sección ya tiene datos.
  final bool filled;
  final Widget? trailing;

  @override
  State<AppSection> createState() => _AppSectionState();
}

class _AppSectionState extends State<AppSection> with SingleTickerProviderStateMixin {
  late final AnimationController _control;
  late final CurvedAnimation _expansion;
  late final CurvedAnimation _aparicion;
  late final Animation<double> _giro;

  @override
  void initState() {
    super.initState();
    _control = AnimationController(
      vsync: this,
      duration: AppMotion.base,
      value: widget.expanded ? 1 : 0,
    );
    _expansion = CurvedAnimation(parent: _control, curve: AppMotion.simetrica);
    // El contenido aparece con la caja ya abriéndose: entrar los dos juntos se
    // lee como un parpadeo, y entrar recién al final se siente lento.
    _aparicion = CurvedAnimation(
      parent: _control,
      curve: const Interval(0.25, 1, curve: AppMotion.entrada),
    );
    _giro = Tween<double>(begin: 0, end: 0.5).animate(_expansion);
    _control.addStatusListener(_alCambiarEstado);
  }

  /// Al terminar de cerrarse soltamos el contenido: un formulario de
  /// seguimiento tiene ocho secciones y no tiene sentido mantener montados los
  /// campos de las que están cerradas.
  void _alCambiarEstado(AnimationStatus estado) {
    if (estado == AnimationStatus.dismissed && mounted) setState(() {});
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // La duración depende de "Reducir movimiento", que es un dato de MediaQuery.
    _control.duration = AppMotion.duracion(context, AppMotion.base);
  }

  @override
  void didUpdateWidget(covariant AppSection anterior) {
    super.didUpdateWidget(anterior);
    if (widget.expanded != anterior.expanded) {
      if (widget.expanded) {
        _control.forward();
      } else {
        _control.reverse();
      }
    }
  }

  @override
  void dispose() {
    _control.removeStatusListener(_alCambiarEstado);
    _expansion.dispose();
    _aparicion.dispose();
    _control.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final abierta = widget.expanded;
    final montado = _control.status != AnimationStatus.dismissed;

    return AnimatedContainer(
      duration: AppMotion.duracion(context, AppMotion.base),
      curve: AppMotion.simetrica,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        // La sección abierta es donde el operario está trabajando: se marca con
        // el naranja de foco y un poco más de elevación.
        border: Border.all(color: abierta ? AppColors.brand200 : AppColors.line),
        boxShadow: abierta ? AppColors.shadowMd : AppColors.shadowSm,
      ),
      child: Column(
        children: [
          InkWell(
            onTap: widget.onToggle,
            borderRadius: BorderRadius.vertical(
              top: const Radius.circular(AppRadius.lg),
              bottom: Radius.circular(abierta ? 0 : AppRadius.lg),
            ),
            child: Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.s4,
                vertical: _Med.padSeccionV,
              ),
              child: Row(
                children: [
                  if (widget.icon != null) ...[
                    _ChipIcono(icono: widget.icon!, progreso: _expansion),
                    const SizedBox(width: AppSpacing.s3),
                  ],
                  Expanded(
                    child: Text(
                      widget.title,
                      style: const TextStyle(
                        fontFamily: 'PlusJakartaSans',
                        fontSize: _Med.fuenteTitulo,
                        fontWeight: FontWeight.w700,
                        color: AppColors.ink900,
                      ),
                    ),
                  ),
                  if (widget.trailing != null) widget.trailing!,
                  if (widget.filled) ...[
                    const SizedBox(width: AppSpacing.s2),
                    // Verde = la sección ya tiene datos. Es un estado de éxito,
                    // no una acción: acá el verde sí corresponde.
                    const _EntradaConfirmacion(
                      child: SizedBox(
                        width: _Med.puntoLleno,
                        height: _Med.puntoLleno,
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            color: AppColors.green500,
                            shape: BoxShape.circle,
                          ),
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(width: AppSpacing.s2),
                  RotationTransition(
                    turns: _giro,
                    child: const Icon(
                      Icons.keyboard_arrow_down_rounded,
                      size: _Med.chevron,
                      color: AppColors.ink300,
                    ),
                  ),
                ],
              ),
            ),
          ),
          // El contenido se revela con la altura en vez de aparecer de golpe.
          // `SizeTransition` ya recorta con un `ClipRect` propio, que acá es
          // imprescindible: sin recorte, el contenido a media apertura se
          // dibuja fuera de la tarjeta y se ve por encima del borde.
          SizeTransition(
            sizeFactor: _expansion,
            alignment: AlignmentDirectional.topStart,
            child: FadeTransition(
              opacity: _aparicion,
              child: montado
                  ? Column(
                      children: [
                        Divider(height: 1, color: AppColors.line),
                        Padding(
                          padding: const EdgeInsets.fromLTRB(
                            AppSpacing.s4,
                            AppSpacing.s3,
                            AppSpacing.s4,
                            _Med.padSeccionAbajo,
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              for (int i = 0; i < widget.children.length; i++) ...[
                                if (i > 0) const SizedBox(height: AppSpacing.s3),
                                widget.children[i],
                              ],
                            ],
                          ),
                        ),
                      ],
                    )
                  : const SizedBox(width: double.infinity),
            ),
          ),
        ],
      ),
    );
  }
}

/// Cuadrito del ícono de la sección. Se tiñe de naranja a medida que la sección
/// se abre, en sincronía exacta con la expansión (la misma animación, no una
/// duración aparte que se desfase).
class _ChipIcono extends StatelessWidget {
  const _ChipIcono({required this.icono, required this.progreso});

  final IconData icono;
  final Animation<double> progreso;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: progreso,
      builder: (context, _) {
        final t = progreso.value;
        return Container(
          width: _Med.chipIcono,
          height: _Med.chipIcono,
          decoration: BoxDecoration(
            color: Color.lerp(AppColors.cream2, AppColors.brandTint, t),
            borderRadius: BorderRadius.circular(AppRadius.sm),
          ),
          child: Icon(
            icono,
            size: _Med.iconoSeccion,
            color: Color.lerp(AppColors.ink700, AppColors.brand700, t),
          ),
        );
      },
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppInfoBox
// ═══════════════════════════════════════════════════════════════════════════

enum InfoTone { info, warn, success }

class AppInfoBox extends StatelessWidget {
  const AppInfoBox({super.key, required this.text, this.tone = InfoTone.info});

  final String text;
  final InfoTone tone;

  @override
  Widget build(BuildContext context) {
    // El ícono acompaña al tono. Ninguno es un triángulo de alarma: un aviso
    // acá es contexto de trabajo (falta un dato, hoy toca pesar), no una falla
    // del equipo.
    final (bg, fg, icono) = switch (tone) {
      InfoTone.info => (AppColors.infoBg, _TonoTexto.sobreInfo, Icons.info_outline_rounded),
      InfoTone.warn => (AppColors.warningBg, _TonoTexto.sobreWarning, Icons.error_outline_rounded),
      InfoTone.success => (AppColors.successBg, AppColors.green700, Icons.check_circle_outline_rounded),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: _Med.padInfoH, vertical: _Med.padInfoV),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.sm)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icono, size: _Med.iconoSeccion, color: fg),
          const SizedBox(width: AppSpacing.s2),
          Expanded(
            child: Text(
              text,
              style: TextStyle(
                fontFamily: 'Inter',
                fontSize: _Med.fuenteInfo,
                height: _Med.alturaLineaInfo,
                color: fg,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppStatTile — celda de métrica con cifra tabular
// ═══════════════════════════════════════════════════════════════════════════

class AppStatTile extends StatelessWidget {
  const AppStatTile({super.key, required this.label, required this.value, this.color, this.background});

  final String label;
  final String value;
  final Color? color;
  final Color? background;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: _Med.padStatH, vertical: _Med.padStatV),
      decoration: BoxDecoration(
        color: background ?? AppColors.cream,
        borderRadius: BorderRadius.circular(AppRadius.sm),
        // Hairline: sobre una tarjeta crema el relleno solo no separa la celda
        // del fondo cuando la pantalla está bajo sol directo.
        border: Border.all(color: AppColors.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontFamily: 'Inter',
              fontSize: _Med.fuenteStatEtiqueta,
              fontWeight: FontWeight.w700,
              letterSpacing: _Med.espaciadoStat,
              color: AppColors.ink500,
            ),
          ),
          const SizedBox(height: _Med.gapMinimo),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontFamily: 'PlusJakartaSans',
              fontSize: AppFontSize.base,
              fontWeight: FontWeight.w700,
              color: color ?? AppColors.ink900,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
        ],
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppSavedChip — confirmación optimista tras guardar
// ═══════════════════════════════════════════════════════════════════════════

class AppSavedChip extends StatelessWidget {
  const AppSavedChip({super.key, this.label = 'Guardado aquí'});

  final String label;

  @override
  Widget build(BuildContext context) {
    return _EntradaConfirmacion(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: _Med.padChipH, vertical: _Med.padChipV),
        decoration: BoxDecoration(
          color: AppColors.successBg,
          borderRadius: BorderRadius.circular(AppRadius.pill),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.check_rounded, size: _Med.iconoChip, color: AppColors.green600),
            const SizedBox(width: _Med.gapCorto),
            Text(
              label,
              style: const TextStyle(
                fontFamily: 'Inter',
                fontSize: _Med.fuenteEtiqueta,
                fontWeight: FontWeight.w600,
                color: AppColors.green600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Piezas internas compartidas
// ═══════════════════════════════════════════════════════════════════════════

/// Entrada de una confirmación: escala corta con rebote + fade.
///
/// Es el gesto de "listo, quedó anotado". Se reserva para lo que confirma algo
/// (el chip de guardado, el punto de sección completa); usada como decoración
/// pierde el significado.
class _EntradaConfirmacion extends StatelessWidget {
  const _EntradaConfirmacion({required this.child});

  /// De cuánto arranca la escala. Corta a propósito: más chica se lee como un
  /// elemento que "aterriza" y distrae de lo que el operario estaba mirando.
  static const double _escalaInicial = 0.8;

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (AppMotion.reducido(context)) return child;

    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0.0, end: 1.0),
      duration: AppMotion.duracion(context, AppMotion.base),
      curve: AppMotion.confirmacion,
      builder: (context, t, hijo) => Opacity(
        // `confirmacion` es un easeOutBack: se pasa de 1. La opacidad hay que
        // recortarla o Flutter lanza en debug por el valor fuera de rango.
        opacity: t.clamp(0.0, 1.0),
        child: Transform.scale(scale: _escalaInicial + (1 - _escalaInicial) * t, child: hijo),
      ),
      child: child,
    );
  }
}

typedef _ConstructorCampo = Widget Function(BuildContext context, FocusNode foco, bool enfocado);

/// Provee un [FocusNode] propio y avisa cuándo el campo está enfocado.
///
/// Vive acá para que el halo y la etiqueta reaccionen al foco sin que cada
/// pantalla tenga que manejar nodos: ninguna primitiva expone `focusNode`.
class _CampoEnfocable extends StatefulWidget {
  const _CampoEnfocable({required this.constructor});

  final _ConstructorCampo constructor;

  @override
  State<_CampoEnfocable> createState() => _CampoEnfocableState();
}

class _CampoEnfocableState extends State<_CampoEnfocable> {
  final FocusNode _foco = FocusNode();
  bool _enfocado = false;

  @override
  void initState() {
    super.initState();
    _foco.addListener(_sincronizar);
  }

  void _sincronizar() {
    if (_foco.hasFocus == _enfocado) return;
    setState(() => _enfocado = _foco.hasFocus);
  }

  @override
  void dispose() {
    _foco.removeListener(_sincronizar);
    _foco.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.constructor(context, _foco, _enfocado);
}

/// Halo naranja de foco.
///
/// Va por fuera del borde a propósito: si el resalte se hiciera engrosando el
/// `BorderSide`, el campo cambiaría de alto al enfocarse y arrastraría a toda
/// la columna. Como sombra, el layout no se mueve ni un píxel.
class _Halo extends StatelessWidget {
  const _Halo({required this.activo, required this.child});

  final bool activo;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: AppMotion.duracion(context, AppMotion.fast),
      curve: AppMotion.tactil,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.md),
        boxShadow: activo
            ? [BoxShadow(color: AppColors.brandTint, blurRadius: _Med.desenfoqueHalo, spreadRadius: 1)]
            : const <BoxShadow>[],
      ),
      child: child,
    );
  }
}

/// Etiqueta de campo: título + asterisco de obligatorio + auxiliar a la derecha.
/// Compartida por [AppField] y [AppPairField] — antes estaba duplicada.
class _EtiquetaCampo extends StatelessWidget {
  const _EtiquetaCampo({
    required this.texto,
    this.hint,
    this.requerido = false,
    this.activo = false,
    this.error = false,
  });

  final String texto;
  final String? hint;
  final bool requerido;
  final bool activo;
  final bool error;

  @override
  Widget build(BuildContext context) {
    final color = error
        ? AppColors.danger
        : activo
        ? AppColors.brand600
        : AppColors.ink700;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Flexible(
          child: AnimatedDefaultTextStyle(
            duration: AppMotion.duracion(context, AppMotion.fast),
            style: TextStyle(
              fontFamily: 'Inter',
              fontSize: _Med.fuenteEtiqueta,
              fontWeight: FontWeight.w600,
              color: color,
            ),
            child: Text.rich(
              TextSpan(
                children: [
                  TextSpan(text: texto),
                  if (requerido)
                    TextSpan(
                      text: ' *',
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: error ? AppColors.danger : AppColors.brand500,
                      ),
                    ),
                ],
              ),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ),
        if (hint != null)
          Text(
            hint!,
            style: const TextStyle(
              fontFamily: 'Inter',
              fontSize: _Med.fuenteAuxiliar,
              color: AppColors.ink500,
            ),
          ),
      ],
    );
  }
}

/// Mensaje de error bajo un campo.
///
/// Dos animaciones distintas y a propósito: [AnimatedSize] hace que el hueco se
/// abra empujando suave lo de abajo, y el texto entra con un fade corto.
/// Apareciendo de golpe, el operario pierde de vista dónde estaba.
class _MensajeError extends StatelessWidget {
  const _MensajeError({required this.texto});

  final String? texto;

  @override
  Widget build(BuildContext context) {
    return AnimatedSize(
      duration: AppMotion.duracion(context, AppMotion.fast),
      curve: AppMotion.simetrica,
      alignment: Alignment.topLeft,
      child: texto == null
          ? const SizedBox(width: double.infinity)
          : Padding(
              padding: const EdgeInsets.only(top: AppSpacing.s1, left: AppSpacing.s1),
              child: _EntradaSuave(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.error_outline_rounded,
                      size: _Med.fuenteEtiqueta,
                      color: AppColors.danger,
                    ),
                    const SizedBox(width: AppSpacing.s1),
                    Expanded(
                      child: Text(
                        texto!,
                        style: const TextStyle(
                          fontFamily: 'Inter',
                          fontSize: _Med.fuenteAuxiliar,
                          fontWeight: FontWeight.w600,
                          color: AppColors.danger,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }
}

/// Fade + desplazamiento corto al montarse. Para contenido que aparece dentro
/// de una pantalla que ya está a la vista.
class _EntradaSuave extends StatelessWidget {
  const _EntradaSuave({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (AppMotion.reducido(context)) return child;

    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0.0, end: 1.0),
      duration: AppMotion.duracion(context, AppMotion.fast),
      curve: AppMotion.entrada,
      builder: (context, t, hijo) => Opacity(
        opacity: t,
        child: Transform.translate(offset: Offset(0, (1 - t) * AppSpacing.s1), child: hijo),
      ),
      child: child,
    );
  }
}

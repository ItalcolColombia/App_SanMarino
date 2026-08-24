/// Widgets base del design system. Todo lo demás se construye con estos.
library;

import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

// ═══════════════════════════════════════════════════════════════════════════
// AppButton
// ═══════════════════════════════════════════════════════════════════════════

enum AppButtonVariant { primary, accent, secondary, ghost, danger }
enum AppButtonSize { sm, md, lg }

class AppButton extends StatelessWidget {
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
  Widget build(BuildContext context) {
    final (bg, fg, border) = switch (variant) {
      AppButtonVariant.primary   => (AppColors.green500, Colors.white, null),
      AppButtonVariant.accent    => (AppColors.orange500, Colors.white, null),
      AppButtonVariant.secondary => (Colors.transparent, AppColors.green600, AppColors.green200),
      AppButtonVariant.ghost     => (Colors.transparent, AppColors.ink700, null),
      AppButtonVariant.danger    => (AppColors.danger, Colors.white, null),
    };

    final (height, hPad, fontSize, radius) = switch (size) {
      AppButtonSize.sm => (38.0, 14.0, 13.0, AppRadius.sm),
      AppButtonSize.md => (48.0, 18.0, 14.0, AppRadius.md),
      AppButtonSize.lg => (56.0, 22.0, 15.0, 16.0),
    };

    final disabled = onPressed == null || loading;

    return Opacity(
      opacity: disabled ? 0.5 : 1,
      child: Material(
        color: bg,
        borderRadius: BorderRadius.circular(radius),
        child: InkWell(
          onTap: disabled ? null : onPressed,
          borderRadius: BorderRadius.circular(radius),
          child: Container(
            height: height,
            width: full ? double.infinity : null,
            padding: EdgeInsets.symmetric(horizontal: hPad),
            decoration: border != null
                ? BoxDecoration(
                    borderRadius: BorderRadius.circular(radius),
                    border: Border.all(color: border, width: 1.5),
                  )
                : null,
            alignment: Alignment.center,
            child: Row(
              mainAxisSize: full ? MainAxisSize.max : MainAxisSize.min,
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                if (loading)
                  SizedBox(
                    width: 16, height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2, color: fg),
                  )
                else if (icon != null)
                  Icon(icon, size: fontSize + 3, color: fg),
                if (loading || icon != null) const SizedBox(width: AppSpacing.s2),
                Text(label, style: TextStyle(
                  fontFamily: 'Inter', fontSize: fontSize,
                  fontWeight: FontWeight.w700, color: fg,
                )),
              ],
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
      BadgeTone.warning => (AppColors.warningBg, const Color(0xFF9A7626)),
      BadgeTone.danger  => (AppColors.dangerBg, const Color(0xFF9A4035)),
      BadgeTone.info    => (AppColors.infoBg, const Color(0xFF3F668A)),
      BadgeTone.neutral => (AppColors.cream2, AppColors.ink700),
      BadgeTone.orange  => (AppColors.orange100, AppColors.orange600),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 3),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.pill)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (dot) ...[
            Container(width: 5, height: 5, decoration: BoxDecoration(color: fg, shape: BoxShape.circle)),
            const SizedBox(width: 5),
          ],
          Text(label, style: TextStyle(
            fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600, color: fg,
          )),
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

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (label != null) ...[
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(children: [
                Text(label!, style: const TextStyle(
                  fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.ink700,
                )),
                if (required) const Text(' *', style: TextStyle(
                  fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.orange500,
                )),
              ]),
              if (hint != null) Text(hint!, style: const TextStyle(
                fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500,
              )),
            ],
          ),
          const SizedBox(height: 6),
        ],
        TextField(
          controller: controller,
          onChanged: onChanged,
          readOnly: readOnly,
          keyboardType: keyboardType,
          maxLines: maxLines,
          style: TextStyle(
            fontFamily: 'PlusJakartaSans',
            fontSize: large ? 22 : 16,
            fontWeight: FontWeight.w600,
            color: AppColors.ink900,
            fontFeatures: const [FontFeature.tabularFigures()],
          ),
          decoration: InputDecoration(
            hintText: placeholder,
            fillColor: readOnly ? AppColors.cream2 : AppColors.surface,
            suffixText: suffix,
            suffixStyle: const TextStyle(
              fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.ink500,
            ),
            contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: large ? 17 : 13),
          ),
        ),
      ],
    );
  }
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
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Row(children: [
              Text(label, style: const TextStyle(
                fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.ink700,
              )),
              if (required) const Text(' *', style: TextStyle(
                fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.orange500,
              )),
            ]),
            if (hint != null) Text(hint!, style: const TextStyle(
              fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500,
            )),
          ],
        ),
        const SizedBox(height: 6),
        Row(children: [
          Expanded(child: _SexInput(
            controller: hController, onChanged: onHChanged,
            symbol: '♀', color: AppColors.hembra, suffix: suffix,
          )),
          const SizedBox(width: AppSpacing.s2),
          Expanded(child: _SexInput(
            controller: mController, onChanged: onMChanged,
            symbol: '♂', color: AppColors.macho, suffix: suffix,
          )),
        ]),
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
    return TextField(
      controller: controller,
      onChanged: onChanged,
      keyboardType: const TextInputType.numberWithOptions(decimal: true),
      style: TextStyle(
        fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700,
        color: AppColors.ink900, fontFeatures: const [FontFeature.tabularFigures()],
      ),
      decoration: InputDecoration(
        hintText: '0',
        prefixIcon: Padding(
          padding: const EdgeInsets.only(left: 11, right: 4),
          child: Text(symbol, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: color)),
        ),
        prefixIconConstraints: const BoxConstraints(minWidth: 0, minHeight: 0),
        suffixText: suffix,
        suffixStyle: const TextStyle(fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w600, color: AppColors.ink500),
        contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 13),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AppSection — acordeón. Los formularios largos se dividen en estas.
// ═══════════════════════════════════════════════════════════════════════════

class AppSection extends StatelessWidget {
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
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.shadowSm,
      ),
      child: Column(children: [
        InkWell(
          onTap: onToggle,
          borderRadius: BorderRadius.circular(AppRadius.lg),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: 14),
            child: Row(children: [
              if (icon != null) ...[
                Container(
                  width: 32, height: 32,
                  decoration: BoxDecoration(color: AppColors.cream2, borderRadius: BorderRadius.circular(AppRadius.sm)),
                  child: Icon(icon, size: 16, color: AppColors.ink700),
                ),
                const SizedBox(width: AppSpacing.s3),
              ],
              Expanded(child: Text(title, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 14, fontWeight: FontWeight.w700, color: AppColors.ink900,
              ))),
              if (trailing != null) trailing!,
              if (filled) ...[
                const SizedBox(width: AppSpacing.s2),
                Container(width: 8, height: 8, decoration: const BoxDecoration(color: AppColors.green500, shape: BoxShape.circle)),
              ],
              const SizedBox(width: AppSpacing.s2),
              AnimatedRotation(
                turns: expanded ? 0.5 : 0,
                duration: AppDuration.base,
                child: const Icon(Icons.keyboard_arrow_down_rounded, size: 20, color: AppColors.ink300),
              ),
            ]),
          ),
        ),
        if (expanded) ...[
          Divider(height: 1, color: AppColors.line),
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (int i = 0; i < children.length; i++) ...[
                  if (i > 0) const SizedBox(height: AppSpacing.s3),
                  children[i],
                ],
              ],
            ),
          ),
        ],
      ]),
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
    final (bg, fg) = switch (tone) {
      InfoTone.info    => (AppColors.infoBg, const Color(0xFF3F668A)),
      InfoTone.warn    => (AppColors.warningBg, const Color(0xFF9A7626)),
      InfoTone.success => (AppColors.successBg, AppColors.green700),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.sm)),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(Icons.info_outline_rounded, size: 16, color: fg),
        const SizedBox(width: AppSpacing.s2),
        Expanded(child: Text(text, style: TextStyle(
          fontFamily: 'Inter', fontSize: 12, height: 1.5, color: fg,
        ))),
      ]),
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
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: background ?? AppColors.cream,
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label.toUpperCase(), style: const TextStyle(
          fontFamily: 'Inter', fontSize: 9, fontWeight: FontWeight.w700,
          letterSpacing: 0.5, color: AppColors.ink500,
        )),
        const SizedBox(height: 2),
        Text(value, style: TextStyle(
          fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w700,
          color: color ?? AppColors.ink900,
          fontFeatures: const [FontFeature.tabularFigures()],
        )),
      ]),
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
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
      decoration: BoxDecoration(color: AppColors.successBg, borderRadius: BorderRadius.circular(AppRadius.pill)),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.check_rounded, size: 13, color: AppColors.green600),
        const SizedBox(width: 5),
        Text(label, style: const TextStyle(
          fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.green600,
        )),
      ]),
    );
  }
}

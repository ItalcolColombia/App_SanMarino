/// Selector de ítems de inventario para el bloque de alimento — F5.2 del plan
/// `descuento_inventario_movil_plan.md`.
///
/// Reemplaza el campo de texto libre `tipoAlimento` + el consumo escalar
/// **sólo** cuando `Usuario.descuentaInventarioDesdeMovil` está encendido
/// (F5.1). El resultado se convierte en el array `itemsHembras`/`itemsMachos`
/// del payload con `ItemsConsumo.armar()` — la lógica de qué id mandar y cómo
/// convertir unidades vive ahí, este widget sólo captura la elección del
/// operario.
library;

import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import '../core/models_inventario.dart';
import 'app_widgets.dart';

/// Una sección Hembras/Machos/Lote del selector: la lista de líneas elegidas
/// + el botón para agregar una más.
class SelectorItemsInventario extends StatefulWidget {
  const SelectorItemsInventario({
    super.key,
    required this.lineas,
    required this.catalogo,
    required this.existencias,
    required this.acento,
    required this.onChanged,
    this.farmId,
    this.nucleoId,
    this.galponId,
  });

  final List<LineaConsumo> lineas;
  final List<ItemInventario> catalogo;
  final Map<String, ExistenciaInventario> existencias;
  final Color acento;
  final VoidCallback onChanged;

  /// Ubicación del lote, para mostrar el disponible. Puede faltar (lote sin
  /// granja resuelta en caché) — sin ella el selector igual funciona, sólo no
  /// muestra el saldo.
  final int? farmId;
  final String? nucleoId;
  final String? galponId;

  double? _disponibleDe(ItemInventario item) {
    if (farmId == null) return null;
    final clave = ExistenciaInventario.claveDe(
      farmId: farmId!, itemId: item.id, nucleoId: nucleoId, galponId: galponId,
    );
    return existencias[clave]?.disponible;
  }

  @override
  State<SelectorItemsInventario> createState() => _SelectorItemsInventarioState();
}

class _SelectorItemsInventarioState extends State<SelectorItemsInventario> {
  @override
  Widget build(BuildContext context) {
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      for (int i = 0; i < widget.lineas.length; i++) ...[
        if (i > 0) const SizedBox(height: AppSpacing.s2),
        _FilaItem(
          linea: widget.lineas[i],
          disponible: widget._disponibleDe(widget.lineas[i].item),
          acento: widget.acento,
          onCantidadCambio: (v) => setState(() { widget.lineas[i].cantidad = v; widget.onChanged(); }),
          onQuitar: () => setState(() { widget.lineas.removeAt(i); widget.onChanged(); }),
        ),
      ],
      if (widget.lineas.isNotEmpty) const SizedBox(height: AppSpacing.s2),
      OutlinedButton.icon(
        onPressed: widget.catalogo.isEmpty ? null : _agregar,
        icon: const Icon(Icons.add_rounded, size: 16),
        label: Text(
          widget.catalogo.isEmpty ? 'Sin catálogo disponible sin conexión' : 'Agregar ítem',
          style: const TextStyle(fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700),
        ),
        style: OutlinedButton.styleFrom(
          foregroundColor: widget.acento,
          side: BorderSide(color: widget.acento.withValues(alpha: 0.4)),
          padding: const EdgeInsets.symmetric(vertical: 10),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.sm)),
        ),
      ),
    ]);
  }

  Future<void> _agregar() async {
    // Ya elegidos (mismo ítem, sin silo) no se repiten: sumaría dos filas al
    // mismo consumo en vez de una cantidad más grande en la misma fila.
    final yaElegidos = widget.lineas.map((l) => l.item.id).toSet();
    final disponibles = widget.catalogo.where((i) => !yaElegidos.contains(i.id)).toList();

    final elegido = await showModalBottomSheet<ItemInventario>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => _BuscadorItems(
        items: disponibles,
        disponibleDe: widget._disponibleDe,
        acento: widget.acento,
      ),
    );
    if (elegido == null) return;
    setState(() {
      widget.lineas.add(LineaConsumo(item: elegido));
      widget.onChanged();
    });
  }
}

class _FilaItem extends StatelessWidget {
  const _FilaItem({
    required this.linea,
    required this.disponible,
    required this.acento,
    required this.onCantidadCambio,
    required this.onQuitar,
  });

  final LineaConsumo linea;
  final double? disponible;
  final Color acento;
  final ValueChanged<String> onCantidadCambio;
  final VoidCallback onQuitar;

  @override
  Widget build(BuildContext context) {
    final excedeStock = disponible != null && linea.cantidadKg > disponible!;

    return Container(
      padding: const EdgeInsets.all(AppSpacing.s3),
      decoration: BoxDecoration(
        color: AppColors.cream,
        borderRadius: BorderRadius.circular(AppRadius.sm),
        border: excedeStock ? Border.all(color: AppColors.danger.withValues(alpha: 0.5)) : null,
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          Expanded(child: Text(linea.item.nombre, style: const TextStyle(
            fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.ink900,
          ))),
          GestureDetector(
            onTap: onQuitar,
            child: const Text('Quitar', style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600, color: AppColors.danger,
            )),
          ),
        ]),
        const SizedBox(height: 2),
        Text(
          disponible == null ? 'Disponible: sin dato' : 'Disponible: ${_kg(disponible!)}',
          style: TextStyle(
            fontFamily: 'Inter', fontSize: 10,
            color: excedeStock ? AppColors.danger : AppColors.ink500,
            fontWeight: excedeStock ? FontWeight.w700 : FontWeight.w400,
          ),
        ),
        const SizedBox(height: AppSpacing.s2),
        AppField(
          label: 'Cantidad', suffix: 'kg', placeholder: '0',
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          onChanged: onCantidadCambio,
        ),
      ]),
    );
  }

  static String _kg(double v) =>
      '${v.toStringAsFixed(v.truncateToDouble() == v ? 0 : 1)} kg';
}

class _BuscadorItems extends StatefulWidget {
  const _BuscadorItems({required this.items, required this.disponibleDe, required this.acento});

  final List<ItemInventario> items;
  final double? Function(ItemInventario) disponibleDe;
  final Color acento;

  @override
  State<_BuscadorItems> createState() => _BuscadorItemsState();
}

class _BuscadorItemsState extends State<_BuscadorItems> {
  String _q = '';

  @override
  Widget build(BuildContext context) {
    final filtrados = _q.trim().isEmpty
        ? widget.items
        : widget.items.where((i) => i.nombre.toLowerCase().contains(_q.trim().toLowerCase())).toList();

    return DraggableScrollableSheet(
      initialChildSize: 0.7, minChildSize: 0.4, maxChildSize: 0.92, expand: false,
      builder: (context, scrollController) => Container(
        decoration: const BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.lg)),
        ),
        padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, AppSpacing.s4),
        child: Column(children: [
          Container(
            width: 36, height: 4, margin: const EdgeInsets.only(bottom: AppSpacing.s3),
            decoration: BoxDecoration(color: AppColors.line, borderRadius: BorderRadius.circular(2)),
          ),
          AppField(
            label: 'Buscar ítem', placeholder: 'Nombre del alimento…',
            onChanged: (v) => setState(() => _q = v),
          ),
          const SizedBox(height: AppSpacing.s2),
          Expanded(
            child: filtrados.isEmpty
                ? const Center(child: Text('Sin resultados', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)))
                : ListView.separated(
                    controller: scrollController,
                    itemCount: filtrados.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (context, i) {
                      final item = filtrados[i];
                      final disp = widget.disponibleDe(item);
                      return ListTile(
                        title: Text(item.nombre, style: const TextStyle(
                          fontFamily: 'Inter', fontSize: 13, fontWeight: FontWeight.w600)),
                        subtitle: Text(
                          disp == null ? 'Sin dato de existencia' : 'Disponible: ${disp.toStringAsFixed(disp.truncateToDouble() == disp ? 0 : 1)} kg',
                          style: const TextStyle(fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500),
                        ),
                        trailing: Icon(Icons.add_circle_outline_rounded, color: widget.acento),
                        onTap: () => Navigator.of(context).pop(item),
                      );
                    },
                  ),
          ),
        ]),
      ),
    );
  }
}

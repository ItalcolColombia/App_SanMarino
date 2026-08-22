/// Catálogo de ítems y existencias — lo que el operario necesita tener encima
/// para poder elegir alimento sin señal.
///
/// Se descarga entero en la sincronización diaria y se guarda en SQLite. Un
/// catálogo que hay que pedir al momento de capturar no sirve: en el galpón no
/// hay red, que es todo el punto de esta app.
library;

import 'dart:convert';

import '../models_inventario.dart';
import 'api_client.dart';

class InventarioApi {
  InventarioApi(this._api);

  final ApiClient _api;

  /// `GET /api/inventario/items?activo=true`
  ///
  /// **Sin `?tipoItem=`, a propósito.** Ese filtro compara exacto y sensible a
  /// mayúsculas contra el texto del catálogo, y los alimentos están cargados
  /// como `Alimento`, `alimento` y `ALIMENTO` según la empresa: pedir
  /// `?tipoItem=alimento` devuelve 1 de los 8 alimentos de Ecuador. Se baja todo
  /// y se clasifica localmente (`ItemInventario.esAlimento`), que es lo que hace
  /// el web.
  Future<List<ItemInventario>> catalogo() async {
    final cuerpo = await _api.getRaw('/inventario/items', query: {'activo': true});
    return _lista(cuerpo).map(ItemInventario.fromJson).toList();
  }

  /// `GET /api/inventario-gestion/stock`
  ///
  /// Trae la existencia con su **clave completa** (granja, núcleo, galpón, silo,
  /// ítem). Guardar un saldo por ítem mostraría el total de la granja y dejaría
  /// pasar un consumo contra un galpón vacío.
  ///
  /// [farmId] acota la descarga: sin él vendría el stock de todas las granjas de
  /// la empresa, que en una tablet de una sola granja es peso muerto.
  Future<List<ExistenciaInventario>> existencias({int? farmId}) async {
    final cuerpo = await _api.getRaw(
      '/inventario-gestion/stock',
      query: farmId == null ? null : {'farmId': farmId},
    );
    return _lista(cuerpo).map(ExistenciaInventario.fromJson).toList();
  }

  /// `GET /api/LoteSilo/{loteId}` — los silos que el lote tiene asignados.
  ///
  /// Sólo hace falta en las empresas con `maneja_inventario_por_silo`. Ojo: el
  /// id que espera es el del lote **maestro**, no el de la etapa.
  ///
  /// Un lote sin silos asignados no puede consumir con el flag encendido: el
  /// backend rechaza cada ítem. Vale la pena saberlo **al capturar**, no al
  /// sincronizar horas después.
  Future<List<SiloDelLote>> silosDelLote(int loteMaestroId) async {
    final cuerpo = await _api.getRaw('/LoteSilo/$loteMaestroId');
    return _lista(cuerpo).map(SiloDelLote.fromJson).toList();
  }

  static List<Map<String, dynamic>> _lista(String cuerpo) {
    if (cuerpo.trim().isEmpty) return const [];
    final json = jsonDecode(cuerpo);
    // Algunos listados vienen envueltos; el de stock viene pelado.
    final filas = switch (json) {
      List l => l,
      Map m => (m['items'] ?? m['data'] ?? const []) as List,
      _ => const [],
    };
    return filas.whereType<Map>().map((e) => e.cast<String, dynamic>()).toList();
  }
}

/// Un silo asignado a un lote.
class SiloDelLote {
  const SiloDelLote({required this.siloId, required this.nombre});

  final int siloId;
  final String nombre;

  factory SiloDelLote.fromJson(Map<String, dynamic> j) => SiloDelLote(
        siloId: (j['siloId'] ?? j['id'] ?? 0) as int,
        nombre: (j['siloNombre'] ?? j['nombre'] ?? '') as String,
      );

  Map<String, dynamic> toJson() => {'siloId': siloId, 'nombre': nombre};
}

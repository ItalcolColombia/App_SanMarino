/// Qué se le muestra al usuario según el país de su empresa.
///
/// Regla vinculante del repo (§ Features por EMPRESA de CLAUDE.md): la decisión
/// se toma **por dato**, nunca con un `if (empresa == 'ItalcolPanama')`. El país
/// llega en `companyPaises[].paisId` de la respuesta del login; acá sólo se
/// decide, sin red ni estado, para que sea testeable en un `flutter test`.
///
/// Si el país es desconocido (o la sesión aún no lo resolvió), todo se apaga:
/// **fail-closed**. Mostrar un campo de más contamina el registro de una empresa
/// que no lo usa; mostrarlo de menos sólo obliga a completar desde el web.
library;

/// Ids de `paises.pais_id`. Son los de la BD, no un enum inventado por la app.
class PaisId {
  const PaisId._();

  static const int colombia = 1;
  static const int ecuador = 2;
  static const int panama = 3;
}

class PerfilPais {
  const PerfilPais._();

  /// Control de agua: pH, ORP y temperatura. Sólo Ecuador y Panamá lo capturan.
  static bool controlAgua(int? paisId) =>
      paisId == PaisId.ecuador || paisId == PaisId.panama;

  /// Alimento en quintales (`qqMixtas` / `qqHembras` / `qqMachos`): sólo Panamá.
  /// El resto de países registra el consumo en kilos.
  static bool quintales(int? paisId) => paisId == PaisId.panama;

  /// Qué id de ítem entiende el backend para descontar inventario.
  ///
  /// En **Ecuador y Panamá** el consumo usa `itemInventarioEcuadorId` tal cual,
  /// sin traducir. En **Colombia** el backend recibe `catalogItemId` y lo cruza
  /// por código contra el catálogo de la empresa.
  ///
  /// No es una preferencia de estilo: mandar sólo `catalogItemId` en Ecuador o
  /// Panamá hace que el parser lo tome **como si fuera** un id de inventario, y
  /// hay 227 ids que colisionan entre las dos tablas. El descuento puede caer
  /// sobre otro producto sin dar ningún error.
  ///
  /// Fail-closed: sin país resuelto devuelve `false`, que es el caso conservador
  /// (mandar sólo `catalogItemId`, que en el peor caso no descuenta en vez de
  /// descontar mal).
  static bool usaItemInventarioEcuador(int? paisId) =>
      paisId == PaisId.ecuador || paisId == PaisId.panama;

  /// Nombre legible, para la cabecera del perfil.
  static String nombre(int? paisId) => switch (paisId) {
        PaisId.colombia => 'Colombia',
        PaisId.ecuador => 'Ecuador',
        PaisId.panama => 'Panamá',
        _ => 'Sin país',
      };

  /// Resuelve el id desde el nombre que manda el backend en `companyPaises`.
  /// Tolerante a mayúsculas y a la tilde de "Panamá", que viaja de las dos formas.
  static int? idDesdeNombre(String? nombre) {
    final n = (nombre ?? '').trim().toLowerCase();
    if (n.isEmpty) return null;
    if (n.startsWith('colombia')) return PaisId.colombia;
    if (n.startsWith('ecuador')) return PaisId.ecuador;
    if (n.startsWith('panam')) return PaisId.panama;
    return null;
  }
}

/// Formateo compartido entre features.
///
/// Vive acá y no dentro de una feature porque lo usan `lotes/` y su selector:
/// duplicarlo sería la vía rápida para que los dos se desincronicen.
library;

/// Separador de miles con punto, como se lee en Colombia/Ecuador/Panamá.
///
/// `25038` → `25.038`
String fmtMiles(int n) =>
    n.toString().replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.');

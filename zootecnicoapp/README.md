# App móvil San Marino Zootécnico — código de desarrollo

Código Flutter listo para copiar al proyecto real (`App_SanMarino/zootecnicoapp/`).
Traducción directa del design system: los colores, tipografías, espaciados y
patrones de esta carpeta son los mismos que verás en `../ui_kits/mobile_app/v2/`.

## Cómo integrarlo

```bash
# desde la raíz de App_SanMarino
cp -r <design-system>/movil/lib/*        zootecnicoapp/lib/
cp -r <design-system>/movil/assets/*     zootecnicoapp/assets/
# revisar pubspec.yaml y fusionar dependencies + fonts + assets
```

Luego descargar las fuentes a `assets/fonts/`:
- [Plus Jakarta Sans](https://fonts.google.com/specimen/Plus+Jakarta+Sans) — Regular, Medium, SemiBold, Bold, ExtraBold
- [Inter](https://fonts.google.com/specimen/Inter) — Regular, Medium, SemiBold, Bold

```bash
flutter pub get
flutter run
```

## Estructura

```
movil/
├── pubspec.yaml                  Dependencias, fuentes y assets
├── assets/images/brand/          Logos oficiales (ya copiados)
└── lib/
    ├── main.dart                 App + RootShell + barra inferior con FAB
    ├── theme/
    │   ├── app_colors.dart       Paleta suavizada + sombras cálidas
    │   ├── app_spacing.dart      Escala 4pt, radios, tamaños de fuente
    │   └── app_theme.dart        ThemeData completo (foco naranja, cards, inputs)
    ├── core/
    │   ├── models.dart           Usuario, Lote, ModuloSeguimiento, ItemSeguimiento
    │   ├── local_db.dart         SQLite: cola de sync, cache de lotes y catálogo
    │   └── sync_service.dart     Calidad de conexión + flujo de reconexión
    ├── widgets/
    │   ├── app_widgets.dart      AppButton, AppBadge, AppField, AppPairField,
    │   │                         AppSection, AppInfoBox, AppStatTile, AppSavedChip
    │   └── sync_widgets.dart     SyncDot, AmbientDot, ConnectionChip, SyncRibbon
    └── screens/
        ├── login_screen.dart     Login + recuperar contraseña
        ├── home_screen.dart      Bienvenida con animación gallina+huevo (CustomPainter)
        ├── app_screens.dart      Lotes, cola de sync, perfil, selector de módulo
        └── seguimiento_screen.dart  Los 4 formularios de seguimiento diario
```

## Los 4 módulos de seguimiento

Un solo `SeguimientoScreen` que cambia sus secciones según `lote.modulo`.
Los campos salen de los modales reales del web:

| Módulo | Secciones | Origen en el web |
|---|---|---|
| **Levante** | General · Ítems H/M/Generales · Mortalidad y selección · Peso y uniformidad · Agua | `lote-levante/pages/modal-create-edit` |
| **Pollo Engorde** | General · Alimento · Mortalidad y selección · Peso (obligatorio) · Agua | `aves-engorde/pages/modal-seguimiento-engorde` |
| **Producción** | General (etapa/ciclo) · Hembras · Machos · Clasificadora de huevos (11 tipos) · Pesaje semanal · Agua | `lote-produccion/pages/modal-seguimiento-diario` |
| **Reproductora** | General · Hembras (ítems + bajas) · Machos (ítems + bajas) · Peso · Agua | `seguimiento-diario-lote-reproductora/pages/modal-seguimiento-reproductora` |

La sección **Agua** (pH, ORP, temperatura) solo aparece si `usuario.tieneControlAgua`,
es decir cuando el país es Ecuador o Panamá.

## Offline-first

El flujo es siempre el mismo:

1. El usuario llena el formulario y toca **Guardar registro**
2. `SyncService.encolar()` escribe en SQLite (`pending_sync`) y **retorna de inmediato**
3. Aparece el chip verde "Guardado aquí" y la pantalla se cierra
4. Si hay red y `autoSync` está activo, la cola se sube en segundo plano
5. Al recuperar conexión, el `SyncRibbon` muestra: detectando → sincronizando → al día → se oculta

Reglas de UX que el código respeta:
- Cuando todo está al día, **nada** es visible (ni chip ni banner)
- Offline nunca es rojo — es un modo de trabajo válido
- Sin spinners en el formulario: la confirmación es optimista

## Pendiente de conectar

Los `TODO` del código marcan los cuatro puntos de integración:

1. **`login_screen.dart`** → `AuthService.login()`; el backend devuelve el JSON con
   `modulos[]` (según rol) y `loteIds[]` (según granjas asignadas)
2. **`login_screen.dart`** → `AuthService.recuperarPassword()`; envía correo con nueva clave
3. **`main.dart`** → endpoint de lotes asignados (hoy usa `_lotesDemo`)
4. **`sync_service.dart`** → el `POST` real por cada `tipo` de registro en la cola

## Movimientos y ventas

Diseñados en el prototipo (`ui_kits/mobile_app/v2/Movimientos.jsx`) pero **no
portados a Flutter todavía**, porque el foco acordado es el seguimiento diario:

- Venta de aves (despacho por galpón con asignación H/M sobre mixtas)
- Traslado de aves (entre lotes o venta desde levante)
- Movimiento de huevos (traslado, venta o desecho)

Cuando los necesiten, la referencia visual y los campos exactos están en el prototipo.

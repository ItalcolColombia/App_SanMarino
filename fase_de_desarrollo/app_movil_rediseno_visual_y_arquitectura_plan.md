# App móvil (`zootecnicoapp`) — rediseño visual, transiciones, offline y arquitectura

> **Estado:** plan · 23-ago-2026
> **Alcance:** solo `zootecnicoapp/` (app Flutter). El front Angular y el backend **no se tocan**,
> salvo lectura como referencia de diseño.

---

## 0. Qué pidió el usuario (literal, y su traducción a trabajo)

| Pedido | Traducción |
|---|---|
| «mejora visual de la app y transiciones» | Sistema de diseño alineado a la web validada + animaciones y transiciones reales (hoy no hay). |
| «que tenga siempre el offline» | El offline-first no se rompe **y** se cierran los huecos donde hoy falla. |
| «los logos sean los del login de la web; el de italfoods lo eliminamos porque ni en la web se usa y se confunden» | Portar el `logo-stack` del login web; **borrar** el asset Italfoods. |
| «la app más profesional, ya que validamos la web y patrones de diseño» | La web es la **referencia validada**: se adoptan sus tokens y patrones. |
| «organiza los archivos con una arquitectura bien definida» | Reestructura **feature-first** completa. |
| «colócala en el CLAUDE del archivo de la app para que no se desfase» | Crear `zootecnicoapp/CLAUDE.md` con la arquitectura y las reglas, vinculante para futuras sesiones. |

**Decisiones tomadas por el usuario en esta sesión:**
1. **Color = híbrido** — marca en acentos y acciones; fondos/neutros cálidos se quedan.
2. **Arquitectura = feature-first completa.**

---

## 1. Hallazgo que cambia el enfoque: la app incumple la regla de marca del repo

`CLAUDE.md` (raíz) fija la regla, y es **vinculante**:

> `rojo SanMarino` = identidad/marca · **`naranja` = acciones** · **`verde` solo éxito** · `rojo` solo peligro/destructivo.

La app móvil hoy usa **verde como color de acción primaria** — el patrón *legacy* que el front web
**ya migró**. Medido:

| Archivo | Línea | Qué |
|---|---|---|
| `lib/theme/app_theme.dart` | 18 | `colorScheme.primary = green500` |
| `lib/theme/app_theme.dart` | 85 | `FilledButton.backgroundColor = green500` |
| `lib/widgets/app_widgets.dart` | 38 | `AppButtonVariant.primary → green500` |
| `lib/main.dart` | 345, 349 | ítem activo del bottom nav en `green600` |
| `lib/screens/login_screen.dart` | 323 | botón de texto en `green600` |

Además, los hex que el `CLAUDE.md` de la raíz cita (`ital-orange #e85c25` / `ital-green #2d7a3e`)
están **desactualizados**: el código real del web y la memoria del proyecto usan **`#F5821F`**.
Fuente medida: `frontend/src/app/features/auth/login/login.component.scss:8`.

> **Consecuencia visible:** el botón «Entrar», el FAB y el ítem activo del nav **pasan de verde a
> naranja**. No es un capricho estético: es aplicar la regla de marca que el repo ya tiene escrita.

---

## 2. Sistema de diseño — tokens (híbrido aprobado)

Fuente de verdad medida: `frontend/src/app/features/auth/login/login.component.scss` +
memoria `paleta-marca-italcol`.

### 2.1 Lo que ENTRA de la marca (acentos y acciones)

| Rol | Token nuevo | Hex | Origen |
|---|---|---|---|
| **Acción / CTA** | `brand500` | `#F5821F` | `--brand-orange` |
| Acción · hover | `brand600` | `#E86F12` | `--brand-orange` hover |
| Acción · pressed | `brand700` | `#C85A0E` | `--brand-orange-dark` |
| Acción · tinte | `brand50` | `rgba(245,130,31,.14)` | `--brand-orange-light` |
| Acento dorado | `gold500` | `#FBB040` | `--brand-yellow` |
| Acento dorado claro | `gold400` | `#FDB813` | `--brand-gold` |
| **Identidad SanMarino** | `sanMarinoRed` | `#D2181E` | `--brand-sanmarino` |
| Identidad · profundo | `sanMarinoRedDeep` | `#991918` | logo SanMarino |
| **Peligro** | `danger` | `#DC2626` | `--brand-red` |

Gradiente de marca (dorado → naranja, **sin rojo**): `#FDB813 → #F5821F`.

### 2.2 Lo que NO se toca (decisión del usuario: legibilidad en campo)

Fondos, superficies, textos y neutros cálidos siguen igual — ya son casi idénticos a los del web
(`#FBF8F3` app vs `#FAFAF9` web):

`cream #FBF8F3` · `cream2 #F4EFE6` · `surface #FFFFFF` · `ink900 #1E2620` · `ink700 #3A4640` ·
`ink500 #6B736F` · `ink300 #A5ABA7` · `ink200 #D6D9D7` · `ink100 #ECEEEC` · touch target **44 px**.

### 2.3 Verde: **degradado a estado, no acción**

`green500 #4F8A60` deja de ser `primary`. Queda **solo** como `success` y como color categórico del
módulo Levante. Ese uso categórico (módulo) es un eje distinto del semántico y **no** viola la regla.

### 2.4 Reglas de uso (van al `CLAUDE.md` de la app)

- Naranja `brand500` → **toda** acción primaria: botones, FAB, nav activo, links.
- Verde → **solo** éxito (y el color categórico de Levante).
- Rojo `danger` → **solo** peligro/destructivo.
- Rojo `sanMarinoRed` → **solo** identidad de marca (logo-stack, borde de marca). **Nunca** en botones.
- Dorado → acentos, badges, highlights.
- Ningún `Color(0x…)` literal fuera de `design_system/`. Ningún padding fuera de `AppSpacing`.

---

## 3. Marca y logos

**Medido:** los 6 assets de marca de la app son **byte-idénticos** a los del web (mismo sha256).
No hay que copiar nada, solo cambiar a cuáles se apunta.

| Asset | Usos en la **web** | Usos en la **app** hoy | Decisión |
|---|---|---|---|
| `italcol-naranja.png` (web: `italcol-naraanja.png`) | **7** | login:225 | ✅ **primario** del logo-stack |
| `logo-sanmarino.png` (web: `Logo-sanmarino-innovacion.png`) | **4** | — | ✅ **secundario** del logo-stack |
| `logo-italfoods-zootecnico.png` | **0** | perfil (`app_screens.dart:498`) | ❌ **borrar el archivo** |
| `icono-logo.png` | **0** | login:28 | ❌ dejar de usar |
| `italcol-blanco.png` / `v-logo.png` | 2 / 0 | — | se conservan, sin uso nuevo |

**Texto de marca.** La web se identifica como **ItalGranja**, tagline
`Gestión de granjas avícolas · Italcol` (`frontend/src/environments/environment.ts:4-6`).
La app dice `Genética avícola · Italfoods` — la misma confusión que motivó el pedido.
→ Se conserva el nombre de producto **«San Marino Zootécnico»** (es el título de la app y el del
repo) y se **elimina toda mención a Italfoods**; el tagline pasa a `Gestión de granjas avícolas · Italcol`.

**Logo-stack a portar** (del login web): logo primario Italcol → logo secundario SanMarino →
divider de marca (gradiente naranja→rojo SanMarino) → tagline.

`pubspec.yaml` declara la carpeta (`assets/images/brand/`), no archivo por archivo: **borrar el png
no requiere tocar el pubspec**.

---

## 4. Arquitectura destino — feature-first

```
lib/
├── main.dart                    solo bootstrap + shell raíz
├── core/                        datos y servicios (sin UI)
│   ├── api/                     api_client, auth, lotes, seguimientos, inventario
│   ├── db/                      local_db + migraciones
│   ├── session/                 session_store, sesion_actual
│   ├── sync/                    sync_service, cola offline
│   ├── models/                  modelos de dominio
│   ├── config/                  api_config
│   ├── crypto/                  crypto_service
│   └── platform/                platform_db (factory web condicional)
├── design_system/               ← ÚNICA fuente de tokens y primitivas
│   ├── tokens/                  colors, spacing, typography, motion
│   ├── components/              AppButton, AppBadge, AppCard, AppStatTile…
│   ├── motion/                  curvas, duraciones, page transitions
│   └── feedback/                skeletons, estados vacíos, toasts
├── features/
│   ├── auth/                    login, recuperación
│   ├── home/                    inicio
│   ├── lotes/                   listado, filtros, tarjeta
│   ├── seguimiento/             formulario + funciones/ (lógica pura)
│   ├── sync/                    bandeja de cola offline
│   └── perfil/
└── shared/                      utilidades transversales
```

**Reglas de dependencia (se verifican, no se confían):**
- `core/` **no** importa de `features/` ni de `design_system/`.
- `design_system/` **no** importa de `features/` ni de `core/`.
- `features/X` **no** importa de `features/Y` (lo común sube a `shared/` o `design_system/`).
- La lógica pura vive en `features/<x>/funciones/`: sin `BuildContext`, sin red, sin estado — y con test.

Es el mismo patrón que el repo ya usa en `movimientos-pollo-engorde` (front) y en
`Application/Calculos/` (back), bajado a Dart.

---

## 5. Transiciones y movimiento

**Estado medido hoy:** navegación con `MaterialPageRoute` pelado (`main.dart:235,241`,
`login_screen.dart:208`), cambio de tab con un `switch` sin animación, y animación real en **solo 2
archivos** (`home_screen.dart` la gallina, `sync_widgets.dart`). Es la causa principal de que se
sienta poco profesional.

| Dónde | Qué | Duración | Curva |
|---|---|---|---|
| Navegación push/pop | fade + slide-up 12 px | 260 ms | `easeOutCubic` |
| Cambio de tab | `AnimatedSwitcher` fade + slide 8 px | 200 ms | `easeOut` |
| Ítems de lista | entrada escalonada (stagger 30 ms/ítem, tope 6) | 240 ms | `easeOutCubic` |
| Botón al presionar | escala 0.97 | 120 ms | `easeOut` |
| Indicador del bottom nav | pill deslizante | 240 ms | `easeOutCubic` |
| Secciones del formulario | expand/collapse + rotación del chevron | 200 ms | `easeInOut` |
| Carga de datos | skeleton shimmer (reemplaza spinner) | 1200 ms loop | `linear` |
| Ribbon de sync | slide-down al cambiar de estado | 240 ms | `easeOutCubic` |

Todo sale de `design_system/motion/` — nada de duraciones sueltas en las pantallas.
**Respeto de accesibilidad:** si `MediaQuery.disableAnimations` está activo, las duraciones caen a 0.

---

## 6. Offline — no romperlo y cerrar huecos

Requisito duro del usuario. El refactor mueve archivos: los invariantes se listan **antes** de mover
y se verifican **después**.

**Invariantes (de la auditoría del workflow — sección a completar con el mapa):**
- La cola `pending_sync` **nunca** se borra en logout (ya documentado en `session_store.dart:76-78`).
- Un registro guardado sin red queda en SQLite y se sube al reconectar.
- Sin red la app entra con la caché y **no** bloquea el ingreso.
- La sesión se lee del disco **antes** de pintar (evita el flash de login).

**Mejoras de percepción del offline** (el pedido «que tenga siempre el offline» es también de UX):
- Estado de conexión y pendientes **siempre visible**, no solo cuando falla.
- Cada registro encolado confirma en pantalla que quedó guardado.
- Distinguir «sin red» de «error real»: sin red no es un fallo, es el caso normal en granja.

---

## 7. Verificación

| Compuerta | Comando | Criterio |
|---|---|---|
| Análisis estático | `flutter analyze` | 0 errores; sin *info* nuevos |
| Tests | `flutter test` | ≥ 165 (los de hoy) + los nuevos de lógica pura |
| Build | `flutter build web --release` | compila |
| Reglas de capa | grep de imports | 0 violaciones |
| Smoke visual | app en el navegador | login → home → lotes → seguimiento → perfil, sin excepciones en consola |
| Offline | smoke con red cortada | registro se encola y sube al reconectar |

**Refactor ≠ cambio de comportamiento:** payloads, contratos con el backend y aritmética quedan
idénticos. Lo único que cambia a propósito es la capa visual y la ubicación de los archivos.

---

## 8. Entregable de documentación

`zootecnicoapp/CLAUDE.md` — vinculante para futuras sesiones, con: mapa de carpetas, reglas de
dependencia, tokens y regla de marca, catálogo de primitivas, sistema de movimiento, contrato
offline y checklist de PR. Es lo que evita que la arquitectura «se desfase».

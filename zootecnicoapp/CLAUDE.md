# CLAUDE.md — App móvil `zootecnicoapp` (Flutter)

Guía **vinculante** para trabajar en la app móvil. Complementa al `CLAUDE.md` de la raíz (que manda
en backend, front web, BD y despliegue); acá mandan las reglas de **esta** app.

> **Quién la usa:** supervisores avícolas, parados en un galpón, con guantes, bajo sol fuerte y
> **normalmente sin señal**. Cada decisión de esta guía sale de ahí. Si una elección de diseño se
> ve linda en el escritorio pero le cuesta un registro al operario en el campo, está mal.

---

## 🧭 Los 4 invariantes de producto

1. **Offline-first, sin excepción.** Todo registro se guarda en SQLite **primero** y se sincroniza
   después. Nada bloquea la captura por falta de red.
2. **Nunca confirmar antes de haber guardado.** Si el chip verde aparece y el `INSERT` falla, el
   operario se va convencido de haber anotado el día y el dato no existe en ningún lado.
3. **Offline no es un error.** Es el modo normal de trabajo. Nunca en rojo, nunca con ícono de alarma.
4. **La cola es trabajo de una persona.** `pending_sync` no se borra por cerrar sesión, ni por
   migrar, ni por un 401. Se borra **sólo** cuando el servidor confirmó que recibió la fila.

---

## 🏗️ Arquitectura — feature-first

```
lib/
├── main.dart                     bootstrap + shell (tabs, nav inferior)
│
├── core/                         datos y servicios · SIN UI
│   ├── api/                      api_client, auth, lotes, seguimientos, inventario
│   ├── db/                       local_db (SQLite, esquema y migraciones)
│   ├── sync/                     sync_service (cola offline, máquina de estados)
│   ├── session/                  session_store, sesion_actual
│   ├── models/                   modelos de dominio
│   ├── reglas/                   lógica pura compartida con core/api
│   ├── calculos/                 lógica pura de decisión (p. ej. conectividad)
│   ├── config/ · crypto/         configuración y cifrado
│   └── platform/                 factory de BD por plataforma (import condicional)
│
├── design_system/                ÚNICA fuente de estilo
│   ├── tokens/                   app_colors, app_spacing (+ radios, tipografía, touch)
│   ├── components/               primitivas (AppButton, AppField…) y marca
│   ├── motion/                   app_motion (duraciones/curvas) + transiciones
│   └── app_theme.dart            ThemeData: composición de tokens
│
├── features/                     una carpeta por dominio de pantalla
│   ├── auth/ home/ lotes/ seguimiento/ sync/ perfil/
│   │   ├── pages/                la pantalla
│   │   ├── widgets/              UI propia de esa feature
│   │   └── funciones/            lógica PURA de esa feature (+ su test)
│
└── shared/                       utilidades transversales (formato, etc.)
```

### Reglas de dependencia (no son sugerencias)

| Capa | Puede importar | **No** puede importar |
|---|---|---|
| `core/` | `core/` | `features/`, `design_system/` |
| `design_system/` | `design_system/` | `features/`, `core/` |
| `features/X` | `core/`, `design_system/`, `shared/` | `features/Y` |
| `shared/` | nada del proyecto | todo lo demás |

**Cómo decidir dónde va una función pura:**
- ¿La usa `core/api`? → `core/reglas/` o `core/calculos/`. Si la bajás a `features/`, `core` pasaría
  a importar de `features/` y se invierte la capa.
- ¿La usa una sola feature? → `features/<x>/funciones/`.

*Ejemplo real:* `postura_calculos.dart` **se queda en `core/`** porque lo consume
`core/api/seguimientos_api.dart`; `items_consumo.dart` **baja a la feature** porque sólo lo usa el
formulario.

**Excepción declarada:** los widgets de estado de sincronización (`features/sync/widgets/`) los
consumen varias features. Es estado global de la app, no lógica de la feature sync. Si aparece un
segundo caso así, sube a `shared/` o a `design_system/`.

### Imports

Siempre `package:zootecnicoapp/...`, nunca `../../../`.

```dart
import 'package:zootecnicoapp/core/models/models.dart';   // ✅
import '../../../core/models/models.dart';                 // ❌
```

Mover un archivo no rompe sus propios imports, y la capa se lee en la línea del import.
**Única excepción:** el `export` condicional de `core/platform/db_init.dart`, que es relativo por
requisito del mecanismo.

### Nombres

- Archivo de pantalla: `<algo>_page.dart` → clase `<Algo>Page`. El nombre de la clase sigue al archivo.
- Widget de feature: `<algo>.dart` → clase `<Algo>`.
- Privados (`_Algo`) sólo si viven en el **mismo** archivo que su único consumidor. Al partir un
  archivo, un privado compartido **deja de verse**: publicalo o movelo junto.

---

## 🎨 Sistema de diseño

**Todo el estilo sale de `design_system/`.** En una pantalla **no** puede haber un `Color(0x…)`
literal, un tamaño de fuente suelto ni un padding mágico.

### Regla de marca (heredada del `CLAUDE.md` de la raíz — **vinculante**)

| Color | Rol | Dónde |
|---|---|---|
| **Naranja `#F5821F`** (`brand500`) | **acciones** | botones, FAB, nav activo, links, foco de input |
| Dorado `#FBB040` (`gold500`) | acento | badges, highlights |
| Verde (`green500`) | **sólo éxito** | + color categórico del módulo Levante |
| Rojo `#DC2626` (`danger`) | **sólo peligro** | destructivo, errores reales |
| Rojo SanMarino `#D2181E` | **sólo identidad** | logo-stack, divisor de marca. **Nunca** un botón |

> ⚠️ El `CLAUDE.md` de la raíz cita `ital-orange #e85c25`: **está desactualizado**. El código real del
> front web usa `#F5821F` (`frontend/.../login.component.scss:8`). Si hay duda, manda el web.

**Verde ≠ primario.** Hasta ago-2026 la app usaba verde como color de acción — el patrón *legacy*
que el web ya había abandonado. Si ves un botón de acción en verde, es una regresión.

**Neutros cálidos:** son propios de la app (`cream #FBF8F3`, `ink900 #1E2620`, …), bajados en
saturación para lectura prolongada bajo sol. **Decisión deliberada:** no los reemplaces por los del
web sin una razón medida.

### Movimiento

Duraciones y curvas salen de `AppMotion`. Nada supera los **320 ms**: la app se usa parada, con una
mano; una transición larga es tiempo perdido.

```dart
// ✅ respeta "Reducir movimiento" del sistema
duration: AppMotion.duracion(context, AppMotion.fast)

// ❌ ignora accesibilidad
duration: const Duration(milliseconds: 200)
```

Reutilizables en `motion/transiciones.dart`: `rutaApp()` / `rutaModal()` (en vez de
`MaterialPageRoute`), `EntradaEscalonada`, `CambioSuave`, `PresionHundida`.

### Marca y logos

El bloque de marca es `design_system/components/marca.dart` (`LogoMarca`, `DivisorMarca`,
`MarcaDeAgua`). Replica el logo-stack del login web.

- Logos vigentes: `italcol-naranja.png` (primario) y `logo-sanmarino.png` (secundario). Son
  **byte-idénticos** a los del web.
- ❌ **No reintroducir `logo-italfoods-zootecnico.png`**: el web no lo usa en ningún lado (0
  referencias) y se confundía con la marca real. Se eliminó el 23-ago-2026.
- El tagline sale de `kTaglineApp`. No escribir "Italfoods" en ningún texto.

---

## 📴 Contrato offline — invariantes que no se rompen

Medidos sobre el código, cada uno con su prueba o su file:line. Si tu cambio toca alguno,
justificalo por escrito.

| # | Invariante |
|---|---|
| **I1** | `pending_sync` nunca se hace `DROP`/`TRUNCATE` en una migración: sólo `ALTER ADD COLUMN` dentro de `try/catch`. |
| **I2** | El único `DELETE` de `pending_sync` es `confirmarEnviado`, y va en la **misma transacción** que el `INSERT` a `seguimientos_local`. Nunca separarlos ni reordenarlos. |
| **I3** | `SessionStore.cerrar()` no toca `pending_sync`. Cerrar sesión jamás borra trabajo del usuario. |
| **I4** | `porEnviar()` ordena `created_at ASC`. El backend valida contra el saldo del lote: invertirlo produce rechazos artificiales. |
| **I5** | El `endpoint` viaja congelado con la fila; `sincronizar()` lo prefiere sobre el mapa actual. Un remapeo futuro no redirige lo ya anotado. |
| **I6** | `TipoFallo.duplicado` **nunca** es error: llama `confirmarEnviado`. El detector reconoce 5 formas (incluido el `23505` crudo dentro de un 500); quitar cualquiera = reintento infinito. |
| **I7** | `TipoFallo.plataformaRechazada` **nunca** cierra sesión. Rotar el secreto en el servidor borraría la cola de todos los equipos. |
| **I8** | `TipoFallo.sesionVencida` vuelve al login **sin** borrar la cola. |
| **I9** | `datosInvalidos` suelta la marca local del día y **sólo** la de origen `local`: la marca `servidor` es verdad confirmada. |
| **I10** | `LocalDb.encolar` no marca el día; lo hace `SyncService.encolar`. Todo encolador nuevo pasa por `SyncService` o pierde la protección anti-doble-carga. |
| **I11** | `lotes_cache` tiene PK `(modulo, id)`: engorde y reproductora numeran por separado. |
| **I12** | `existencias_inventario` tiene PK `(farm_id, item_id, nucleo_id, galpon_id, silo_id)`. Un saldo por ítem dejaría pasar un consumo contra un galpón vacío. |
| **I13** | `SessionStore.cargar()` corre **antes** de `runApp`, y un token vencido en disco no impide entrar. La app abre siempre, con o sin red. |
| **I14** | `sincronizar()` sale en seco con `_api == null` sin tocar la cola. |
| **I15** | `_fechaIso` manda mediodía sin zona y `_soloFecha` usa componentes locales. Los dos siguen en hora local o la marca del día y el POST se desalinean. |
| **I16** | `guardarLotes`/`guardarCatalogo`/`guardarExistencias` son **reemplazo total intencional**… salvo la guarda **«lista vacía no reemplaza»**: una respuesta vacía con caché en disco no borra nada. Un 200 con cuerpo raro es más destructivo que un error de red. |
| **I17** | `maxIntentos = 5`: la fila agotada deja de reintentarse pero **no** se borra. |
| **I18** | **Ningún camino de guardado pinta confirmación antes de que el `INSERT` haya resuelto.** Encolar primero, confirmar después. |
| **I19** | La feature `seguimiento` nunca escribe en `core/db` directamente: escribe **sólo** vía `SyncService.encolar` (preserva I10). |

### Comportamientos que hay que conocer antes de tocar esto

Los seis huecos de offline que estaban medidos acá **se cerraron el 23-ago-2026**. Queda lo que sigue,
que no son huecos sino decisiones y límites:

- **Los días que el servidor ya tiene se consultan por lote y a demanda**, al abrir el formulario —
  no en la sincronización diaria. El endpoint es uno por lote: con 124 lotes asignados serían 124
  peticiones cada mañana. Si alguna vez se necesita en bloque, hace falta un endpoint que reciba
  varios lotes; no lo resuelvas llamando al actual en un `for`.
- **Un token vencido NO expulsa: la app pasa a «sólo captura»** (`_marcarSesionVencida` en
  `main.dart`). Se puede seguir registrando y viendo la cola; lo único suspendido es subir. No abre
  ninguna puerta —todo lo que se ve ya estaba en el SQLite del equipo— y cerrar sesión a mano sigue
  borrándola de verdad. Antes se cerraba la sesión y, como el login exige red, el operario quedaba
  afuera de su propia app justo cuando más la necesitaba.
- **El historial muestra sólo lo que salió de ESTE equipo.** Una tablet nueva no conoce los días que
  subió otro equipo de la misma granja; la pantalla lo dice en vez de dejar que el usuario lo suponga.
- La ruta de silos sigue a medias **a propósito** (decisión de producto F5.5): `manejaSilos` está fijo
  en `false` y ninguna empresa con ese modelo tiene el flag encendido. `InventarioApi.silosDelLote`,
  `guardarSilosDeLote` y `silosDeLote` existen, sin usar. **No lo cablees sin decisión de producto.**

---

## 🪤 Trampas de Flutter que ya nos costaron caro

**1. `Column` dentro de `bottomNavigationBar` sin `mainAxisSize: MainAxisSize.min`.**
`Scaffold` le da al `bottomNavigationBar` restricciones de alto **sueltas** (0 hasta la pantalla
entera). Un `Column` con el default `MainAxisSize.max` se come toda la altura y deja el `body` en
**0 px**: la app se ve en blanco debajo del login. Costó una sesión entera de diagnóstico.

**2. `borderRadius` + `Border` de colores no uniformes en el mismo `BoxDecoration`.**
Flutter tira `A borderRadius can only be given on borders with uniform colors` **en `paint()`** —
no compila mal, se rompe al dibujar y la tarjeta queda vacía. Es el caso de las tarjetas con acento
lateral de color. Solución: el radio va en un `ClipRRect` aparte, el borde en el `Container` interno.

**3. `sqflite` no tiene backend en web.** Para validar en el navegador se usa
`core/platform/db_init.dart` (import condicional). El `sqlite3.wasm` de `web/` **tiene que coincidir
con la versión del paquete `sqlite3`** que resuelve pub: si no, tira
`WebAssembly.instantiate(): Import #25 "env"`. La herramienta de setup baja una versión fija que
suele estar desactualizada.

**4. Flutter web renderiza dentro de un Shadow DOM** (`flt-glass-pane`). `document.querySelector`
desde el documento raíz **no** encuentra el canvas: no concluyas "no renderiza" por eso.

**5. Un `IconData` elegido en runtime se pinta en BLANCO.**
El build de release tree-shakea la fuente de íconos y sólo conserva los que puede resolver
**estáticamente**. Un ternario deja el ícono fuera del subconjunto y queda un cuadro vacío — no hay
error, no hay warning, y en debug se ve bien.

```dart
// ❌ los dos glifos quedan fuera del subconjunto → cuadro vacío en release
Icon(sincronizadoHoy ? Icons.cloud_done_rounded : Icons.cloud_outlined)

// ✅ ícono fijo; el estado se comunica con el color
Icon(Icons.sync_rounded, color: sincronizadoHoy ? AppColors.green600 : AppColors.warning)
```

Para verificar que un ícono existe en la fuente (antes de acusar al tree-shaker):

```bash
grep "^check_circle_rounded " /c/src/flutter/bin/cache/artifacts/material_fonts/codepoints
```

Diagnóstico rápido: si el ícono **está** en ese archivo pero sale en blanco en release, es el
tree-shaking; pasalo a estático. (En un apuro, `flutter build web --no-tree-shake-icons` lo confirma,
pero la fuente pasa de ~14 KB a 1,6 MB: no es la solución, es el diagnóstico.)

**6. El caché del navegador miente durante la verificación visual.**
Flutter web registra un service worker que sirve el bundle viejo. Si acabás de compilar y la pantalla
no cambió, **no** asumas que tu código no se aplicó: limpiá y recargá antes de diagnosticar.

```js
for (const r of await navigator.serviceWorker.getRegistrations()) await r.unregister();
for (const k of await caches.keys()) await caches.delete(k);
location.reload(true);
```

---

## 🧪 Tests

- Viven en `test/`, planos, nombrados `<tema>_test.dart`.
- **Toda lógica pura nueva lleva test.** Es la regla del repo y acá aplica igual.
- La cola offline se prueba con `sqflite_common_ffi` real (`cola_sync_test.dart`), no con mocks:
  es la pieza donde un bug cuesta el trabajo de campo de alguien.
- Un test que documenta un fallo real lleva en el encabezado **qué fallo** previene.

### Al probar la cola, dos trampas que ya rompieron tests

**1. `encolar`, `reintentar` y la reconexión disparan la subida SIN esperarla.**
Es a propósito: el usuario ya vio su confirmación y la red no puede bloquear la captura. Pero
significa que `await sync.encolar(...)` vuelve **antes** de que la fila haya subido. Afirmar ahí falla
de forma intermitente. Hay que esperar la condición (`esperarA` en `sync_service_test.dart`), no
dormir un rato fijo.

**2. `created_at` tiene precisión de milisegundo.**
Dos filas encoladas en el mismo ms **empatan**, y el orden que devuelve `porEnviar()` entre ellas
queda indefinido. Nunca afirmes sobre `filas.first`: buscá la fila por `loteId`. Si lo que querés
probar es el orden, separá los encolados unos milisegundos.

### Los tests de la cola se validaron con mutación

No alcanza con que estén en verde: se rompió el código a propósito, una regla por vez (duplicado que
frena la cola, rechazo de plataforma que cierra sesión, 401 que borra la fila, encolar que no marca
el día…) y se verificó que **cada una la detecta el test que nombra su invariante**. 9 de 9. Si
agregás una regla nueva a `SyncService`, hacé lo mismo: un test que no falla cuando rompés la regla
no está protegiendo nada.

```bash
flutter analyze     # 0 errores, 0 warnings, sin infos nuevos
flutter test        # todos verdes antes de commitear
```

---

## 🛠️ Comandos

```bash
flutter run                                   # dispositivo/emulador
flutter analyze
flutter test
flutter build apk --release
```

Validar en el navegador (soporte web temporal, sólo para desarrollo):

```bash
flutter build web --dart-define=API_BASE_URL=http://localhost:5002/api
```

y servir `build/web` (hay una config `zootecnicoapp-web-static` en `.claude/launch.json`).
El backend local debe tener el puerto en `AllowedOrigins` de `appsettings.Development.json`, o CORS
bloquea el login.

---

## ✅ Checklist antes de commitear

- [ ] `flutter analyze` en 0 errores y sin infos nuevos
- [ ] `flutter test` verde
- [ ] Cero `Color(0x…)`, tamaños de fuente sueltos o paddings mágicos fuera de `design_system/`
- [ ] Regla de marca respetada: acciones en naranja, verde sólo éxito, rojo sólo peligro
- [ ] Animaciones vía `AppMotion.duracion(context, …)`
- [ ] Reglas de capa: `core/` no importa `features/` ni `design_system/`
- [ ] Imports `package:`, no relativos
- [ ] Si tocaste guardado/cola: los invariantes I1–I19 siguen en pie
- [ ] Si agregaste lógica pura: tiene test

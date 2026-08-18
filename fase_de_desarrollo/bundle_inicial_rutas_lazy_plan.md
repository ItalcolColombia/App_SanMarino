# Aire en el bundle inicial: las pantallas de administración dejan de viajar en el arranque

**Pendiente que ataca:** `tracker_estado.md` → bloque *«PWA — validación de estado y brecha real para
salir a producción»*, §5:

> 🟠 **Aire en el bundle** — el build de hoy da **initial 1,84 MB** contra un techo de error de
> **2,05 MB** (`angular.json:62`) ⇒ quedan **~210 kB de aire**. El riesgo sigue (un import eager
> grande rompe el build de prod).

**Fecha:** 2026-08-17 · Bloque propio — no tocar desde otras sesiones.
Es el único ítem abierto del tracker que es **código puro y no espera decisión, admin ni deploy**
(el resto del bloque PWA depende del merge a `main-produccion`; §5 declara que esto **no** lo bloquea).

---

## 1. Diagnóstico — medido, no estimado

`ng build --source-map` + reparto de los bytes **de salida** por fuente (caminando las `mappings`
del sourcemap, método de source-map-explorer). Contar `sourcesContent` **no sirve**: le atribuye
998 kB a `@fortawesome/free-solid-svg-icons` cuando su barril ya queda podado a **45,5 kB** reales.

`main.js` = **1.671,9 kB**. Reparto real:

| kB de salida | Grupo | ¿Debe estar en el arranque? |
|---:|---|---|
| 310,6 | `features/config` | ❌ pantallas de administración |
| 157,4 | `features/lote` | ❌ |
| 153,6 | `@angular/core` | ✅ |
| 94,8 | `@fortawesome/angular-fontawesome` | ✅ (lo usa el shell) |
| 84,6 | `features/farm` | ❌ |
| 84,1 | `@angular/router` | ✅ |
| 72,1 | `features/galpon` | ❌ |
| 69,8 | `crypto-js` | ✅ (lo usa el interceptor de auth) |
| 61,7 · 56,7 | `@angular/common` · `@angular/forms` | ✅ |
| 55,7 | `features/nucleo` | ❌ |
| 50,3 | `features/clientes` | ❌ |
| 47,7 | `features/auth` | ✅ (login es la primera pantalla) |
| 45,5 | `@fortawesome/free-solid-svg-icons` | ✅ ya podado |
| 27,7 · 26,8 · 22,9 · 21,9 · 9,4 | `lote-levante` · `silos` · `implementacion` · `tickets` · `vacunacion` | ❌ arrastrados por los de arriba |

**Causa**: `app/app.config.ts` importa de forma **estática** 25 componentes de pantalla y los cablea
con `component:`. Todo lo que un `component:` alcanza entra al bundle inicial. El propio archivo ya
tiene el antecedente y el aviso, escrito cuando el build **falló** por presupuesto:

> *«Empresas y Roles se cargan con `loadComponent` más abajo: importarlas acá las devolvería al
> bundle inicial, que es justo lo que hacía fallar el build por presupuesto.»*

O sea: la solución ya está decidida y probada en este mismo archivo; sólo se aplicó a 2 rutas.

## 2. Enfoque

Convertir a `loadComponent: () => import(...)` las rutas de **pantallas de administración y CRUD**,
y borrar sus imports estáticos. Es el patrón que la app ya usa en ~30 rutas.

**Qué se queda eager, a propósito:**
- `login` y `password-recovery` — es la primera pantalla; hacerla lazy agrega un viaje antes de
  poder escribir la contraseña, y en una tablet sin buena red eso se nota.
- `home` — es el aterrizaje inmediato del login.
- El shell (`app.component`, sidebar, interceptores, `crypto-js`, FontAwesome del layout).

**Qué pasa a lazy:**

| Ruta(s) | Componente |
|---|---|
| `profile` | `ProfileComponent` |
| `config` (padre) | `ConfigComponent` |
| `config/master-lists`, `.../new`, `.../:id` | `MasterListsComponent`, `ListDetailComponent` |
| `config/users` | `UserManagementComponent` |
| `config/countries` · `states` · `departments` · `cities` (+ `new`/`:id`) | los 8 de geografía |
| `config/farms-list` (+ `new`, `:id/edit`) | `FarmListComponent`, `FarmFormComponent` |
| `config/nucleos` (+ `new`, `:nucleoId`) | `NucleoListComponent`, `NucleoFormComponent` |
| `config/galpones` (+ `new`, `:galponId`) | `GalponListComponent`, `GalponFormComponent` |
| `config/lotes` | `LoteListComponent` |
| `config/guia-genetica` (+ `new`, `:id`, `:id/edit`) | `GuiaGeneticaList/Form/Detail` |
| `config/guia-genetica-ecuador` | `GuiaGeneticaEcuadorPageComponent` |
| `config/clientes` | `ClienteListComponent` |

## 3. Reglas y riesgos

1. **Sin cambio de comportamiento.** `loadComponent` monta el MISMO componente; sólo cambia cuándo
   llega el JavaScript. Guards (`authGuard`), `children`, `title` y paths quedan idénticos.
2. **Un componente que además esté en el `imports:` de otro componente eager sigue siendo eager**
   — no se rompe nada, sólo no se gana ese peso. Se mide después, no se supone.
3. **PWA**: el service worker precachea los chunks lazy declarados en `ngsw.json` (grupo `assets`
   de `ngsw-config.json`), así que **offline sigue funcionando**; lo que cambia es el orden de
   descarga, no la disponibilidad. Se verifica que `ngsw.json` siga listando los chunks.
4. **No se toca `angular.json`**: bajar el presupuesto o subir el techo sería tapar el problema.

## 4. Casos de prueba

- **T1** `yarn build`: el `initial` baja y el warning de 1,50 MB se reduce o desaparece. Se registra
  la cifra antes/después.
- **T2** `yarn test`: los 325 specs siguen verdes.
- **T3 (smoke de UI)**: navegar con la app corriendo a `config/lotes`, `config/farms-list`,
  `config/users` y `config/guia-genetica`, y comprobar que **pintan** y que el navegador pidió un
  chunk nuevo al entrar (prueba de que ahora son lazy y de que igual cargan).
- **T4**: `home` y `login` siguen sin pedir chunk extra (siguen eager).

## 5. Cambios de BD / SQL

**Ninguno.** Es 100 % front.

---

## 6. Resultado (17-ago-2026)

| | Antes | Después |
|---|---:|---:|
| **Initial total** | 1,85 MB | **967,45 kB** |
| `main.js` | 1.709.481 B | **829.457 B** |
| Transferencia (gzip) | — | 226,72 kB |
| Archivos de chunk | 118 | **183** |
| Margen contra el techo de 2,05 MB | ~210 kB | **~1,08 MB** |
| Advertencias del build | 1 (presupuesto) | **0** |

Es la primera vez que `yarn build` sale **sin una sola advertencia**.

**Verificación**
- **T1 ✔** cifras de arriba. `lote-list-component` (132,03 kB) y `user-management-component`
  (124,92 kB) ahora son chunks propios.
- **T2 ✔** `yarn test` **325 SUCCESS**, los mismos de antes.
- **T3 ✔** abren con datos reales: `config/lotes` (12 lotes), `config/farms-list` (29 granjas),
  `config/users` (56 usuarios), `config/guia-genetica` (889 registros), `config/countries` (3 países)
  y `profile`. En pestaña limpia, **0 errores de consola**.
- **T4 ✔** `home` sigue eager y carga igual.
- **PWA ✔** `ngsw.json` mantiene los **179 chunks** en el grupo `app` con `installMode: prefetch`: el
  service worker los sigue bajando todos, así que **offline no pierde nada**. Lo que cambia es el
  orden de descarga — la app queda interactiva antes y el resto llega detrás.

**Cerrado**: el 🟠 «Aire en el bundle» del bloque PWA §5.

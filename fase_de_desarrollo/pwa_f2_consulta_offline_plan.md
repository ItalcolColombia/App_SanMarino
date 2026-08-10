# Plan — PWA F2: consulta offline (la app deja de ser inútil sin red)

**Fecha:** 2026-08-09
**Estado:** ENTREGADO Y VALIDADO
**Depende de:** F1 (`pwa_f1_shell_plan.md`, commit `8ecb7c6`) y §5.2/§5.3 de `pwa_offline_first_plan.md`.

---

## 1. Qué falta para que la PWA sirva en campo

F1 dejó la app **instalable y con el shell funcionando sin red**: abre, navega, y `/diagnostico`
responde. Pero cualquier pantalla con datos queda vacía, porque todo va a `HttpClient` y sin red no
hay respuesta. En una granja sin señal eso es una app que arranca y no muestra nada.

F2 cierra eso: **lo que el operario ya consultó, lo puede volver a ver sin red.**

> **Riesgo de integridad: CERO.** Esto es solo lectura. No encola escrituras, no reenvía nada, no
> toca un saldo. La captura offline (F3) sigue bloqueada por F0.A/F0.B — de los cuales A1 y A2 ya
> están hechos (`44b2400`), y A3-A10 no.

---

## 2. Por qué NO se hace con el Service Worker

Es la solución que parece obvia y es la que rompe: un `dataGroup` de `ngsw-config.json` sobre
`/api/**`. **La caché del Service Worker indexa por URL e ignora los headers**, y en esta app la
empresa activa viaja en `X-Active-Company` / `X-Active-Company-Id`. Dos empresas distintas piden
`GET /api/Lote` con la **misma URL** y respuestas distintas: el operario de la empresa B recibiría la
respuesta cacheada de la empresa A. Es una fuga entre empresas, silenciosa y sin rastro.

Por eso la caché vive en **IndexedDB**, donde la clave la elegimos nosotros y puede incluir la
partición. `scripts/verificar-ngsw.js` ya falla el build si alguien agrega un `dataGroup`.

---

## 3. Diseño

### 3.1 Estrategia: red primero, caché solo como respaldo

Nunca se sirve de caché habiendo red. Una app de gestión que muestra números viejos cuando podía
mostrar los buenos es peor que una que tarda. La caché entra **solo** cuando la petición falla por
falta de red (`status === 0`).

### 3.2 Partición obligatoria, fail-closed

La clave de cada entrada es `{userId}|{companyId}|{paisId}|{método} {url}`. Si la sesión no tiene los
tres identificadores, **no se lee ni se escribe caché** (no se degrada a una clave parcial: eso es
exactamente cómo se filtran datos entre empresas). Mismo criterio que
`InventarioCatalogoScopeCalculos` en el backend.

### 3.3 Lista blanca, no lista negra

Solo se cachean **GET** de endpoints operativos explícitamente listados. Una lista negra deja
entrar todo lo que nadie se acordó de excluir.

**Excluido a propósito** (decisión D3 del plan madre: minimizar el dato en reposo, sin precios ni
facturación): `ReporteDiarioCostos*`, `ReporteContable`, `DbStudio`, `Auth`, `Users`, `Roles`,
`session`. Un reporte de costos en una tablet que se pierde es un problema distinto al de un
seguimiento diario.

### 3.4 TTL duro

- **≤ 16 h** — se sirve, marcando en la UI que es una consulta sin conexión.
- **> 16 h** — **no se sirve**. Se propaga el error de red.

Las 16 h son la jornada offline de la decisión D4. Pasado ese plazo, mostrar datos viejos sin que se
note es peor que no mostrar nada: el operario toma decisiones sobre saldos de hace días creyendo que
son de hoy.

### 3.5 Purga

Se borra la partición completa en **logout** y en **cambio de empresa**. No se espera al TTL: si el
dispositivo cambia de manos o de empresa, el dato anterior no tiene por qué seguir ahí.

### 3.6 Migraciones de esquema ACUMULATIVAS

IndexedDB entrega **un solo** `upgradeneeded` de v1 a v5 y nunca ejecuta los pasos intermedios si
están escritos como saltos. El handler itera `for (v = oldVersion + 1; v <= newVersion; v++)`. Con
test que abre en v1, salta a vN y verifica el esquema.

---

## 4. Archivos

```
frontend/src/app/shared/offline/
├── models/offline.model.ts
├── funciones/
│   ├── README.md
│   ├── clave-particion.funcion.ts       # fail-closed
│   ├── decidir-cacheable.funcion.ts     # lista blanca + solo GET
│   └── vigencia-cache.funcion.ts        # TTL duro
├── offline-db.ts                        # IndexedDB + migraciones acumulativas
├── cache-consultas.service.ts           # orquestador
└── offline-cache.interceptor.ts         # el seam
```

Modificados: `app.config.ts` (interceptor), `token-storage.service.ts` (purga en logout y cambio de
empresa), `pwa-barra-estado` (aviso "viendo datos guardados"), `diagnostico` (estado de la caché).

---

## 5. Casos de prueba

**Puros (Karma)**
1. `claveParticion`: sesión completa ⇒ clave; sin `userId` / sin `companyId` / sin `paisId` ⇒ `null`.
2. Dos empresas distintas ⇒ claves distintas para la **misma** URL (el caso de la fuga).
3. `decidirCacheable`: GET de endpoint listado ⇒ sí; POST ⇒ no; GET de `ReporteDiarioCostos` ⇒ no;
   endpoint no listado ⇒ no.
4. `vigenciaCache`: 0 h ⇒ vigente; 15 h 59 ⇒ vigente; 16 h 01 ⇒ vencida; timestamp futuro ⇒ vencida.
5. Migración de IndexedDB: abrir en v1, saltar a v3, verificar que corrieron **todos** los pasos.

**En vivo**
6. Con red: la respuesta viene de la red y se guarda.
7. **Sin red**: la misma pantalla se sirve de la caché y la UI lo dice.
8. Sin red y sin caché previa: error de red normal (no una pantalla en blanco mentirosa).
9. Cambio de empresa ⇒ la partición anterior desaparece.
10. Logout ⇒ la base queda vacía.

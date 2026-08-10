# Plan — PWA: alistamiento para campo (persistencia de cuota + regla dura de D6)

**Fecha:** 2026-08-10
**Contexto:** F1 (shell instalable) y F2 (consulta offline) están **construidas y probadas**, pero
**no desplegadas**: producción sirve el build del 07-ago y `ngsw.json` / `manifest.webmanifest` /
`ngsw-worker.js` responden **404**. Verificado con `curl` contra el ALB, no supuesto.

Antes de entregar tablets a la granja quedan huecos que no son de código de la PWA sino de
**alistamiento**. Este plan cierra los dos que protegen datos y que no dependen de nadie más.

---

## 1. Los dos huecos que se cierran

### H1 — Nadie pide que el almacenamiento sea persistente

`/diagnostico` ya **informa** `navigator.storage.persisted()`, pero **nadie llama nunca a
`persist()`**. Sin esa concesión, la base de la consulta offline vive en almacenamiento *best-effort*:
el navegador puede **desalojarla** cuando el dispositivo se quede sin espacio, y el operario se entera
en la granja, sin red y sin datos.

Es el modo de falla más caro de todos los que quedan porque **no avisa**: no hay error, no hay log; la
pantalla simplemente aparece vacía como si nunca se hubiera consultado.

Pedir la persistencia es **no destructivo**: si el navegador la deniega, todo sigue igual que hoy.

### H2 — D6 no está implementado: una cuenta multiempresa se baja el snapshot de todas

La decisión **D6** del plan madre dice, textual: *opt-in por rol y por dispositivo, y **prohibido para
cuentas con alcance global/multiempresa** (un super admin bajaría el snapshot de todas las empresas)*.
Hoy no hay nada de eso: `grep` sobre el front no encuentra ningún gate.

**Por qué la partición no alcanza.** `claveParticion` evita que una sesión **lea** lo de otra, y eso
funciona. Pero no evita que **el mismo dispositivo acumule** los datos de todas las empresas que ese
usuario visita, cada una en su partición. Como se decidió **no cifrar** el dato en reposo (D3), un
dispositivo perdido expone lo de todas. La partición protege contra la fuga *entre sesiones*, no
contra la pérdida del equipo — son amenazas distintas y solo una está cubierta.

---

## 2. Alcance: la mitad de D6 que NO depende de una migración

D6 tiene dos partes:

| Parte | Qué necesita | En este plan |
|---|---|---|
| **Prohibición** para cuentas globales/multiempresa | Nada: la sesión ya trae `isSuperAdmin`, `hasMultipleCompanies` y `companyIds` | ✅ **Sí** |
| **Opt-in** por rol y por dispositivo | Flag en BD + registro de dispositivos (que hoy no existe) | ❌ No — queda para cuando exista la telemetría de flota |

Se hace la parte que **protege datos** y que se puede resolver con lo que la sesión ya tiene. El
opt-in es una comodidad de despliegue; la prohibición es la que evita el snapshot multiempresa.

---

## 3. Diseño

### H1 — `AlmacenamientoPersistenteService`

`core/pwa/almacenamiento-persistente.service.ts`, con la decisión en función pura
(`funciones/decidir-pedir-persistencia.funcion.ts`).

- Se pide **una sola vez por sesión de app** y solo si `persisted()` devuelve `false`: volver a
  pedirlo cuando ya está concedido es una llamada al pedo, y en Firefox reabre el prompt.
- **Nunca rompe nada**: todo va en `try/catch` y un rechazo se registra como estado, no como error.
  La app funciona igual sin persistencia — solo queda expuesta al desalojo.
- Se dispara **cuando hay sesión**, no en el arranque en frío: Chrome concede la persistencia según el
  *engagement* del sitio (y automáticamente si la app está instalada), así que pedirla antes del login
  es donde más probable es que la denieguen.
- El resultado se refleja en `/diagnostico`, que ya tiene el campo.

### H2 — `decidirCacheOffline(sesion)` puro + purga

`shared/offline/funciones/decidir-cache-offline.funcion.ts`:

```
sin sesión                      ⇒ NO   (fail-closed)
isSuperAdmin === true           ⇒ NO   (alcance global)
más de una empresa en la cuenta ⇒ NO   (D6: nada de snapshot multiempresa)
resto                           ⇒ SÍ
```

"Más de una empresa" se evalúa por **las tres señales disponibles** (`hasMultipleCompanies`,
`companyIds`, `companies`), y basta que **una** diga que hay varias. No se exige que coincidan: son
campos que el backend llena por caminos distintos y el criterio conservador es el correcto cuando de
lo que se trata es de no bajar datos de más.

Se aplica en `offlineCacheInterceptor` **antes de guardar y antes de leer**.

🔴 **Y hay que purgar lo ya guardado.** Un gate que solo impide *escribir* deja intacto lo que la
cuenta multiempresa haya cacheado antes del cambio, y se lo sigue sirviendo. Al detectar una cuenta no
elegible se purga su caché — si no, el cambio da una falsa sensación de cierre.

---

## 4. Casos de prueba

**`decidirPedirPersistencia`:** ya concedida ⇒ no pedir · no concedida ⇒ pedir · API ausente ⇒ no
pedir y no romper · ya pedida en esta sesión ⇒ no repetir.

**`decidirCacheOffline`:** sin sesión ⇒ no · super admin ⇒ no · `hasMultipleCompanies` ⇒ no ·
`companyIds` con 2+ ⇒ no · `companies` con 2+ ⇒ no · señales en conflicto (una dice varias) ⇒ **no**
· usuario normal de una sola empresa ⇒ **sí** (el caso que debe seguir funcionando).

**Integración (IndexedDB real):** una cuenta multiempresa no guarda, no sirve caché previa, y su
caché queda purgada.

**Transversal:** `yarn build` y `yarn test` sin regresión (base **199** verdes).

---

## 5. Criterio de cierre

- La app pide persistencia una vez, con sesión, y `/diagnostico` lo muestra.
- Ninguna cuenta con alcance global o multiempresa guarda ni lee caché offline, y la que tuviera queda purgada.
- Un operario de una sola empresa sigue viendo su consulta offline exactamente como hoy.

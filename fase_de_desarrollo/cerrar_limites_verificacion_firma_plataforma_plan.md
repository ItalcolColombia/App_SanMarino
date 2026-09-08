# Cerrar los límites de verificación de la firma de plataforma

> Los gates y el smoke de la auditoría (commits `c1777db`…`1738515`) dejaron tres límites declarados.
> Dos son **deuda de verificación real y se corrigen acá**. El tercero **no es un defecto**: es una
> propiedad del diseño, y lo accionable es habilitar la Fase B.
>
> Regla rectora: **refactor ≠ cambio de comportamiento.** La firma que se emite y la que se acepta
> tienen que ser byte a byte las mismas antes y después. Si algo de esto cambia un valor, está mal.

---

## Límite 1 — el criterio 7 verifica TEXTO, no semántica

**El problema.** `verificar-superficie-produccion.js` comprueba con expresiones regulares que
`AuthService` derive la firma de `jti.ToString()`. Eso tiene dos fallas opuestas:

- **Falso positivo:** renombrar la variable `jti`, o extraer su generación a un helper, rompe el gate
  sin que nada esté roto. Alguien con prisa lo "arregla" borrando el criterio.
- **Falso negativo:** el regex mira la *forma*, no el *valor*. Un cambio que conserve la forma y
  altere el insumo real pasaría.

**La corrección.** Mover la invariante a donde se pueda **ejecutar**, siguiendo la regla del repo
(cálculo puro en `Application/Calculos/` + xUnit):

1. Nueva `Application/Calculos/SesionTokenCalculos.cs` (`static class`) con
   `ConstruirDatosDeSesion(Guid jti, string? derivationKey)` → record
   `DatosSesion(string JtiClaim, string? PlatformKey)`. Un solo lugar produce **ambos** valores a
   partir del **mismo** `jti`; `PlatformKey` sale de `PlatformSecretCalculos.DerivarClaveSesion`
   (no se duplica la fórmula).
2. `AuthService.GenerateResponseAsync` la llama una vez y usa `datos.JtiClaim` para el claim
   `JwtRegisteredClaimNames.Jti` y `datos.PlatformKey` para la respuesta. El `jti` sigue siendo el
   mismo `Guid` que va a `sesiones_activas`.
3. Tests `tests/ZooSanMarino.Application.Tests/SesionTokenCalculosTests.cs`:
   - **La invariante, ejecutada:** para N `jti` al azar, `DerivarClaveSesion(datos.JtiClaim, key)`
     ⇒ exactamente `datos.PlatformKey`. Esto es lo que el regex sólo podía suponer.
   - Ida y vuelta con el middleware: `LeerJtiDeAuthorizationHeader` sobre un JWT que lleve
     `datos.JtiClaim` devuelve ese mismo `jti` y deriva la misma firma.
   - `derivationKey` nula/vacía ⇒ `PlatformKey == null` y `JtiClaim` intacto (camino legacy).
   - `JtiClaim` == `jti.ToString()` (formato "D", minúsculas, con guiones) — congela el formato, que
     es lo que viaja en el token y lo que se vuelve a leer.
4. **El criterio 7 se reescribe, no se borra.** Deja de mirar la forma de `AuthService` y pasa a
   exigir lo que sí es estructural y estable: que `AuthService` obtenga los dos valores de
   `SesionTokenCalculos` (un solo productor), y que ni emisor ni verificador construyan su propio
   HMAC. Comentario nuevo explicando que la invariante la prueba el test y esto sólo cuida que nadie
   se salga del carril.

**Cómo se prueba que sirve:** mutar `SesionTokenCalculos` para derivar de otro insumo ⇒ el **test**
falla (hoy sólo fallaba el regex).

---

## Límite 2 — no existe prueba del login completo

**El problema.** El smoke (`smoke-firma-plataforma.js`, 8/8) prueba que el middleware **valida bien
lo que se le manda**, con un JWT fabricado. Nadie ejecutó `login real → firma emitida → request
aceptada`. Es el único tramo donde emisor y verificador se encuentran de verdad.

**La corrección.** Extender el smoke con un **bloque opcional de login real**, que se activa sólo si
hay credenciales en el entorno:

```bash
SMOKE_EMAIL=... SMOKE_PASSWORD=... node backend/scripts/smoke-firma-plataforma.js
```

- Sin esas variables: el script corre los 8 casos de hoy e **informa** que el tramo del login quedó
  sin ejercitar (no falla: sigue sirviendo como está).
- Con ellas: cifra el cuerpo del login como espera el backend (AES-256-CBC + PKCS7, IV al frente,
  PBKDF2-SHA256 sal `sanmarino-salt` 10k — ya replicado en el script), descifra la respuesta con la
  llave del remitente-backend, y verifica:
  1. la respuesta trae `platformKey` no vacío;
  2. `GET /api/Company` con `X-Secret-Up: <platformKey>` + `Bearer <token>` ⇒ **no** es 401
     `platform-secret` (el tramo completo funciona);
  3. `platformKey` == `HMAC(DerivationKey, jti del token recibido)` — el emisor calculó lo mismo que
     el verificador espera, medido sobre datos reales;
  4. el `platformKey` de esa sesión con el token de **otra** ⇒ 401 `platform-secret`.

**Las credenciales nunca se escriben en el repo ni en el log.** Van por variable de entorno, y el
script no las imprime.

**Riesgo a cuidar:** el login **escribe** (`sesiones_activas`, `last_login`, contadores de intento).
Se corre contra la BD **local**, nunca contra prod, y el script lo dice en su encabezado y aborta si
el host no es `localhost`.

---

## Límite 3 — la firma no es autenticación (NO es un defecto)

Es un **filtro de origen**, por diseño, y así está documentado en la respuesta al analista. No hay
nada que "corregir": la autenticación real es el JWT por usuario + authz deny-by-default + alcance
multiempresa fail-closed, y ninguno depende de la firma.

Lo único accionable es **destrabar la Fase B** (retirar el secreto estático del bundle), que el plan
`llave_plataforma_por_sesion_plan.md` condiciona a *"cuando legacy-web ≈ 0 durante 48 h"* — una
medición que **hoy no existe**, así que la Fase B no puede decidirse.

**Propuesta (NO se implementa en esta tanda; requiere tu OK):** contador en
`PlatformSecretMiddleware` por camino resuelto (derivada / legacy-web / legacy-móvil / rechazo),
expuesto donde se pueda leer sin abrir superficie nueva. Decisión pendiente: dónde. Un endpoint nuevo
es superficie; un log estructurado periódico no, pero exige mirar CloudWatch. **Lo dejo planteado, no
hecho.**

---

## Lo que NO se toca

- El comportamiento del middleware, del login y de la firma: idénticos byte a byte.
- La tanda ya validada (gates 1-6, smoke de 8 casos, checks del borde).
- `main-produccion`: ⚠️ ya tiene el merge preparado sin pushear. **Este trabajo lo deja
  desactualizado** ⇒ hay que rehacer el merge antes del deploy. Nadie commitea a esa rama acá.

## Validación exigida

`dotnet build` 0 err / 0 warn · `dotnet test` sin regresiones (hoy: 4030 pass) ·
`node backend/scripts/verificar-superficie-produccion.js` 7/7 · el smoke 8/8 sin credenciales ·
prueba negativa del test nuevo (mutar el cálculo ⇒ el test falla, revertir).

⚠️ **Compilar con `-p:UseSharedCompilation=false -m:1` y arrancar con `--no-build`.** El compilador
compartido se infla a ~7 GB con este repo y cuelga la máquina; pasó el 8-sep-2026 y obligó a matar
todo. Nunca dos builds a la vez.

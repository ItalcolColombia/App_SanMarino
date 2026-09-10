#!/usr/bin/env node
/**
 * Smoke del filtro de origen `X-Secret-Up` — los DOS caminos, SIN credenciales de usuario.
 *
 * Por qué existe
 * --------------
 * La Fase A de la firma de plataforma por sesión (commit `9da8296`) quedó con su smoke marcado como
 * "pendiente — lo corre el usuario, requiere un usuario válido" en el tracker. Eso bloqueaba su
 * despliegue: el cambio toca `PlatformSecretMiddleware`, por donde pasa TODA petición, así que si
 * falla no falla una pantalla — no entra nadie.
 *
 * El truco que lo destraba: el middleware lee el `jti` del Bearer **sin validar la firma del JWT**
 * (la validación real la hace `OnTokenValidated` unos ms después, y B1 comprueba `sesiones_activas`).
 * Eso permite ejercitar el filtro fabricando un JWT con un `jti` arbitrario. Si el filtro acepta la
 * firma, el rechazo deja de ser `platform-secret` y pasa a ser de autenticación: son DOS 401
 * distintos, separados por la cabecera `X-Auth-Failure`. Esa distinción es justamente el contrato
 * que el middleware documenta, y es lo que este smoke mide.
 *
 * Qué cubre
 * ---------
 *   Camino nuevo (firma derivada del `jti`): correcta ⇒ pasa · de otro `jti` ⇒ 401 · basura ⇒ 401 ·
 *   sin Bearer ⇒ 401 · sin header ⇒ 401.
 *   Camino legacy (secreto estático cifrado, el que usan las sesiones YA ABIERTAS): válido ⇒ pasa ·
 *   secreto equivocado ⇒ 401 · llave equivocada ⇒ 401.
 *
 * Qué NO cubre (sin credenciales)
 * --------------------------------
 * Que el `platformKey` que emite `AuthService` en el login coincida con lo que el middleware espera.
 * Eso necesita un login real con un usuario válido. Sin credenciales, acá se verifica el otro
 * extremo —que el middleware valide bien lo que se le mande— y que la fórmula sea la misma que
 * `PlatformSecretCalculos.DerivarClaveSesion`.
 *
 * Bloque OPCIONAL de login real (Límite 2)
 * -----------------------------------------
 * Se activa SOLO si el entorno trae `SMOKE_EMAIL` y `SMOKE_PASSWORD`:
 *
 *   SMOKE_EMAIL=... SMOKE_PASSWORD=... node backend/scripts/smoke-firma-plataforma.js
 *
 * Sin esas variables, el script corre los 8 casos de siempre e INFORMA (sin fallar) que el tramo del
 * login quedó sin ejercitar. Con ellas, cifra el body del login como espera el backend
 * (`EncryptionService.Encrypt`, con `Encryption:RemitenteFrontend`), lo manda a `POST /api/Auth/login`,
 * descifra la respuesta con `Encryption:RemitenteBackend`, y verifica que `platformKey` no esté vacío,
 * que habilite `GET /api/Company` sin el 401 `platform-secret`, que sea exactamente
 * `HMAC(DerivationKey, jti del token recibido)` medido sobre datos reales, y que ESE `platformKey` con
 * el token de OTRA sesión sea rechazado. Las credenciales nunca se imprimen, ni la contraseña ni el
 * token completo.
 *
 * ⚠️ El login ESCRIBE en la BD (`sesiones_activas`, `last_login`, contadores de intento): el bloque
 * aborta si el host configurado no es `localhost`/`127.0.0.1`. Nunca contra prod.
 *
 * Cómo se corre
 * -------------
 *   1. Postgres local en :5433 arriba.
 *   2. Backend en :5002 con `ASPNETCORE_ENVIRONMENT=Development`.
 *      ⚠️ Compilá ANTES por separado (`dotnet build -p:UseSharedCompilation=false -m:1`) y arrancá
 *      con `dotnet run --no-build`: el compilador compartido se infla a 7 GB con este repo y
 *      cuelga la máquina (ver [[builds-concurrentes-se-traban]]).
 *   3. `node backend/scripts/smoke-firma-plataforma.js` (agregá `SMOKE_EMAIL`/`SMOKE_PASSWORD` para
 *      el bloque del login real).
 *   4. Apagá el backend y confirmá que :5002 quedó libre.
 *
 * Las llaves NO están escritas acá: se leen de `appsettings.Development.json` en tiempo de ejecución.
 * Ver la nota junto a `leerConfiguracion()` — resumen: esos valores hoy coinciden con los de
 * producción, así que copiarlos a un tercer archivo sería empeorar un problema que ya existe.
 */

const crypto = require('crypto');
const http = require('http');
const fs = require('fs');
const path = require('path');

const BASE = { host: 'localhost', port: 5002 };
const RUTA = '/api/Company';

// ─────────────────────────────────────────────────────────────────────────────
// Las llaves se LEEN de appsettings.Development.json, no se copian acá.
// ─────────────────────────────────────────────────────────────────────────────
// Medido el 8-sep-2026: `Encryption:RemitenteFrontend`, `Encryption:RemitenteBackend`,
// `PlatformSecret:SecretUpFrontend` y `PlatformSecret:EncryptionKey` tienen EL MISMO VALOR en
// `appsettings.Development.json` y en `appsettings.json` — o sea, las de "dev" son también las de
// producción. (Ese es un problema aparte, ya conocido: los secretos están versionados en el repo.
// No lo arregla este script.)
//
// Lo que sí evita este script es EMPEORARLO: copiarlas acá las duplicaría en un tercer archivo,
// las dejaría desincronizadas el día que se roten, y sumaría un lugar más de donde extraerlas.
// Leerlas en tiempo de ejecución mantiene una sola fuente y hace que el smoke siga funcionando
// después de una rotación, sin tocar nada.
const RUTA_APPSETTINGS = path.resolve(__dirname, '..', 'src', 'ZooSanMarino.API', 'appsettings.Development.json');

function leerConfiguracion() {
  if (!fs.existsSync(RUTA_APPSETTINGS)) {
    console.error(`\n❌ No se encontró ${RUTA_APPSETTINGS}. El smoke lee de ahí las llaves de DEV.`);
    process.exit(1);
  }
  let crudo = fs.readFileSync(RUTA_APPSETTINGS, 'utf8');
  if (crudo.charCodeAt(0) === 0xfeff) crudo = crudo.slice(1); // BOM
  let cfg;
  try {
    cfg = JSON.parse(crudo);
  } catch (e) {
    console.error(`\n❌ ${RUTA_APPSETTINGS} no es JSON válido: ${e.message}`);
    process.exit(1);
  }

  const faltantes = [];
  const leer = (ruta) => {
    const valor = ruta.split(':').reduce((o, k) => (o == null ? undefined : o[k]), cfg);
    if (typeof valor !== 'string' || valor.length === 0) faltantes.push(ruta);
    return valor;
  };

  const valores = {
    derivationKey: leer('PlatformSecret:DerivationKey'),
    jwtKey: leer('JwtSettings:Key'),
    secretUp: leer('PlatformSecret:SecretUpFrontend'),
    encKey: leer('PlatformSecret:EncryptionKey'),
    remitenteFrontend: leer('Encryption:RemitenteFrontend'),
    remitenteBackend: leer('Encryption:RemitenteBackend'),
  };

  if (faltantes.length > 0) {
    console.error(`\n❌ Faltan claves en appsettings.Development.json: ${faltantes.join(', ')}`);
    process.exit(1);
  }
  return valores;
}

const CFG = leerConfiguracion();
const DERIVATION_KEY = CFG.derivationKey;
const JWT_KEY = CFG.jwtKey;
const SECRET_UP = CFG.secretUp;
const ENC_KEY = CFG.encKey;

// Lo que viaja FRENTE -> back se cifra con RemitenteFrontend; lo que vuelve back -> frente, con
// RemitenteBackend (ver EncryptionService).
const REMITENTE_FRONTEND_KEY = CFG.remitenteFrontend;
const REMITENTE_BACKEND_KEY = CFG.remitenteBackend;

const b64url = (buf) => Buffer.from(buf).toString('base64')
  .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** JWT HS256 con el `jti` pedido, firmado con la clave de dev. */
function hacerJwt(jti) {
  const header = b64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const ahora = Math.floor(Date.now() / 1000);
  const payload = b64url(JSON.stringify({
    jti, sub: 'smoke', iss: 'ZooSanMarino.API', aud: 'ZooSanMarino.Client',
    iat: ahora, exp: ahora + 3600,
  }));
  const firma = b64url(crypto.createHmac('sha256', JWT_KEY).update(`${header}.${payload}`).digest());
  return `${header}.${payload}.${firma}`;
}

/** Misma fórmula que PlatformSecretCalculos.DerivarClaveSesion. */
const derivar = (jti) =>
  crypto.createHmac('sha256', DERIVATION_KEY).update(jti, 'utf8').digest('base64');

/** Misma fórmula que EncryptionService.Encrypt: AES-256-CBC + PKCS7, IV al frente, PBKDF2 con sal fija. */
function cifrar(texto, keyString) {
  const key = crypto.pbkdf2Sync(keyString, Buffer.from('sanmarino-salt', 'utf8'), 10000, 32, 'sha256');
  const iv = crypto.randomBytes(16);
  const c = crypto.createCipheriv('aes-256-cbc', key, iv);
  return Buffer.concat([iv, c.update(Buffer.from(texto, 'utf8')), c.final()]).toString('base64');
}

/**
 * Inversa exacta de `cifrar` / de `EncryptionService.Decrypt`: mismo derivado PBKDF2, IV leído de
 * los primeros 16 bytes del blob (no de un campo aparte), resto = ciphertext AES-256-CBC/PKCS7.
 */
function descifrar(base64, keyString) {
  const key = crypto.pbkdf2Sync(keyString, Buffer.from('sanmarino-salt', 'utf8'), 10000, 32, 'sha256');
  const datos = Buffer.from(base64, 'base64');
  const iv = datos.subarray(0, 16);
  const cifrado = datos.subarray(16);
  const d = crypto.createDecipheriv('aes-256-cbc', key, iv);
  return Buffer.concat([d.update(cifrado), d.final()]).toString('utf8');
}

function pedir(headers) {
  return new Promise((resolve) => {
    const req = http.request({ ...BASE, path: RUTA, method: 'GET', headers }, (res) => {
      let cuerpo = '';
      res.on('data', (d) => (cuerpo += d));
      res.on('end', () => resolve({
        status: res.statusCode,
        motivo: res.headers['x-auth-failure'] ?? null,
        cuerpo: cuerpo.slice(0, 160),
      }));
    });
    req.on('error', (e) => resolve({ status: 0, motivo: `ERROR: ${e.message}`, cuerpo: '' }));
    req.end();
  });
}

/**
 * Como `pedir`, pero para POST con body JSON y SIN truncar la respuesta — el bloque de login
 * necesita el `cuerpo` completo (es el payload cifrado, no cabe en 160 caracteres).
 */
function pedirPost(path, headers, bodyObj) {
  return new Promise((resolve) => {
    const data = Buffer.from(JSON.stringify(bodyObj), 'utf8');
    const req = http.request({
      ...BASE,
      path,
      method: 'POST',
      headers: { ...headers, 'Content-Type': 'application/json', 'Content-Length': data.length },
    }, (res) => {
      let cuerpo = '';
      res.on('data', (d) => (cuerpo += d));
      res.on('end', () => resolve({
        status: res.statusCode,
        motivo: res.headers['x-auth-failure'] ?? null,
        cuerpo,
      }));
    });
    req.on('error', (e) => resolve({ status: 0, motivo: `ERROR: ${e.message}`, cuerpo: '' }));
    req.write(data);
    req.end();
  });
}

let fallos = 0;
function afirmar(descripcion, ok, detalle) {
  console.log(`  ${ok ? 'OK   ' : 'FALLA'} ${descripcion}`);
  if (!ok) { console.log(`        ${detalle}`); fallos++; }
}

/** `jti` del payload de un JWT, SIN validar la firma — mismo criterio que el middleware. */
function jtiDelJwt(jwt) {
  const partes = jwt.split('.');
  if (partes.length !== 3) return null;
  const s = partes[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = s.length % 4 === 0 ? '' : '='.repeat(4 - (s.length % 4));
  const payload = JSON.parse(Buffer.from(s + pad, 'base64').toString('utf8'));
  return payload.jti ?? null;
}

/**
 * Límite 2 — bloque OPCIONAL de login real. Sólo corre si SMOKE_EMAIL y SMOKE_PASSWORD están en el
 * entorno; si no, informa (sin fallar) que el tramo del login quedó sin ejercitar. Es el único tramo
 * donde emisor (AuthService, vía SesionTokenCalculos) y verificador (PlatformSecretMiddleware) se
 * encuentran de verdad, con un usuario real en vez de un JWT fabricado.
 */
async function bloqueLoginReal() {
  const email = process.env.SMOKE_EMAIL;
  const password = process.env.SMOKE_PASSWORD;

  console.log('\n== Bloque OPCIONAL: login real (emisor <-> verificador) ==');

  if (!email || !password) {
    console.log('  (SMOKE_EMAIL/SMOKE_PASSWORD no están en el entorno -> tramo del login SIN EJERCITAR)');
    return;
  }

  // El login ESCRIBE en la BD (sesiones_activas, last_login, contadores de intento). Nunca contra prod.
  if (BASE.host !== 'localhost' && BASE.host !== '127.0.0.1') {
    console.error(`\n❌ El host configurado (${BASE.host}) no es localhost. Este bloque hace un login ` +
      'real que escribe en la base de datos (sesiones_activas, last_login, contadores de intento): ' +
      'sólo puede correr contra la BD local, nunca contra prod.');
    process.exit(1);
  }

  const cuerpoLogin = cifrar(JSON.stringify({ email, password }), REMITENTE_FRONTEND_KEY);
  const respuestaLogin = await pedirPost('/api/Auth/login', {}, { EncryptedData: cuerpoLogin });

  if (respuestaLogin.status !== 200) {
    afirmar('login real -> responde 200', false,
      `status=${respuestaLogin.status} (credenciales de SMOKE_EMAIL/SMOKE_PASSWORD correctas? ` +
      'usuario existe en la BD local?)');
    return;
  }
  afirmar('login real -> responde 200', true);

  let dto;
  try {
    dto = JSON.parse(descifrar(respuestaLogin.cuerpo, REMITENTE_BACKEND_KEY));
  } catch (e) {
    afirmar('la respuesta del login se descifra y parsea como JSON', false, e.message);
    return;
  }

  afirmar('platformKey de la respuesta no está vacío',
    typeof dto.platformKey === 'string' && dto.platformKey.length > 0,
    dto.platformKey === null
      ? 'platformKey vino null (¿falta PlatformSecret:DerivationKey en appsettings.Development.json?)'
      : `tipo=${typeof dto.platformKey}`);

  if (typeof dto.token !== 'string' || !dto.platformKey) {
    console.log('  (sin token o sin platformKey -> se detiene acá el bloque del login)');
    return;
  }

  const jtiReal = jtiDelJwt(dto.token);

  const respuestaCompany = await pedir({ Authorization: `Bearer ${dto.token}`, 'X-Secret-Up': dto.platformKey });
  afirmar('GET /api/Company con el platformKey de la sesión real -> NO es 401 platform-secret',
    respuestaCompany.motivo !== 'platform-secret',
    `status=${respuestaCompany.status} motivo=${respuestaCompany.motivo}`);

  const esperado = jtiReal ? derivar(jtiReal) : null;
  afirmar('platformKey == HMAC(DerivationKey, jti del token recibido) — medido sobre datos reales',
    jtiReal !== null && dto.platformKey === esperado,
    jtiReal === null
      ? 'no se pudo leer el jti del token recibido'
      : 'el platformKey emitido no coincide con la fórmula esperada (no se imprime ninguno de los ' +
        'dos: la comparación ya dice si coinciden)');

  // El platformKey de ESTA sesión no puede servir para un token de OTRA sesión.
  const jtiDeOtraSesion = crypto.randomUUID();
  const jwtDeOtraSesion = hacerJwt(jtiDeOtraSesion);
  const respuestaCruzada = await pedir({ Authorization: `Bearer ${jwtDeOtraSesion}`, 'X-Secret-Up': dto.platformKey });
  afirmar('el platformKey de esta sesión con el token de OTRA -> 401 platform-secret',
    respuestaCruzada.status === 401 && respuestaCruzada.motivo === 'platform-secret',
    `status=${respuestaCruzada.status} motivo=${respuestaCruzada.motivo}`);
}

(async () => {
  const salud = await new Promise((res) => {
    const r = http.request({ ...BASE, path: '/health', method: 'GET' }, (x) => res(x.statusCode));
    r.on('error', () => res(0)); r.end();
  });
  if (salud !== 200) {
    console.error(`\n❌ El backend no responde en :${BASE.port}. Levantalo antes (ver el encabezado).`);
    process.exit(1);
  }

  const jti = crypto.randomUUID();
  const otroJti = crypto.randomUUID();
  const jwt = hacerJwt(jti);

  console.log('\n== Camino NUEVO: firma derivada del jti ==');

  const a = await pedir({});
  afirmar('sin X-Secret-Up -> 401 del filtro de origen',
    a.status === 401 && a.motivo === 'platform-secret',
    `status=${a.status} motivo=${a.motivo} ${a.cuerpo}`);

  const b = await pedir({ Authorization: `Bearer ${jwt}`, 'X-Secret-Up': derivar(jti) });
  afirmar('firma derivada CORRECTA -> el filtro la acepta (el 401 ya no es platform-secret)',
    b.motivo !== 'platform-secret',
    `status=${b.status} motivo=${b.motivo} ${b.cuerpo}`);

  const c = await pedir({ Authorization: `Bearer ${jwt}`, 'X-Secret-Up': derivar(otroJti) });
  afirmar('firma derivada de OTRO jti -> rechazada',
    c.status === 401 && c.motivo === 'platform-secret',
    `status=${c.status} motivo=${c.motivo}`);

  const d = await pedir({ Authorization: `Bearer ${jwt}`, 'X-Secret-Up': 'firma-inventada-que-no-descifra' });
  afirmar('firma basura -> rechazada',
    d.status === 401 && d.motivo === 'platform-secret',
    `status=${d.status} motivo=${d.motivo}`);

  const e = await pedir({ 'X-Secret-Up': derivar(jti) });
  afirmar('firma derivada SIN Bearer -> rechazada (no hay jti del que derivar)',
    e.status === 401 && e.motivo === 'platform-secret',
    `status=${e.status} motivo=${e.motivo}`);

  console.log('\n== Camino LEGACY: secreto estatico cifrado (sesiones YA abiertas) ==');

  const f = await pedir({ 'X-Secret-Up': cifrar(SECRET_UP, ENC_KEY) });
  afirmar('secreto estatico VALIDO -> el filtro lo acepta (no hay corte de servicio)',
    f.motivo !== 'platform-secret',
    `status=${f.status} motivo=${f.motivo} ${f.cuerpo}`);

  const g = await pedir({ 'X-Secret-Up': cifrar('secreto-que-no-es', ENC_KEY) });
  afirmar('secreto estatico INVALIDO (bien cifrado) -> rechazado',
    g.status === 401 && g.motivo === 'platform-secret',
    `status=${g.status} motivo=${g.motivo}`);

  const h = await pedir({ 'X-Secret-Up': cifrar(SECRET_UP, 'llave-equivocada') });
  afirmar('cifrado con llave equivocada -> rechazado',
    h.status === 401 && h.motivo === 'platform-secret',
    `status=${h.status} motivo=${h.motivo}`);

  await bloqueLoginReal();

  console.log(`\n${fallos === 0 ? '✅' : '❌'} fallos=${fallos}`);
  process.exit(fallos === 0 ? 0 : 1);
})();

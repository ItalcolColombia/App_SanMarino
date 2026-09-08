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
 * Qué NO cubre
 * ------------
 * Que el `platformKey` que emite `AuthService` en el login coincida con lo que el middleware espera.
 * Eso necesita un login real con un usuario válido. Acá se verifica el otro extremo —que el
 * middleware valide bien lo que se le mande— y que la fórmula sea la misma que
 * `PlatformSecretCalculos.DerivarClaveSesion`.
 *
 * Cómo se corre
 * -------------
 *   1. Postgres local en :5433 arriba.
 *   2. Backend en :5002 con `ASPNETCORE_ENVIRONMENT=Development`.
 *      ⚠️ Compilá ANTES por separado (`dotnet build -p:UseSharedCompilation=false -m:1`) y arrancá
 *      con `dotnet run --no-build`: el compilador compartido se infla a 7 GB con este repo y
 *      cuelga la máquina (ver [[builds-concurrentes-se-traban]]).
 *   3. `node backend/scripts/smoke-firma-plataforma.js`
 *   4. Apagá el backend y confirmá que :5002 quedó libre.
 *
 * Los valores de abajo son los de `appsettings.Development.json` — claves de DEV, nunca de prod.
 */

const crypto = require('crypto');
const http = require('http');

const BASE = { host: 'localhost', port: 5002 };
const RUTA = '/api/Company';

const DERIVATION_KEY = 'DevOnly#DerivationKey#NOT-FOR-PROD';
const JWT_KEY = 'ZooSanMarino_SecretKey_For_Development_Only_Not_For_Production_Use_This_Is_Very_Long_Key_For_Security';
const SECRET_UP = 'Fr0nt#SeCr3t!SanM@r1n0X2';
const ENC_KEY = 'EncKey#SANMARINO2024!xZ9';

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

let fallos = 0;
function afirmar(descripcion, ok, detalle) {
  console.log(`  ${ok ? 'OK   ' : 'FALLA'} ${descripcion}`);
  if (!ok) { console.log(`        ${detalle}`); fallos++; }
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

  console.log(`\n${fallos === 0 ? '✅' : '❌'} fallos=${fallos}`);
  process.exit(fallos === 0 ? 0 : 1);
})();

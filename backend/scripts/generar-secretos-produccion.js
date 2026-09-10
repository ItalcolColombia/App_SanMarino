#!/usr/bin/env node
/**
 * Genera valores AUTO-GENERADOS para los secretos de producción, distintos de los de desarrollo.
 *
 * Por qué existe
 * --------------
 * Medido el 8-sep-2026: `Encryption:RemitenteFrontend`, `Encryption:RemitenteBackend`,
 * `PlatformSecret:SecretUpFrontend` y `PlatformSecret:EncryptionKey` tienen **el mismo valor** en
 * `appsettings.json` (producción) y en `appsettings.Development.json`. No es una coincidencia
 * cosmética: el task definition de ECS **no** sobrescribe esas claves —solo pisa la connection
 * string, `JwtSettings__*` y las de entorno—, así que producción corre literalmente con los valores
 * que están versionados en el repo, y cualquiera que clone puede leerlos.
 *
 * Este script NO aplica nada: imprime valores nuevos y el bloque listo para pegar en el task
 * definition. Aplicarlos es una decisión con consecuencias distintas según el grupo (ver abajo).
 *
 * Uso:
 *   node backend/scripts/generar-secretos-produccion.js            # los tres grupos
 *   node backend/scripts/generar-secretos-produccion.js --solo-a   # sólo lo que rota sin coordinar
 *
 * ⚠️ Los valores se imprimen en pantalla y NO se escriben a ningún archivo, a propósito: si
 * terminaran en el repo estaríamos repitiendo el problema que este script existe para cerrar.
 * Cargalos en el task definition de ECS (o mejor, en Secrets Manager) y cerrá la terminal.
 */

const crypto = require('crypto');

/** 32 bytes aleatorios en Base64. Seguro dentro de JSON y de una env var de ECS. */
const secreto = () => crypto.randomBytes(32).toString('base64');

/** Variante sin `+ / =`, para valores que puedan pasar por una URL o un shell descuidado. */
const secretoAlfanumerico = (largo = 44) => {
  const abecedario = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  const bytes = crypto.randomBytes(largo);
  return Array.from(bytes, (b) => abecedario[b % abecedario.length]).join('');
};

// ─────────────────────────────────────────────────────────────────────────────
// GRUPO A — sólo los conoce el servidor. Rotar es seguro: ningún cliente los tiene.
// ─────────────────────────────────────────────────────────────────────────────
const GRUPO_A = [
  {
    envVar: 'PlatformSecret__DerivationKey',
    valor: secreto(),
    nota: 'Hoy VACÍA en producción. Ponerla ACTIVA la firma de plataforma por sesión (Fase A): ' +
          'las sesiones nuevas pasan a usar HMAC(clave, jti) y las ya abiertas siguen por el ' +
          'camino legacy. Sin corte de servicio — el middleware es fail-safe en ambas direcciones.',
  },
  {
    envVar: 'Swagger__Password',
    valor: secretoAlfanumerico(32),
    nota: 'En producción Swagger NO se monta (vive dentro de `if (!IsProduction())`), así que ' +
          'rotarla no cambia nada en prod; sirve para que dev/staging dejen de usar la del repo.',
  },
  {
    envVar: 'JwtSettings__Key',
    valor: secreto(),
    nota: '⚠️ Rotarla INVALIDA todos los tokens vivos: todos los usuarios tienen que volver a ' +
          'entrar. No rompe la aplicación, pero hacelo en horario de baja operación. Esta clave YA ' +
          'viene por env var, así que es la única del grupo que hoy no es la del repo.',
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// GRUPO B — el navegador y la app móvil también los tienen. Rotar EXIGE desplegar todo junto.
// ─────────────────────────────────────────────────────────────────────────────
const GRUPO_B = [
  { envVar: 'Encryption__RemitenteFrontend',      valor: secreto() },
  { envVar: 'Encryption__RemitenteBackend',       valor: secreto() },
  { envVar: 'PlatformSecret__SecretUpFrontend',   valor: secreto() },
  { envVar: 'PlatformSecret__EncryptionKey',      valor: secreto() },
  { envVar: 'PlatformSecret__SecretUpMovil',      valor: secreto() },
];

// ─────────────────────────────────────────────────────────────────────────────
const GRUPO_C = [
  ['Recaptcha:SecretKey',   'la emite Google — se rota en la consola de reCAPTCHA, no acá'],
  ['Email:Smtp:Password',   'credencial de O365 / Graph — se rota en el tenant'],
  ['ConnectionStrings:…',   'contraseña de RDS — se rota en la consola de AWS y en el task definition'],
];

const soloA = process.argv.includes('--solo-a');

function imprimirGrupo(titulo, items, detalle) {
  console.log(`\n${titulo}`);
  console.log('─'.repeat(titulo.length));
  if (detalle) console.log(detalle + '\n');
  for (const it of items) {
    console.log(`  ${it.envVar}`);
    console.log(`    ${it.valor}`);
    if (it.nota) console.log(`    → ${it.nota.replace(/\s+/g, ' ')}`);
    console.log('');
  }
}

function bloqueTaskDefinition(items) {
  return items.map((i) => `        { "name": "${i.envVar}", "value": "${i.valor}" }`).join(',\n');
}

console.log('\n════════════════════════════════════════════════════════════════════');
console.log(' Secretos auto-generados para PRODUCCIÓN — distintos de los de desarrollo');
console.log('════════════════════════════════════════════════════════════════════');
console.log('\nNada de esto se aplica solo. El script imprime y nada más.');

imprimirGrupo(
  'GRUPO A — sólo servidor: rotar es seguro',
  GRUPO_A,
  'Ningún cliente conoce estos valores, así que cambiarlos no puede romper al navegador ni a la\n' +
  'app móvil. Se cargan en el task definition y con el próximo arranque quedan activos.');

if (!soloA) {
  imprimirGrupo(
    'GRUPO B — compartidos con el navegador y la app móvil: NO rotar de a uno',
    GRUPO_B,
    '⚠️ Estos valores viven TAMBIÉN en `frontend/src/environments/environment.prod.ts` (compilado\n' +
    'dentro del bundle) y en la app Flutter. Si cambiás el backend y no el resto, el login deja de\n' +
    'descifrarse y el filtro de origen rechaza TODAS las peticiones: la aplicación queda inutilizable\n' +
    'para todo el mundo, no "un poco rota".\n\n' +
    'Para rotarlos hace falta, en un mismo despliegue coordinado: (1) cargar el valor nuevo en el\n' +
    'task definition, (2) actualizar `environment.prod.ts` y desplegar el frontend, (3) publicar una\n' +
    'versión de la app móvil con el suyo — y hasta que los usuarios actualicen, la app vieja queda\n' +
    'afuera. Por eso `SecretUpMovil` es una clave aparte: permite rotar el web sin voltear el móvil.\n\n' +
    'La alternativa que evita todo esto es terminar la Fase B (que el bundle deje de llevar un valor\n' +
    'que sirva). Ver `fase_de_desarrollo/llave_plataforma_por_sesion_plan.md`.');
}

console.log('\nGRUPO C — no se auto-generan acá');
console.log('─────────────────────────────────');
for (const [clave, nota] of GRUPO_C) console.log(`  ${clave.padEnd(24)} ${nota}`);

console.log('\n\nBloque para el task definition de ECS (pegar dentro de "environment")');
console.log('─────────────────────────────────────────────────────────────────────');
console.log(bloqueTaskDefinition(soloA ? GRUPO_A : [...GRUPO_A, ...GRUPO_B]) + ',');

console.log('\n\nDespués de cargarlos');
console.log('────────────────────');
console.log('  1. Verificá el deploy con los comandos de ECS del CLAUDE.md — el CLI miente:');
console.log('     ECS hace rollback silencioso y `update-service` igual dice "completado".');
console.log('  2. Confirmá que la app arranca y que se puede iniciar sesión.');
console.log('  3. Recién con las env vars vivas, vaciá esos valores de `appsettings.json`, para que');
console.log('     el repo deje de contener secretos que sirven en producción. Hacerlo ANTES deja la');
console.log('     app sin arrancar: `PlatformSecretMiddleware` lanza si falta SecretUpFrontend o');
console.log('     EncryptionKey.');
console.log('');

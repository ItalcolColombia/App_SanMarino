#!/usr/bin/env node
/**
 * Rota la clave de firma JWT dentro del JSON de la task definition que el deploy ya descargó.
 *
 * Por qué existe
 * --------------
 * La clave JWT de producción vive en `environment` de la task definition de ECS
 * (`JwtSettings__Key`) y hasta sep-2026 era fija: la misma desde hacía meses y, además, copiada en
 * claro en los snapshots versionados de `backend/deploy/*.json`. Nadie del equipo administra AWS
 * (no se pueden crear secretos ni tocar IAM), así que la rotación tiene que salir del pipeline con
 * lo que ya hace en cada deploy: bajar la task definition, cambiarle la imagen y registrar una
 * revisión nueva. Este script agrega un paso a esa misma edición del JSON:
 *
 *   - la `JwtSettings__Key` que había pasa a `JwtSettings__PreviousKey`;
 *   - `JwtSettings__Key` recibe 64 bytes aleatorios nuevos.
 *
 * La API firma con la nueva y valida con las dos (`JwtRotacionClaveCalculos`), así que los tokens
 * emitidos antes del deploy siguen valiendo hasta vencer: el deploy no desloguea a nadie.
 *
 * Si la task definition ya toma `JwtSettings__Key` de `secrets` (Secrets Manager), no se toca: esa
 * clave la administra otro.
 *
 * Nunca imprime una clave: las enmascara para GitHub Actions (`::add-mask::`) y solo informa largos.
 *
 * Uso:  node backend/scripts/rotar-clave-jwt-taskdef.js <task-def.json> [contenedor=backend]
 */

const crypto = require('crypto');
const fs = require('fs');

const CLAVE = 'JwtSettings__Key';
const ANTERIOR = 'JwtSettings__PreviousKey';

/** 64 bytes aleatorios en base64 estándar (88 caracteres). */
const claveNueva = () => crypto.randomBytes(64).toString('base64');

/**
 * Devuelve la task definition con la clave rotada (sin mutar la de entrada) y qué pasó.
 * `nueva` se inyecta para poder probarla; en el deploy la genera `claveNueva()`.
 */
function rotarClaveEnTaskDef(taskDef, contenedor, nueva) {
  const definiciones = Array.isArray(taskDef && taskDef.containerDefinitions) ? taskDef.containerDefinitions : [];
  const indice = definiciones.findIndex((c) => c && c.name === contenedor);
  if (indice < 0) throw new Error(`La task definition no tiene el contenedor "${contenedor}".`);

  const actual = definiciones[indice];
  if ((actual.secrets || []).some((s) => s.name === CLAVE)) {
    return { taskDef, estado: 'secrets' };
  }

  const environment = actual.environment || [];
  const previa = (environment.find((e) => e.name === CLAVE) || {}).value || '';

  const nuevoEnvironment = environment
    .filter((e) => e.name !== CLAVE && e.name !== ANTERIOR)
    .concat([{ name: CLAVE, value: nueva }])
    .concat(previa ? [{ name: ANTERIOR, value: previa }] : []);

  const containerDefinitions = definiciones.map((c, i) => (i === indice ? { ...c, environment: nuevoEnvironment } : c));
  return {
    taskDef: { ...taskDef, containerDefinitions },
    estado: previa ? 'rotada' : 'primera',
    previa,
  };
}

function main(argv) {
  const [ruta, contenedor = 'backend'] = argv;
  if (!ruta) {
    console.error('Uso: node backend/scripts/rotar-clave-jwt-taskdef.js <task-def.json> [contenedor]');
    return 2;
  }

  const nueva = claveNueva();
  console.log(`::add-mask::${nueva}`);

  const original = JSON.parse(fs.readFileSync(ruta, 'utf8'));
  const { taskDef, estado, previa } = rotarClaveEnTaskDef(original, contenedor, nueva);
  if (previa) console.log(`::add-mask::${previa}`);

  if (estado === 'secrets') {
    console.log(`::notice::${CLAVE} sale de secrets (Secrets Manager): no se rota desde el deploy.`);
    return 0;
  }

  fs.writeFileSync(ruta, JSON.stringify(taskDef, null, 2));
  console.log(
    estado === 'rotada'
      ? `Clave JWT rotada: ${CLAVE} nueva (${nueva.length} caracteres); ${ANTERIOR} = la anterior (${previa.length} caracteres).`
      : `Clave JWT creada: ${CLAVE} nueva (${nueva.length} caracteres). No habia clave anterior: los tokens vivos dejan de valer.`
  );
  return 0;
}

module.exports = { rotarClaveEnTaskDef, claveNueva, CLAVE, ANTERIOR };

if (require.main === module) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (e) {
    console.log(`::error::${e.message}`);
    process.exitCode = 1;
  }
}

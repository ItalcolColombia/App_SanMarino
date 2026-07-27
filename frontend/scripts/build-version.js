#!/usr/bin/env node

/**
 * Sella la versión del build SIN mutar el output de `ng build`.
 *
 * Reemplaza al viejo `inject-version.js`, que reescribía `dist/browser/index.html`
 * DESPUÉS del build. Esa mutación es incompatible con el Service Worker: el builder
 * de `@angular/service-worker` calcula el SHA1 de cada archivo (index.html incluido)
 * mientras genera `ngsw.json`; si el archivo cambia después, el hash deja de coincidir
 * y el SW entra en **safe mode** y se desactiva solo, en silencio. La PWA se despliega,
 * el operario la instala, y en la granja no funciona nada.
 *
 * Dos fases:
 *
 *   prepare  (ANTES de `ng build`)  escribe el buildId en `src/app/core/build-info.ts`,
 *                                   que el compilador mete DENTRO del bundle. El bundle
 *                                   se hashea normalmente y `ngsw.json` queda consistente.
 *
 *   emit     (DESPUÉS de `ng build`) escribe `dist/browser/version.json` con el MISMO
 *                                   buildId. Es un archivo nuevo, no una mutación de uno
 *                                   ya hasheado.
 *
 * ⚠️ `version.json` NO DEBE entrar en ningún `assetGroup` de `ngsw-config.json`.
 *    Si entrara, el SW lo serviría desde caché y el chequeo de versión miraría para
 *    siempre la versión con la que se instaló. Tiene que ir siempre a la red.
 *
 * Uso:
 *   node scripts/build-version.js prepare
 *   ng build --configuration docker
 *   node scripts/build-version.js emit
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const BUILD_INFO_TS = path.join(ROOT, 'src/app/core/build-info.ts');
const BUILD_ID_TMP = path.join(ROOT, '.build-version');
const VERSION_JSON = path.join(ROOT, 'dist/browser/version.json');

/** Lee el buildId que dejó `prepare`. Falla fuerte: un emit sin prepare es un bug del pipeline. */
function leerBuildId() {
  if (!fs.existsSync(BUILD_ID_TMP)) {
    console.error(
      `[build-version] No existe ${BUILD_ID_TMP}. Hay que correr "prepare" antes del build.`
    );
    process.exit(1);
  }
  return fs.readFileSync(BUILD_ID_TMP, 'utf8').trim();
}

function prepare() {
  const buildId = new Date().toISOString();

  // El `: string` es obligatorio: sin él TypeScript infiere el tipo literal del
  // timestamp y `BUILD_ID !== 'dev'` deja de compilar (TS2367, "no overlap").
  const contenido = `// GENERADO POR scripts/build-version.js — no editar a mano.
// En desarrollo local queda el valor 'dev' commiteado y el chequeo de versión se apaga.
export const BUILD_ID: string = '${buildId}';
`;

  fs.writeFileSync(BUILD_INFO_TS, contenido, 'utf8');
  fs.writeFileSync(BUILD_ID_TMP, buildId, 'utf8');
  console.log(`[build-version] prepare: BUILD_ID=${buildId} -> src/app/core/build-info.ts`);
}

function emit() {
  const buildId = leerBuildId();
  const dir = path.dirname(VERSION_JSON);

  if (!fs.existsSync(dir)) {
    console.error(`[build-version] No existe ${dir}. ¿Corrió el build?`);
    process.exit(1);
  }

  fs.writeFileSync(VERSION_JSON, JSON.stringify({ buildId }) + '\n', 'utf8');
  console.log(`[build-version] emit: dist/browser/version.json -> ${buildId}`);
}

const comando = process.argv[2];

if (comando === 'prepare') {
  prepare();
} else if (comando === 'emit') {
  emit();
} else {
  console.error('[build-version] Uso: node scripts/build-version.js <prepare|emit>');
  process.exit(1);
}

#!/usr/bin/env node

/**
 * Gate de integridad del Service Worker. Corre DESPUÉS de `ng build` y del `emit`
 * de la versión, y **falla el build** si algo no cierra.
 *
 * ## Por qué existe
 *
 * `ngsw.json` guarda el SHA1 de cada archivo que el Service Worker va a precachear.
 * Si un archivo del output cambia **después** de que el builder calculó ese hash, el
 * SW detecta la diferencia al instalar, entra en **safe mode y se desactiva solo, en
 * silencio**: la PWA se despliega bien, el operario la instala bien, y en la granja no
 * funciona nada. No hay error en el build, no hay error en el deploy, no hay error en
 * la consola del navegador.
 *
 * Ese fallo ya estuvo a punto de pasar acá: el viejo `inject-version.js` reescribía
 * `dist/browser/index.html` después del build (por eso hoy existe `build-version.js`
 * partido en `prepare`/`emit`). Este script convierte esa regla —"nada reescribe el
 * output después del build"— en algo que la máquina verifica, en vez de algo que hay
 * que acordarse.
 *
 * ## Qué verifica
 *
 *  1. Que `ngsw.json` exista (si no, el SW no se generó y el deploy sería una PWA muda).
 *  2. Que el SHA1 de cada archivo declarado coincida con el archivo en disco.
 *  3. Que `version.json` NO esté en la tabla de hashes: si entrara, el SW lo serviría
 *     desde caché y el chequeo de versión miraría para siempre la versión con la que se
 *     instaló.
 *  4. Que no haya `dataGroups`. La caché del SW indexa por URL e **ignora headers**, y
 *     la empresa activa viaja en `X-Active-Company`: un dataGroup sobre `/api/**` le
 *     serviría al operario de la empresa B la respuesta cacheada de la empresa A. Es una
 *     fuga entre empresas, y como se configura en un JSON aparte es exactamente el tipo
 *     de cosa que alguien agrega con buena intención dentro de seis meses.
 *  5. Que estén los archivos de instalabilidad (manifest e iconos declarados).
 *
 * Uso:  node scripts/verificar-ngsw.js [directorioDelBuild]
 */

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const ROOT = path.join(__dirname, '..');
const DIST = path.resolve(process.argv[3] || process.argv[2] || path.join(ROOT, 'dist/browser'));

const errores = [];
const notas = [];

function fallar(msg) {
  errores.push(msg);
}

/** El builder del SW hashea con SHA1 sobre el contenido crudo del archivo. */
function sha1(archivo) {
  return crypto.createHash('sha1').update(fs.readFileSync(archivo)).digest('hex');
}

// --- 1) ngsw.json tiene que existir -----------------------------------------
const ngswPath = path.join(DIST, 'ngsw.json');
if (!fs.existsSync(ngswPath)) {
  console.error(
    `[verificar-ngsw] FALLA: no existe ${ngswPath}.\n` +
      `  El build no generó el Service Worker. Revisá que la configuración usada tenga\n` +
      `  "serviceWorker": "ngsw-config.json" en angular.json.`
  );
  process.exit(1);
}

const ngsw = JSON.parse(fs.readFileSync(ngswPath, 'utf8'));

// --- 2) Cada hash declarado contra el archivo en disco ----------------------
const tabla = ngsw.hashTable || {};
const rutas = Object.keys(tabla);

if (rutas.length === 0) {
  fallar('hashTable vacía: el SW no precachearía nada.');
}

let verificados = 0;
for (const ruta of rutas) {
  // Las rutas de ngsw.json arrancan con "/" y son relativas a la raíz del sitio.
  const archivo = path.join(DIST, ruta.replace(/^\//, ''));

  if (!fs.existsSync(archivo)) {
    fallar(`declarado en ngsw.json pero ausente del output: ${ruta}`);
    continue;
  }

  const enDisco = sha1(archivo);
  if (enDisco !== tabla[ruta]) {
    fallar(
      `SHA1 divergente en ${ruta}\n` +
        `      ngsw.json: ${tabla[ruta]}\n` +
        `      en disco : ${enDisco}\n` +
        `      Causa casi segura: algo reescribió ese archivo DESPUÉS de ng build.\n` +
        `      Con esto el Service Worker arranca en safe mode y se desactiva en silencio.`
    );
    continue;
  }
  verificados++;
}

// --- 3) version.json fuera de la tabla de hashes ----------------------------
if (rutas.some(r => r === '/version.json')) {
  fallar(
    'version.json está en el hashTable de ngsw.json. Tiene que ir SIEMPRE a la red:\n' +
      '      cacheado, el chequeo de versión miraría para siempre la versión de instalación.\n' +
      '      Sacalo de los assetGroups de ngsw-config.json.'
  );
}

// --- 4) Prohibido dataGroups (fuga entre empresas) --------------------------
if (Array.isArray(ngsw.dataGroups) && ngsw.dataGroups.length > 0) {
  fallar(
    `ngsw.json declara ${ngsw.dataGroups.length} dataGroup(s). Está prohibido en este proyecto:\n` +
      '      la caché del SW indexa por URL e IGNORA headers, y la empresa activa viaja en\n' +
      '      X-Active-Company => el operario de la empresa B recibiría la respuesta cacheada\n' +
      '      de la empresa A. Los datos van a IndexedDB particionada por {userId, companyId}.'
  );
}

// --- 5) Instalabilidad: manifest e iconos -----------------------------------
const manifestPath = path.join(DIST, 'manifest.webmanifest');
if (!fs.existsSync(manifestPath)) {
  fallar('falta manifest.webmanifest en el output: la app no sería instalable.');
} else {
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  for (const icono of manifest.icons || []) {
    const archivo = path.join(DIST, icono.src.replace(/^\//, ''));
    if (!fs.existsSync(archivo)) {
      fallar(`el manifest declara un icono que no está en el output: ${icono.src}`);
    }
  }
  const tieneMaskable = (manifest.icons || []).some(i => (i.purpose || '').includes('maskable'));
  if (!tieneMaskable) {
    notas.push('el manifest no declara ningún icono maskable (Android lo recortaría con fondo blanco).');
  }
}

// --- 6) El kill switch tiene que estar publicado ----------------------------
if (!fs.existsSync(path.join(DIST, 'safety-worker.js'))) {
  fallar(
    'falta safety-worker.js en el output. Es el kill switch del SW: desplegarlo el día\n' +
      '      del incidente cuesta un build + push a ECR + deploy de ECS con la operación parada.'
  );
}

// --- Resultado --------------------------------------------------------------
for (const nota of notas) {
  console.warn(`[verificar-ngsw] AVISO: ${nota}`);
}

if (errores.length > 0) {
  console.error(`[verificar-ngsw] FALLA (${errores.length}):`);
  errores.forEach(e => console.error(`  - ${e}`));
  process.exit(1);
}

console.log(
  `[verificar-ngsw] OK: ${verificados} archivo(s) con SHA1 coincidente, ` +
    `sin dataGroups, manifest e iconos presentes, kill switch publicado.`
);

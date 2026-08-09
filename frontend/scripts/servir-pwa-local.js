#!/usr/bin/env node

/**
 * Sirve `dist/browser` replicando las reglas de `nginx.conf`, para poder probar la PWA
 * completa en local.
 *
 * ## Por qué no alcanza con `http-server` o con `ng serve`
 *
 * - `ng serve` **no registra el Service Worker** (`enabled: !isDevMode()`), así que no
 *   prueba nada de la PWA.
 * - Un estático genérico no reproduce las tres reglas de nginx de las que depende que el
 *   Service Worker no se rompa, y que son justamente las que hay que verificar:
 *     1. `ngsw.json`, `ngsw-worker.js`, `safety-worker.js`, `manifest.webmanifest` y
 *        `version.json` con `Cache-Control: no-cache`. Cacheados, la PWA no se puede
 *        actualizar nunca y el kill switch queda inservible.
 *     2. Un archivo con extensión que no existe devuelve **404**, nunca el `index.html`.
 *        Si devolviera el index, el SW recibiría HTML donde espera JS/JSON y se
 *        desactivaría en silencio. Es el bug que se encontró en prod el 2026-07-27.
 *     3. El fallback al `index.html` es **solo para navegaciones** (paths sin extensión).
 *     4. `.webmanifest` con `Content-Type: application/manifest+json` — nginx no lo trae
 *        en su mime.types por defecto y sin ese tipo el navegador descarta el manifest
 *        en silencio y la app deja de ser instalable.
 *
 * `localhost` cuenta como contexto seguro, así que el SW se registra sin HTTPS.
 *
 * Uso:  node scripts/servir-pwa-local.js [puerto]
 */

const http = require('http');
const fs = require('fs');
const path = require('path');

const RAIZ = path.join(__dirname, '..', 'dist', 'browser');
const PUERTO = Number(process.argv[2] || 4400);

/** Los 5 archivos de control: si se cachean, la PWA deja de poder actualizarse. */
const SIN_CACHE = new Set([
  '/ngsw.json',
  '/ngsw-worker.js',
  '/safety-worker.js',
  '/worker-basic.min.js',
  '/manifest.webmanifest',
  '/version.json',
  '/index.html'
]);

const TIPOS = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.map': 'application/json; charset=utf-8',
  '.txt': 'text/plain; charset=utf-8'
};

/** Espejo de nginx-security-headers.conf, en lo que aplica a un servidor local. */
const HEADERS_SEGURIDAD = {
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Referrer-Policy': 'strict-origin-when-cross-origin'
};

function enviar(res, codigo, cuerpo, headers) {
  res.writeHead(codigo, { ...HEADERS_SEGURIDAD, ...headers });
  res.end(cuerpo);
}

function servirArchivo(res, rutaUrl, archivo) {
  const ext = path.extname(archivo).toLowerCase();
  const tipo = TIPOS[ext] || 'application/octet-stream';

  const cache = SIN_CACHE.has(rutaUrl)
    ? 'no-cache'
    : // Assets con hash en el nombre (outputHashing: all) -> cache larga, como en nginx.
      'public, max-age=31536000, immutable';

  enviar(res, 200, fs.readFileSync(archivo), {
    'Content-Type': tipo,
    'Cache-Control': cache
  });
}

const servidor = http.createServer((req, res) => {
  const rutaUrl = decodeURIComponent(new URL(req.url, `http://localhost:${PUERTO}`).pathname);

  // Traversal fuera de la raíz -> 403 (nginx lo resuelve normalizando el path).
  const destino = path.join(RAIZ, rutaUrl);
  if (!destino.startsWith(RAIZ)) {
    return enviar(res, 403, 'Forbidden', { 'Content-Type': 'text/plain' });
  }

  if (rutaUrl === '/health') {
    return enviar(res, 200, 'healthy\n', { 'Content-Type': 'text/plain' });
  }

  if (rutaUrl !== '/' && fs.existsSync(destino) && fs.statSync(destino).isFile()) {
    return servirArchivo(res, rutaUrl, destino);
  }

  // Regla clave: un path CON extensión que no existe es 404, nunca el index.
  // Devolver el index acá es lo que deja al Service Worker recibiendo HTML donde
  // espera JS/JSON, y desactivándose en silencio.
  if (path.extname(rutaUrl)) {
    return enviar(res, 404, 'Not Found', {
      'Content-Type': 'text/plain',
      'Cache-Control': 'no-cache'
    });
  }

  // Navegación (path sin extensión) -> index.html
  const index = path.join(RAIZ, 'index.html');
  if (!fs.existsSync(index)) {
    return enviar(res, 500, 'Falta dist/browser/index.html. ¿Corriste yarn build?', {
      'Content-Type': 'text/plain'
    });
  }
  return servirArchivo(res, '/index.html', index);
});

servidor.listen(PUERTO, () => {
  console.log(`[servir-pwa-local] http://localhost:${PUERTO}  (raíz: ${RAIZ})`);
  console.log('[servir-pwa-local] Reglas de nginx replicadas: no-cache de control, 404 de assets, fallback solo en navegaciones.');
});

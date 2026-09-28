'use strict';

/**
 * Lista blanca de rutas del SPA en el borde (hallazgo de seguridad sep-2026: `/env` → 200).
 * Plan: fase_de_desarrollo/borde_rutas_spa_404_plan.md
 *
 * Corre en el job `tests` del deploy (`node --test scripts/tests/rutas-spa-nginx.test.js`), antes de
 * construir ninguna imagen. El comportamiento de nginx de punta a punta lo prueba el gate del borde
 * (bloque C7) sobre la imagen ya construida.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { FUENTE_RUTAS, extraerRutasTopLevel, construirPatron, esRutaSpa, generarConfNginx } = require('../rutas-spa-nginx');

const FRONT = path.join(__dirname, '..', '..');
const leer = rel => fs.readFileSync(path.join(FRONT, rel), 'utf8').replace(/\r\n/g, '\n');

const RUTAS_REALES = extraerRutasTopLevel(fs.readFileSync(FUENTE_RUTAS, 'utf8'), FUENTE_RUTAS);
const REAL = construirPatron(RUTAS_REALES);

/** app.config.ts mínimo con las rutas dadas. */
const fuente = rutas =>
  `import { provideRouter } from '@angular/router';\nexport const c = { providers: [provideRouter([${rutas}])] };\n`;
const patronDe = rutas => construirPatron(extraerRutasTopLevel(fuente(rutas), 'sintetico.ts'));

const esperar = (patron, uris, esperado) => {
  for (const uri of uris) {
    assert.equal(esRutaSpa(uri, patron), esperado, `${uri} ${esperado ? 'tiene que' : 'NO puede'} servir el SPA`);
  }
};

// ─── Contra el app.config.ts real ───────────────────────────────────────────────────────────────

test('las entradas que llegan por carga completa siguen sirviendo el SPA', () => {
  // Son las únicas que llegan a nginx: la navegación interna la resuelve el router en el navegador.
  // Correos (CorreosCuenta: /login, /reset-password?token=), window.location.href = '/home' (login),
  // <a href="/diagnostico"> (barra PWA), manifest (start_url en la raíz, atajos daily-log/seguimiento
  // y diagnostico). La query no forma parte de $uri.
  esperar(REAL, [
    '/', '/login', '/login/', '/reset-password', '/password-recovery', '/home', '/diagnostico',
    '/daily-log/seguimiento', '/selector-usuario', '/cambiar-usuario', '/dashboard'
  ], true);
});

test('las subrutas de los módulos siguen sirviendo el SPA al recargar', () => {
  esperar(REAL, [
    '/config', '/config/', '/config/users', '/config/guia-genetica/9/edit', '/config/nucleos/N-01',
    '/traslados-aves/historial/12', '/tickets/9', '/tickets/panel', '/indicador-ecuador/corridas',
    '/mapas/configuraciones/3', '/gestion-veterinaria/tareas/5/cumplir', '/daily-log/aves-engorde/editar/7'
  ], true);
});

test('las sondas de reconocimiento NO sirven el SPA (el hallazgo)', () => {
  esperar(REAL, [
    '/env', '/environment', '/aws', '/actuator', '/actuator/env', '/swagger', '/swagger-ui',
    '/server-status', '/backup', '/wp-admin', '/phpmyadmin', '/debug', '/console', '/no-existe-1234',
    '/Login', '/LOGIN', '/configx', '/config.php', '/web.config',
    // hojas: no aceptan subrutas
    '/login/env', '/home/env', '/diagnostico/.env', '/reset-password/x',
    // prefijos: el último segmento con punto es un archivo, no una navegación
    '/config/database.yml', '/config/app.php', '/tickets/web.config', '/config/users/backup.sql'
  ], false);
});

test('las rutas top-level del router son exactamente las de la lista blanca', () => {
  // Si alguien agrega una ruta top-level, entra sola (se genera en el build): este test solo fija
  // que la extracción lee las 39 de hoy y ninguna más. Si cambia el número a propósito, actualizalo.
  assert.equal(RUTAS_REALES.length, 39, `rutas leídas: ${RUTAS_REALES.map(r => r.path).join(', ')}`);
  for (const r of RUTAS_REALES) {
    assert.ok(esRutaSpa(`/${r.path}`, REAL), `/${r.path} está en app.config.ts y no quedó en la lista`);
  }
});

// ─── Semántica, sobre fuentes sintéticas ────────────────────────────────────────────────────────

test('hoja → exacta; children/loadChildren/redirect prefix → prefijo; redirect full → exacta', () => {
  const p = patronDe(`
    { path: '', redirectTo: 'inicio', pathMatch: 'full' },
    { path: 'inicio', component: X },
    { path: 'modulo', canActivate: [g], children: [{ path: 'a', component: X }] },
    { path: 'perezoso', loadChildren: () => import('./x').then(m => m.R) },
    { path: 'viejo', redirectTo: 'inicio', pathMatch: 'full' },
    { path: 'viejo-prefijo', redirectTo: 'modulo' },
    { path: 'ficha/:id', component: X },
    { path: '**', redirectTo: 'inicio' }`);

  esperar(p, ['/', '/inicio', '/inicio/', '/viejo', '/ficha/9', '/ficha/9/'], true);
  esperar(p, ['/modulo', '/modulo/', '/modulo/a', '/modulo/a/b/c', '/modulo/a.b/c', '/perezoso/9', '/viejo-prefijo/x'], true);
  esperar(p, ['/inicio/x', '/viejo/x', '/ficha', '/ficha/9/x', '/ficha/x.env', '/modulo/x.yml', '/cualquiera', '/moduloX'], false);
});

test("los hijos inline de una ruta '' cuentan como top-level", () => {
  const p = patronDe(`{ path: '', component: Layout, children: [{ path: 'panel', component: X }, { path: 'area', children: [] }] }`);
  esperar(p, ['/', '/panel', '/area/x'], true);
  esperar(p, ['/env', '/panel/x'], false);
});

test('fail-closed: lo que no se sabe traducir corta con el motivo', () => {
  const casos = [
    ['parámetro en el primer segmento', fuente(`{ path: ':empresa/home', component: X }`), /empieza con un parámetro/],
    ['matcher', fuente(`{ matcher: m, component: X }`), /matcher/],
    ['spread de rutas', fuente(`...OTRAS, { path: 'a', component: X }`), /no es un objeto literal/],
    ['spread de propiedades', fuente(`{ ...BASE, path: 'a' }`), /spread/],
    ['path no literal', fuente(`{ path: RUTA, component: X }`), /texto literal/],
    ['path shorthand', fuente(`{ path, component: X }`), /path/],
    ["'' con loadChildren", fuente(`{ path: '', loadChildren: () => import('./x') }`), /loadChildren/],
    ["'' con children no literales", fuente(`{ path: '', children: HIJOS }`), /array literal/],
    ['pathMatch no literal', fuente(`{ path: 'a', redirectTo: 'b', pathMatch: PM }`), /pathMatch/],
    ['segmento con caracteres raros', fuente(`{ path: 'a.b', component: X }`), /caracteres/],
    ['barra de más', fuente(`{ path: 'a//b', component: X }`), /barra de más/],
    ['sin rutas además de la raíz', fuente(`{ path: '', redirectTo: 'x', pathMatch: 'full' }`), /ninguna ruta/],
    ['sin provideRouter', 'export const c = { providers: [] };', /exactamente un provideRouter/],
    ['dos provideRouter', `${fuente(`{ path: 'a', component: X }`)}provideRouter([]);`, /exactamente un provideRouter/],
    ['array no literal', 'export const c = { providers: [provideRouter(RUTAS)] };', /array literal/]
  ];
  for (const [caso, codigo, motivo] of casos) {
    assert.throws(() => construirPatron(extraerRutasTopLevel(codigo, 'sintetico.ts')), motivo, caso);
  }
});

// ─── La conf de nginx y su cableado ─────────────────────────────────────────────────────────────

test('la conf generada usa exactamente los patrones que se prueban acá', () => {
  const conf = generarConfNginx(RUTAS_REALES);
  assert.match(conf, /^map \$uri \$ruta_spa \{$/m);
  assert.match(conf, /^ {2}default 0;$/m);
  assert.match(conf, /^ {2}\/ {7}1;$/m);
  const enConf = [...conf.matchAll(/^ {2}"~(.+)" 1;$/gm)].map(m => m[1]);
  assert.deepEqual(enConf, REAL.patrones);
  // Sin comodines: ningún patrón puede aceptar cualquier primer segmento.
  for (const p of REAL.patrones) assert.ok(!/^\^\/\(\?:\[\^/.test(p), `patrón demasiado abierto: ${p}`);
});

test('nginx.conf corta con 404 lo que no está en la lista, antes del fallback al index', () => {
  const nginx = leer('nginx.conf');
  assert.match(nginx, /^include \/etc\/nginx\/rutas-spa\.conf;$/m, 'el map se incluye fuera del server (contexto http)');
  const bloque4 = /\n {2}location \/ \{\n([\s\S]*?)\n {2}\}/.exec(nginx);
  assert.ok(bloque4, 'no encuentro el bloque `location / {` de nginx.conf');
  const cuerpo = bloque4[1];
  assert.match(cuerpo, /if \(\$ruta_spa = 0\) \{\s*return 404;\s*\}/);
  assert.match(cuerpo, /error_page 404 \/404\.html;/);
  assert.ok(cuerpo.indexOf('$ruta_spa') < cuerpo.indexOf('try_files'), 'el corte tiene que ir antes del try_files');
  assert.match(nginx, /location = \/404\.html \{\n\s+internal;/, '404.html solo se sirve como página de error');
});

test('el Dockerfile genera la lista en cada build y la copia al runtime', () => {
  const docker = leer('Dockerfile');
  assert.match(docker, /^COPY scripts\/rutas-spa-nginx\.js \.\/scripts\/rutas-spa-nginx\.js$/m);
  assert.match(docker, /node scripts\/rutas-spa-nginx\.js --salida \/app\/rutas-spa\.conf/);
  assert.match(docker, /^COPY --from=build \/app\/rutas-spa\.conf \/etc\/nginx\/rutas-spa\.conf$/m);
  // .dockerignore excluye scripts/* con lista blanca por nombre: sin esta línea el COPY de arriba falla.
  assert.match(leer('.dockerignore'), /^!scripts\/rutas-spa-nginx\.js$/m);
});

test('la página 404 es estática, sin scripts, y viaja en el build', () => {
  const pagina = leer('src/404.html');
  assert.doesNotMatch(pagina, /<script/i, 'la CSP y el propósito piden una página sin JS');
  assert.doesNotMatch(pagina, /<app-root/i, 'no puede ser el shell del SPA');
  assert.match(pagina, /href="\/"/, 'tiene que ofrecer volver al inicio');
  const angular = JSON.parse(leer('angular.json'));
  assert.ok(angular.projects.frontend.architect.build.options.assets.includes('src/404.html'));
});

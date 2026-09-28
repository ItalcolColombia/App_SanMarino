#!/usr/bin/env node
'use strict';

/**
 * Genera la lista blanca de rutas del SPA: nginx sirve el `index.html` SOLO para esas rutas y
 * responde **404** a cualquier otra.
 *
 * ## Por qué existe
 *
 * Validación de seguridad de producción, sep-2026: *"Responde ELB de AWS, código 200 en /env"*.
 * Medido el 28-sep-2026: `/env`, `/environment`, `/aws`, `/actuator/env`, `/swagger`, `/backup`…
 * respondían **200 con el `index.html`** (mismo hash que `/`). No se servía ningún archivo —era el
 * fallback `try_files … /index.html` del bloque 4 de `nginx.conf`, que contestaba 200 a CUALQUIER ruta
 * sin extensión—, pero una ruta que no existe tiene que dar 404: si no, cada sonda de un escáner parece
 * un hallazgo y el reconocimiento confirma que "algo" responde en cualquier path.
 *
 * Una lista negra (`/env`, `/aws`, …) no sirve: los escáneres prueban miles de rutas. La lista blanca
 * es finita y la dicta el router, así que se deriva de él.
 *
 * ## Qué toma del router
 *
 * Solo las rutas **top-level** del `provideRouter([...])` de `src/app/app.config.ts`, leídas con el
 * compilador de TypeScript (una regex no distingue un `path:` top-level del de un `children` anidado):
 *
 *   - hoja (`component`/`loadComponent`) o redirect `pathMatch: 'full'` → **exacta**: `/login`, `/login/`;
 *   - con `children`/`loadChildren`, o redirect `prefix` → **prefijo**: `/config`, `/config/users/9`.
 *     El último segmento no puede tener punto (`/config/database.yml` → 404): es la misma regla con la
 *     que el Service Worker decide qué es una navegación (`navigationUrls: "!/**\/*.*"`);
 *   - `''` → la raíz `/` (y si trae `children` inline, esos hijos cuentan como top-level);
 *   - `**` → se ignora: es el comodín que el borde deja de imitar.
 *
 * Los hijos de `loadChildren` no se recorren: exigiría reproducir la semántica del router en ~25
 * archivos y un error ahí rompería un enlace real al recargar. El prefijo top-level ya cierra la
 * superficie del hallazgo.
 *
 * ## Fail-closed
 *
 * Todo lo que no sabe traducir **corta** con un mensaje que dice qué tocar: parámetro en el primer
 * segmento (dejaría pasar cualquier ruta), `matcher`, spread, `path` no literal, `''` con
 * `loadChildren`, cero o dos `provideRouter`. Nunca genera un comodín ni una lista vacía.
 *
 * ## Dónde corre
 *
 * - `frontend/Dockerfile` lo corre en cada build (antes de `ng build`) y copia la salida a
 *   `/etc/nginx/rutas-spa.conf`: no hay un archivo que se desincronice del router.
 * - `scripts/tests/rutas-spa-nginx.test.js` lo prueba en el job `tests` del deploy, contra el
 *   `app.config.ts` real, antes de que se construya ninguna imagen.
 * - `scripts/servir-pwa-local.js` usa `esRutaSpa` para replicar la regla en local.
 *
 * Uso:
 *   node scripts/rutas-spa-nginx.js                     # imprime la conf de nginx
 *   node scripts/rutas-spa-nginx.js --salida <archivo>  # la escribe (lo usa el Dockerfile)
 *   node scripts/rutas-spa-nginx.js --listar            # rutas y patrones, para revisar a ojo
 */

const fs = require('fs');
const path = require('path');

const FUENTE_RUTAS = path.join(__dirname, '..', 'src', 'app', 'app.config.ts');

/** Segmento de subruta cualquiera (puede tener punto si no es el último). */
const SEGMENTO = '[^/]+';
/** Último segmento: sin punto, igual que la regla de navegación del Service Worker. */
const SEGMENTO_FINAL = '[^/.]+';
/** Cola de una ruta de prefijo: nada, `/`, o subsegmentos cuyo último no tiene punto. */
const COLA_PREFIJO = '(?:/(?:[^/]*/)*[^/.]*)?';

function cargarTypescript() {
  try {
    return require('typescript');
  } catch {
    throw new Error('falta el paquete `typescript` en node_modules (corré `yarn install` en frontend/)');
  }
}

function fallar(ubicacion, mensaje) {
  throw new Error(`${ubicacion}: ${mensaje}`);
}

/** `app.config.ts:123`, para que el error diga dónde mirar. */
function ubicacionDe(ts, sf, nodo) {
  const { line } = sf.getLineAndCharacterOfPosition(nodo.getStart(sf));
  return `${path.basename(sf.fileName)}:${line + 1}`;
}

function textoLiteral(ts, nodo) {
  return nodo && (ts.isStringLiteral(nodo) || ts.isNoSubstitutionTemplateLiteral(nodo)) ? nodo.text : null;
}

/** Lee un objeto de ruta: solo lo que decide si la ruta es exacta o de prefijo. */
function leerRuta(ts, sf, nodo) {
  const donde = ubicacionDe(ts, sf, nodo);
  if (!ts.isObjectLiteralExpression(nodo)) {
    fallar(donde, `la ruta no es un objeto literal (${ts.SyntaxKind[nodo.kind]}). Las rutas top-level se ` +
      'declaran en línea en provideRouter([...]) para que el borde sepa cuáles existen');
  }

  const props = new Map();
  for (const p of nodo.properties) {
    if (ts.isSpreadAssignment(p)) fallar(donde, 'una ruta con `...spread` no se puede leer: declarala completa');
    const nombre = p.name && (ts.isIdentifier(p.name) || ts.isStringLiteral(p.name)) ? p.name.text : null;
    if (!nombre) fallar(donde, 'propiedad de ruta con nombre calculado: no se puede leer');
    props.set(nombre, p);
  }

  if (props.has('matcher')) {
    fallar(donde, 'una ruta top-level con `matcher` no se puede traducir a nginx: agregá su soporte a este script');
  }

  const valor = nombre => {
    const p = props.get(nombre);
    if (!p) return undefined;
    if (!ts.isPropertyAssignment(p)) fallar(donde, `\`${nombre}\` tiene que ser \`${nombre}: '...'\``);
    return p.initializer;
  };

  const nodoPath = valor('path');
  const ruta = textoLiteral(ts, nodoPath);
  if (ruta === null) fallar(donde, '`path` tiene que ser un texto literal');

  const nodoPathMatch = valor('pathMatch');
  const pathMatch = nodoPathMatch === undefined ? 'prefix' : textoLiteral(ts, nodoPathMatch);
  if (pathMatch !== 'prefix' && pathMatch !== 'full') fallar(donde, "`pathMatch` tiene que ser 'full' o 'prefix' literal");

  return {
    path: ruta,
    pathMatch,
    donde,
    esRedirect: props.has('redirectTo'),
    tieneLoadChildren: props.has('loadChildren'),
    hijos: props.has('children') ? valor('children') : null
  };
}

function leerLista(ts, sf, lista) {
  return lista.elements.map(el => leerRuta(ts, sf, el));
}

/** Rutas cuyo primer segmento cuelga de la raíz: las top-level más los hijos de un `''` sin componente propio. */
function rutasDeLaRaiz(ts, sf, rutas) {
  const salida = [];
  for (const r of rutas) {
    if (r.path === '**') continue;
    if (r.path !== '') {
      salida.push(r);
      continue;
    }
    // '' es la raíz, que se agrega siempre. Si además envuelve rutas (layout), esas rutas son top-level.
    if (r.tieneLoadChildren) {
      fallar(r.donde, "una ruta '' con `loadChildren` esconde las rutas top-level en otro archivo: " +
        'declaralas en app.config.ts o agregá su soporte a este script');
    }
    if (r.hijos) {
      if (!ts.isArrayLiteralExpression(r.hijos)) fallar(r.donde, "los `children` de la ruta '' tienen que ser un array literal");
      salida.push(...rutasDeLaRaiz(ts, sf, leerLista(ts, sf, r.hijos)));
    }
  }
  return salida;
}

/**
 * Rutas top-level de `provideRouter([...])`, normalizadas.
 * @returns {{ path: string, prefijo: boolean, donde: string }[]}
 */
function extraerRutasTopLevel(codigo, archivo = 'app.config.ts') {
  const ts = cargarTypescript();
  const sf = ts.createSourceFile(archivo, codigo, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);

  const llamadas = [];
  const visitar = nodo => {
    if (ts.isCallExpression(nodo) && ts.isIdentifier(nodo.expression) && nodo.expression.text === 'provideRouter') {
      llamadas.push(nodo);
    }
    ts.forEachChild(nodo, visitar);
  };
  visitar(sf);

  const nombre = path.basename(archivo);
  if (llamadas.length !== 1) {
    fallar(nombre, `se esperaba exactamente un provideRouter(...) y hay ${llamadas.length}`);
  }
  const lista = llamadas[0].arguments[0];
  if (!lista || !ts.isArrayLiteralExpression(lista)) {
    fallar(ubicacionDe(ts, sf, llamadas[0]), 'provideRouter(...) tiene que recibir el array literal de rutas');
  }

  const rutas = rutasDeLaRaiz(ts, sf, leerLista(ts, sf, lista)).map(r => ({
    path: r.path,
    // Con hijos (o un redirect que arrastra el resto de la URL) hay subrutas válidas debajo.
    prefijo: r.hijos !== null || r.tieneLoadChildren || (r.esRedirect && r.pathMatch === 'prefix'),
    donde: r.donde
  }));

  if (rutas.length === 0) {
    fallar(nombre, 'no se encontró ninguna ruta top-level además de la raíz: el login quedaría en 404');
  }
  return rutas;
}

/** Traduce el `path` de una ruta a regex (sin anclas). */
function patronDePath(ruta) {
  const segmentos = ruta.path.split('/');
  segmentos.forEach((s, i) => {
    if (s === '') fallar(ruta.donde, `path '${ruta.path}' con una barra de más`);
    if (s === '**') fallar(ruta.donde, `path '${ruta.path}' con '**' en medio`);
    if (i === 0 && s.startsWith(':')) {
      fallar(ruta.donde, `la ruta top-level '${ruta.path}' empieza con un parámetro: coincidiría con ` +
        'cualquier dirección y la lista blanca dejaría de existir');
    }
    if (!s.startsWith(':') && !/^[A-Za-z0-9_-]+$/.test(s)) {
      fallar(ruta.donde, `el segmento '${s}' tiene caracteres fuera de [A-Za-z0-9_-]: agregá su escape a este script`);
    }
  });
  return segmentos
    .map((s, i) => {
      if (!s.startsWith(':')) return s;
      const esUltimo = i === segmentos.length - 1;
      return esUltimo && !ruta.prefijo ? SEGMENTO_FINAL : SEGMENTO;
    })
    .join('/');
}

/**
 * Patrones (regex como texto, igual en PCRE y en JS) de las rutas del SPA. La raíz `/` va aparte.
 * @returns {{ exactas: string[], prefijos: string[], patrones: string[], regex: RegExp[] }}
 */
function construirPatron(rutas) {
  const prefijos = [];
  const exactas = [];
  for (const r of rutas) {
    const p = patronDePath(r);
    const destino = r.prefijo ? prefijos : exactas;
    if (!destino.includes(p)) destino.push(p);
  }
  // Si un path aparece como hoja y como módulo, el prefijo ya lo cubre.
  const soloExactas = exactas.filter(p => !prefijos.includes(p));

  const patrones = [];
  if (soloExactas.length) patrones.push(`^/(?:${soloExactas.join('|')})/?$`);
  if (prefijos.length) patrones.push(`^/(?:${prefijos.join('|')})${COLA_PREFIJO}$`);

  return { exactas: soloExactas, prefijos, patrones, regex: patrones.map(p => new RegExp(p)) };
}

/** ¿nginx serviría el index.html para este `$uri` (path ya decodificado, sin query)? */
function esRutaSpa(uri, patron) {
  return uri === '/' || patron.regex.some(r => r.test(uri));
}

/** Contenido de `/etc/nginx/rutas-spa.conf` (contexto `http`: lo incluye nginx.conf fuera del `server`). */
function generarConfNginx(rutas) {
  const patron = construirPatron(rutas);
  const lineas = [
    '# =============================================================================',
    '# GENERADO por frontend/scripts/rutas-spa-nginx.js desde src/app/app.config.ts.',
    '# NO EDITAR: el Dockerfile lo regenera en cada build.',
    '# =============================================================================',
    '# $ruta_spa = 1 -> el bloque 4 de nginx.conf sirve el index.html del SPA.',
    '# $ruta_spa = 0 -> 404. Una ruta que no existe no puede responder 200 (hallazgo',
    '#                  de seguridad sep-2026: /env respondía 200 con el index).',
    `# Exactas (${patron.exactas.length}): ${patron.exactas.join(', ')}`,
    `# Con subrutas (${patron.prefijos.length}): ${patron.prefijos.join(', ')}`,
    '# =============================================================================',
    'map $uri $ruta_spa {',
    '  default 0;',
    '  /       1;',
    ...patron.patrones.map(p => `  "~${p}" 1;`),
    '}',
    ''
  ];
  return lineas.join('\n');
}

module.exports = { FUENTE_RUTAS, extraerRutasTopLevel, construirPatron, esRutaSpa, generarConfNginx };

if (require.main === module) {
  try {
    const args = process.argv.slice(2);
    const rutas = extraerRutasTopLevel(fs.readFileSync(FUENTE_RUTAS, 'utf8'), FUENTE_RUTAS);
    const conf = generarConfNginx(rutas);

    if (args.includes('--listar')) {
      const patron = construirPatron(rutas);
      console.log(`Exactas (${patron.exactas.length}):   ${patron.exactas.join(', ')}`);
      console.log(`Con subrutas (${patron.prefijos.length}): ${patron.prefijos.join(', ')}`);
      patron.patrones.forEach(p => console.log(`  ${p}`));
    } else if (args.includes('--salida')) {
      const destino = args[args.indexOf('--salida') + 1];
      if (!destino) throw new Error('--salida necesita la ruta del archivo');
      fs.writeFileSync(destino, conf);
      console.error(`[rutas-spa-nginx] ${rutas.length} rutas top-level -> ${destino}`);
    } else {
      process.stdout.write(conf);
    }
  } catch (e) {
    console.error(`[rutas-spa-nginx] ERROR: ${e.message}`);
    process.exit(1);
  }
}

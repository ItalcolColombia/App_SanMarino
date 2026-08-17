#!/usr/bin/env node

/**
 * Contrasta la lista blanca de la caché offline contra los endpoints que la app **realmente** pide.
 *
 * ## Por qué hace falta un script y no alcanza con leer el código
 *
 * La lista de `decidir-cacheable.funcion.ts` son cadenas sueltas, y las URL de los servicios se
 * arman con `${environment.apiUrl}/Recurso` — o sea que **nada** conecta las dos cosas: un nombre mal
 * escrito en la lista no rompe el build, no rompe ningún test, y el único síntoma es que esa pantalla
 * no funciona sin red. Eso no se descubre en la oficina; se descubre en la granja.
 *
 * La primera corrida de este script encontró que la lista cubría 23 de 78 endpoints reales y tenía
 * 7 entradas que no existían, una de ellas un typo (`lotepostorabase` por `loteposturabase`).
 *
 * ## Qué falla y qué no
 *
 * La decisión de *qué* cachear es de producto (¿este módulo tiene que andar sin red?) y el script no
 * la puede tomar. Lo que sí es objetivamente un defecto —y por eso corta— es que la lista y el código
 * queden **desincronizados**:
 *
 *   - una entrada de la lista blanca que la app nunca pide (typo: no hace nada y nadie se entera);
 *   - un endpoint que la app pide y no está ni en la lista blanca ni en EXCLUIDOS (nadie lo miró).
 *
 * Arreglar el segundo es agregar una línea al archivo correspondiente, no implementar nada. El
 * mensaje dice exactamente dónde. Con `--informe` no falla nunca (el comportamiento viejo), para
 * poder mirar el estado sin bloquear.
 *
 * Uso:  node scripts/verificar-lista-cacheable.js [--informe]
 */

const fs = require('fs');
const path = require('path');

const RAIZ = path.join(__dirname, '..');
const APP = path.join(RAIZ, 'src/app');
const FUENTE_LISTA = path.join(APP, 'shared/offline/funciones/decidir-cacheable.funcion.ts');

/** Extrae las cadenas de un array `const NOMBRE: ... = [ '...', '...' ]`. */
function leerLista(src, nombre) {
  const m = new RegExp(`${nombre}[^=]*=\\s*\\[(.*?)\\]`, 's').exec(src);
  if (!m) return new Set();
  return new Set([...m[1].matchAll(/'([^']+)'/g)].map(x => x[1]));
}

/** Recorre `src/app` salteando los specs (sus URL son inventadas). */
function archivosTs(dir, acc = []) {
  for (const entrada of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entrada.name);
    if (entrada.isDirectory()) archivosTs(p, acc);
    else if (entrada.name.endsWith('.ts') && !entrada.name.endsWith('.spec.ts')) acc.push(p);
  }
  return acc;
}

// `environment.apiUrl` ya termina en `/api`, así que el recurso es el primer segmento que le sigue.
const PATRONES = [
  /(?:baseUrl|base|url|API|apiBase)\s*=\s*`\$\{environment\.apiUrl\}\/([A-Za-z][A-Za-z0-9_-]*)/g,
  /\$\{(?:environment\.)?apiUrl\}\/([A-Za-z][A-Za-z0-9_-]*)/g,
  /apiUrl\s*\+\s*['"]\/?([A-Za-z][A-Za-z0-9_-]*)/g,
  /\/api\/([A-Za-z][A-Za-z0-9_-]*)/g
];

const src = fs.readFileSync(FUENTE_LISTA, 'utf8');
const blanca = leerLista(src, 'ENDPOINTS_OPERATIVOS');
const excluidos = leerLista(src, 'EXCLUIDOS');

/** recurso -> archivos donde aparece */
const usados = new Map();
for (const archivo of archivosTs(APP)) {
  const texto = fs.readFileSync(archivo, 'utf8');
  for (const patron of PATRONES) {
    for (const m of texto.matchAll(patron)) {
      const recurso = m[1].toLowerCase();
      if (!usados.has(recurso)) usados.set(recurso, new Set());
      usados.get(recurso).add(path.relative(APP, archivo));
    }
  }
}

const recursos = [...usados.keys()].sort();
const cacheables = recursos.filter(r => blanca.has(r));
const excluidosUsados = recursos.filter(r => excluidos.has(r));
const sinCubrir = recursos.filter(r => !blanca.has(r) && !excluidos.has(r));
const fantasma = [...blanca].filter(r => !usados.has(r)).sort();

console.log(`[lista-cacheable] Endpoints distintos que la app pide: ${recursos.length}`);
console.log(`[lista-cacheable]   cacheables sin red : ${cacheables.length}`);
console.log(`[lista-cacheable]   excluidos a propósito: ${excluidosUsados.length}`);
console.log(`[lista-cacheable]   sin decisión tomada : ${sinCubrir.length}`);
for (const r of sinCubrir) {
  console.log(`[lista-cacheable]      - ${r}  (${[...usados.get(r)][0]})`);
}

if (fantasma.length) {
  console.warn(
    `\n[lista-cacheable] ⚠️  ${fantasma.length} entrada(s) de la lista blanca que la app NUNCA pide.` +
      `\n   Suele ser un nombre mal escrito: la entrada no hace nada y esa pantalla no anda sin red.`
  );
  for (const r of fantasma) console.warn(`[lista-cacheable]      - ${r}`);
}

const soloInforme = process.argv.includes('--informe');

if (!sinCubrir.length && !fantasma.length) {
  console.log('\n[lista-cacheable] OK: lista y código sincronizados.');
  process.exit(0);
}

console.error(
  `\n[lista-cacheable] FALLA: la lista blanca y el código están desincronizados.\n` +
    `   Cada endpoint "sin decisión tomada" se agrega a ENDPOINTS_OPERATIVOS si tiene que funcionar\n` +
    `   sin red, o a EXCLUIDOS si no debe guardarse (dinero, identidad, herramientas internas), en\n` +
    `   src/app/shared/offline/funciones/decidir-cacheable.funcion.ts. Es una línea, con su comentario.\n` +
    `   Las entradas "fantasma" se corrigen o se borran: hoy no hacen nada.`
);

process.exit(soloInforme ? 0 : 1);

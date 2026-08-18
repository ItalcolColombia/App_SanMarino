#!/usr/bin/env node

/**
 * Gate anti-regresión: todo `@Component` tiene que declarar `changeDetection` de forma explícita.
 *
 * ## Por qué existe este script
 *
 * En Angular 22 el default del decorador cambió: **omitir `changeDetection` es `OnPush`**. Un
 * componente con estado mutable que asigna desde un `subscribe` de `HttpClient` no marca la vista
 * sucia, así que la plantilla nunca se repinta: el síntoma es el modal que se queda en «Cargando…»
 * para siempre, con la request devolviendo 200 en la pestaña Network y cero errores en consola.
 *
 * Eso ya pasó dos veces (`configurar-alcance-granja`, jul-2026; los 13 componentes de Vacunación e
 * Implementación, 15-ago-2026) y **nada lo detectaba**: compila, los tests pasan y el defecto solo
 * aparece abriendo la pantalla a mano. Un componente nuevo que se olvide la propiedad vuelve a
 * introducirlo, y por eso la comprobación tiene que ser de máquina.
 *
 * ## Qué exige y qué no
 *
 * No decide la estrategia por nadie: exige que **haya una elegida**. `Eager` es la convención del
 * repo para componentes con estado mutable, `subscribe`, `async/await` o timers; `OnPush` es válido
 * para los 100 % presentacionales o los que escriben su estado con señales.
 *
 * `Default` sí se rechaza: está deprecado en v22 (es alias de `Eager`) y CLAUDE.md lo prohíbe en
 * código nuevo.
 *
 * Uso:  node scripts/verificar-change-detection.js [--informe]
 */

const fs = require('fs');
const path = require('path');

const APP = path.join(__dirname, '..', 'src');

/** Recorre `src` juntando los `.ts` que no son specs. */
function archivosTs(dir, acc = []) {
  for (const entrada of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entrada.name);
    if (entrada.isDirectory()) archivosTs(p, acc);
    else if (entrada.name.endsWith('.ts') && !entrada.name.endsWith('.spec.ts')) acc.push(p);
  }
  return acc;
}

/**
 * Devuelve el texto del objeto literal que recibe `@Component(...)`, contando paréntesis.
 *
 * Hace falta contar en vez de usar una regex: el literal trae `template`/`styles` inline con
 * paréntesis adentro, así que cualquier `\)` no balanceado corta en el lugar equivocado.
 */
function cuerpoDelDecorador(texto, desde) {
  let profundidad = 0;
  for (let i = desde; i < texto.length; i++) {
    const c = texto[i];
    if (c === '(') profundidad++;
    else if (c === ')') {
      profundidad--;
      if (profundidad === 0) return texto.slice(desde, i + 1);
    }
  }
  return null;
}

const faltantes = [];
const deprecados = [];
let total = 0;

for (const archivo of archivosTs(APP)) {
  const texto = fs.readFileSync(archivo, 'utf8');
  for (const m of texto.matchAll(/@Component\s*\(/g)) {
    const cuerpo = cuerpoDelDecorador(texto, m.index + m[0].lastIndexOf('('));
    if (cuerpo === null) continue;

    total++;
    const relativo = path.relative(path.join(__dirname, '..'), archivo);
    const linea = texto.slice(0, m.index).split('\n').length;

    if (!/\bchangeDetection\s*:/.test(cuerpo)) {
      faltantes.push(`${relativo}:${linea}`);
    } else if (/ChangeDetectionStrategy\.Default\b/.test(cuerpo)) {
      deprecados.push(`${relativo}:${linea}`);
    }
  }
}

console.log(`[change-detection] Componentes revisados: ${total}`);
console.log(`[change-detection]   sin changeDetection : ${faltantes.length}`);
console.log(`[change-detection]   con Default (deprecado): ${deprecados.length}`);

if (!faltantes.length && !deprecados.length) {
  console.log('[change-detection] OK: todos declaran su estrategia.');
  process.exit(0);
}

for (const x of faltantes) console.error(`[change-detection]   FALTA  ${x}`);
for (const x of deprecados) console.error(`[change-detection]   DEPRECADO  ${x}`);

console.error(
  `\n[change-detection] FALLA: en Angular 22 omitir \`changeDetection\` equivale a OnPush, y un\n` +
    `   componente con estado mutable que asigna desde un \`subscribe\` se queda colgado en pantalla\n` +
    `   aunque la request devuelva 200. Agregá la propiedad al \`@Component\`:\n` +
    `      changeDetection: ChangeDetectionStrategy.Eager   // estado mutable, subscribe, timers\n` +
    `      changeDetection: ChangeDetectionStrategy.OnPush   // 100 % presentacional o con señales\n` +
    `   \`Default\` está deprecado en v22 (alias de Eager): usá \`Eager\`.`
);

process.exit(process.argv.includes('--informe') ? 0 : 1);

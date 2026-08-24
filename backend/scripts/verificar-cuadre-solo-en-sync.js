#!/usr/bin/env node

/**
 * Gate anti-regresión: **sólo `SyncPushService` puede marcar una operación como `requiere_cuadre`.**
 *
 * ## Por qué existe este script
 *
 * F7 del plan `fase_de_desarrollo/descuento_inventario_movil_plan.md` dice, textual: *"la política no
 * puede viajar en la excepción... tiene que actuar en la decisión, antes del throw"*. El diseño lo
 * respeta poniendo la ÚNICA decisión de "esto va a la bandeja de cuadre, no se rechaza" en
 * `SyncPushService` (F7): ahí se atrapa `StockInsuficienteException`, se reintenta SIN los ítems de
 * inventario, y recién entonces se escribe `Estado = requiere_cuadre`.
 *
 * Si otro service copiara ese patrón (por ejemplo, para "que a mí también me funcione" en el camino
 * directo que usa la web/app móvil), dejaría de ser cierto lo que dice el comentario de
 * `SyncPushCalculos.Estados.RequiereCuadre`: que el estado sólo lo emite el push offline por lote. Y
 * el camino directo (F3) depende de lo contrario — de que CUALQUIER falta de stock deshaga TODO el
 * seguimiento — así que un segundo emisor ahí sería, literalmente, desandar F3.
 *
 * ## Qué exige y qué no
 *
 * Busca el literal `"requiere_cuadre"` y la constante `SyncPushCalculos.Estados.RequiereCuadre` en
 * TODO `backend/src/`, fuera de `Services/Sync/` (donde vive `SyncPushService` y sus partial). Un uso
 * de LECTURA (comparar contra el estado, ej. en la bandeja o en un reporte) está permitido en
 * cualquier lado — sólo se prohíbe la ASIGNACIÓN (`Estado = ...requiere_cuadre` o
 * `Estado = "requiere_cuadre"`).
 *
 * Uso:  node scripts/verificar-cuadre-solo-en-sync.js [--informe]
 */

const fs = require('fs');
const path = require('path');

const RAIZ = path.join(__dirname, '..', 'src');
const DIR_PERMITIDO = path.join('Services', 'Sync') + path.sep;

/** Asignación al estado, con la constante tipada o el literal crudo. */
const ASIGNA_REQUIERE_CUADRE = [
  /Estado\s*=\s*SyncPushCalculos\.Estados\.RequiereCuadre/,
  /Estado\s*=\s*"requiere_cuadre"/,
];

function listarCs(dir, salida = []) {
  for (const entrada of fs.readdirSync(dir, { withFileTypes: true })) {
    const completo = path.join(dir, entrada.name);
    if (entrada.isDirectory()) listarCs(completo, salida);
    else if (entrada.name.endsWith('.cs')) salida.push(completo);
  }
  return salida;
}

/** Quita comentarios de línea y de bloque para no marcar una mención en la documentación. */
function sinComentarios(src) {
  return src.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/\/?.*$/gm, '');
}

const infracciones = [];

for (const archivo of listarCs(RAIZ)) {
  const relativo = path.relative(path.join(__dirname, '..'), archivo);
  if (relativo.includes(DIR_PERMITIDO)) continue;

  const codigo = sinComentarios(fs.readFileSync(archivo, 'utf8'));
  const lineas = codigo.split('\n');

  lineas.forEach((linea, i) => {
    if (ASIGNA_REQUIERE_CUADRE.some((re) => re.test(linea))) {
      infracciones.push({ archivo: relativo, linea: i + 1, texto: linea.trim() });
    }
  });
}

if (!infracciones.length) {
  console.log('[cuadre] OK: requiere_cuadre sólo se asigna dentro de Services/Sync/.');
  process.exit(0);
}

for (const x of infracciones) {
  console.error(`[cuadre]   ASIGNA  ${x.archivo}:${x.linea}  ->  ${x.texto}`);
}

console.error(
  `\n[cuadre] FALLA: algo fuera de Services/Sync/ está asignando el estado requiere_cuadre.\n` +
    `   Esa decisión es de SyncPushService (F7) porque se toma ANTES del throw, reintentando el\n` +
    `   push sin los ítems de inventario — no es un catch genérico sobre cualquier rechazo de\n` +
    `   stock. Un segundo emisor puede terminar marcando "para cuadre" un fallo del camino directo\n` +
    `   (F3), que en cambio necesita deshacer TODO el seguimiento si falta stock.\n` +
    `   Si de verdad hace falta un segundo emisor, es una decisión de diseño a propósito, no un\n` +
    `   catch copiado — discutilo antes de tocar este gate.`
);

process.exit(process.argv.includes('--informe') ? 0 : 1);

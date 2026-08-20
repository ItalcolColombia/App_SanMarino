#!/usr/bin/env node

/**
 * Gate anti-regresión: **una pantalla de seguimiento no postea movimientos de inventario.**
 *
 * ## Por qué existe este script
 *
 * El descuento de alimento lo aplica el BACKEND al guardar el seguimiento. Cuando además lo postea
 * el front, los mismos kg salen dos veces: una del inventario legacy (`farm_inventory_movements`,
 * por `catalogItemId`) y otra del unificado (`inventario_gestion_movimiento`), que es donde
 * `InventarioConsumoGate` manda hoy a los tres países.
 *
 * **Ya pasó dos veces, con el mismo código copiado:**
 *
 * 1. `modal-seguimiento-levante` — lo quitó el commit `8e9bbc1` (10-jul-2026), *«duplicaba el
 *    descuento que el backend ya aplicaba sobre el inventario nuevo»*. Dejó 252 movimientos /
 *    131.278,3 kg en la tabla vieja que después descuadraron el Reporte Contable, y hubo que
 *    excluirlos de la columna RETIROS (`EsConsumoYaContabilizadoPorSeguimiento`, `473ac16`).
 * 2. `modal-seguimiento-engorde` — sobrevivió a esa limpieza porque los dos modales se unificaron
 *    después, y se quitó recién el 19-ago-2026. La referencia que escribía todavía decía
 *    «Consumo diario levante», que es la huella de haber sido copiado.
 *
 * Nada lo detectaba: compila, los tests pasan, y el doble descuento sólo se ve meses después como un
 * saldo que no cuadra. Por eso la comprobación tiene que ser de máquina.
 *
 * ## Qué exige y qué no
 *
 * No prohíbe escribir inventario: prohíbe hacerlo **desde fuera del módulo de inventario**. Los
 * formularios del propio módulo (`features/inventario/`) son los que registran un movimiento porque
 * el usuario lo pidió explícitamente — ese es su trabajo y siguen permitidos.
 *
 * Lo que se rechaza es que una pantalla de captura diaria (seguimiento, traslado, liquidación…)
 * descuente por su cuenta: ahí el descuento es un EFECTO del guardado, y el efecto lo aplica el
 * backend, en la misma transacción y validando stock antes de commitear.
 *
 * Uso:  node scripts/verificar-front-no-descuenta-inventario.js [--informe]
 */

const fs = require('fs');
const path = require('path');

const RAIZ = path.join(__dirname, '..', 'src', 'app');

/** Métodos del front que escriben un movimiento de inventario. */
const ESCRITORES = ['postExit', 'postEntry'];

/** Endpoints crudos, por si alguien saltea el service y pega con HttpClient directo. */
const ENDPOINTS = ['inventory/movements/out', 'inventory/movements/in'];

/**
 * Único lugar autorizado: el módulo de inventario. Ahí registrar un movimiento ES la acción que el
 * usuario pidió, no un efecto colateral de otra pantalla.
 */
const PERMITIDO = path.join('features', 'inventario') + path.sep;

function listarTs(dir, salida = []) {
  for (const entrada of fs.readdirSync(dir, { withFileTypes: true })) {
    const completo = path.join(dir, entrada.name);
    if (entrada.isDirectory()) listarTs(completo, salida);
    else if (entrada.name.endsWith('.ts') && !entrada.name.endsWith('.spec.ts')) salida.push(completo);
  }
  return salida;
}

/** Quita comentarios de línea y de bloque para no marcar una mención en la documentación. */
function sinComentarios(src) {
  return src.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');
}

const infracciones = [];

for (const archivo of listarTs(RAIZ)) {
  const relativo = path.relative(path.join(__dirname, '..'), archivo);
  if (relativo.includes(PERMITIDO)) continue;

  const codigo = sinComentarios(fs.readFileSync(archivo, 'utf8'));
  const lineas = codigo.split('\n');

  lineas.forEach((linea, i) => {
    const escritor = ESCRITORES.find((m) => new RegExp(`\\.${m}\\s*\\(`).test(linea));
    const endpoint = ENDPOINTS.find((e) => linea.includes(e));
    if (escritor || endpoint) {
      infracciones.push({ archivo: relativo, linea: i + 1, que: escritor || endpoint });
    }
  });
}

if (!infracciones.length) {
  console.log('[inventario] OK: ninguna pantalla fuera de features/inventario/ postea movimientos.');
  process.exit(0);
}

for (const x of infracciones) {
  console.error(`[inventario]   ESCRIBE  ${x.archivo}:${x.linea}  (${x.que})`);
}

console.error(
  `\n[inventario] FALLA: una pantalla fuera de \`features/inventario/\` está posteando movimientos de\n` +
    `   inventario. El descuento de un seguimiento lo aplica el BACKEND al guardar —con bloqueo\n` +
    `   atómico: valida el stock antes de commitear y descuenta en la misma transacción—, así que\n` +
    `   hacerlo también acá saca los mismos kg dos veces, de dos inventarios distintos.\n` +
    `   Ya pasó en el modal de levante (\`8e9bbc1\`) y en el de engorde: los 252 movimientos que dejó\n` +
    `   el primero descuadraron el Reporte Contable durante meses.\n` +
    `   Si de verdad hace falta registrar un movimiento explícito, va en \`features/inventario/\`.`
);

process.exit(process.argv.includes('--informe') ? 0 : 1);

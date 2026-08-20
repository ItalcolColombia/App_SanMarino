#!/usr/bin/env node

/**
 * Gate anti-regresión: **todo objeto de BD que la app necesita llega a producción por MIGRACIÓN.**
 *
 * ## Por qué existe este script
 *
 * `backend/sql/` tiene 254 archivos y **no hay ningún runner que los aplique**: ni al arrancar la
 * app, ni en el deploy. Lo único que corre solo en producción son las migraciones EF
 * (`Database__RunMigrations=true` en la TaskDef). O sea que un `fn_*.sql` o `vw_*.sql` que viva sólo
 * como archivo **nunca existe en producción**, aunque el repo lo muestre como si estuviera hecho.
 *
 * Eso ya pasó, y por eso este gate no es teórico. Medido el 20-ago-2026 contra la copia de prod:
 *
 * - `vw_validacion_alimento_engorde` (1-jun-2026): **no existe en la BD** y nadie lo lee. Tres meses
 *   de un archivo que parece trabajo entregado y no llegó a ningún lado.
 * - `vw_seguimiento_pollo_engorde_add_company_id` (16-abr-2026): tampoco existe como vista, pero sus
 *   columnas SÍ están vivas — alguien las plegó dentro de la migración de la vista principal. El
 *   archivo quedó como un fragmento superado que aparenta ser un pendiente.
 *
 * Ninguno de los dos era un hueco funcional. El daño es el otro: **hacen perder tiempo y confunden
 * el estado real del sistema.** La regla convierte «¿esto está desplegado?» en algo que se responde
 * mirando, no adivinando.
 *
 * ## Qué exige y qué no
 *
 * Exige que cada `fn_*.sql` y `vw_*.sql` esté **nombrado por al menos una migración**. El `.sql` es
 * el espejo legible del objeto; la migración es el vehículo que lo aplica.
 *
 * Si un archivo legítimamente no lleva migración, se declara **en el propio archivo** con una línea:
 *
 *     -- SIN-MIGRACION: <motivo concreto>
 *
 * y el gate lo acepta. Declararlo es barato; el objetivo es que nadie se entere por casualidad.
 *
 * **Exentos por prefijo, y por qué:**
 * - `verificar_*.sql` — diagnósticos de SOLO LECTURA. No crean ni modifican nada: se corren a mano
 *   contra un dump para medir. Migrarlos no tendría sentido.
 * - `migracion_*.sql` y `backfill_*.sql` — operativos de una sola vez, que se corren de forma
 *   controlada y quedan como registro de lo que se hizo.
 *
 * Uso:  node scripts/verificar-sql-llega-por-migracion.js [--informe]
 */

const fs = require('fs');
const path = require('path');

const RAIZ = path.join(__dirname, '..');
const DIR_SQL = path.join(RAIZ, 'sql');
const DIR_MIG = path.join(RAIZ, 'src', 'ZooSanMarino.Infrastructure', 'Migrations');

/** Los objetos persistentes que la app consulta: si no están en prod, algo falla en silencio. */
const EXIGEN_MIGRACION = [/^fn_.*\.sql$/, /^vw_.*\.sql$/];

/** Prefijos exentos, con su motivo en el doc de arriba. */
const EXENTOS = [/^verificar_/, /^migracion_/, /^backfill_/];

/** Marca para declarar una excepción desde el propio archivo. */
const MARCA = /^--\s*SIN-MIGRACION:\s*(.+)$/im;

if (!fs.existsSync(DIR_SQL) || !fs.existsSync(DIR_MIG)) {
  console.error(`[sql-migracion] No encuentro ${DIR_SQL} o ${DIR_MIG}`);
  process.exit(1);
}

// Todo el texto de las migraciones, de una sola vez. Se excluyen los Designer (son el snapshot del
// modelo, no el DDL) para que un nombre que aparece sólo ahí no cuente como "migrado".
const textoMigraciones = fs
  .readdirSync(DIR_MIG)
  .filter((f) => f.endsWith('.cs') && !f.endsWith('.Designer.cs'))
  .map((f) => fs.readFileSync(path.join(DIR_MIG, f), 'utf8'))
  .join('\n');

const huerfanos = [];
const declarados = [];

for (const archivo of fs.readdirSync(DIR_SQL).sort()) {
  if (!EXIGEN_MIGRACION.some((r) => r.test(archivo))) continue;
  if (EXENTOS.some((r) => r.test(archivo))) continue;

  const nombreObjeto = archivo.replace(/\.sql$/, '');
  if (textoMigraciones.includes(nombreObjeto)) continue;

  const marca = MARCA.exec(fs.readFileSync(path.join(DIR_SQL, archivo), 'utf8'));
  if (marca) declarados.push({ archivo, motivo: marca[1].trim() });
  else huerfanos.push(archivo);
}

for (const d of declarados) {
  console.log(`[sql-migracion]   declarado sin migración  ${d.archivo}  — ${d.motivo}`);
}

if (!huerfanos.length) {
  console.log('[sql-migracion] OK: todo fn_/vw_ tiene migración que lo aplica, o lo declara.');
  process.exit(0);
}

for (const h of huerfanos) console.error(`[sql-migracion]   SIN MIGRACIÓN  sql/${h}`);

console.error(
  `\n[sql-migracion] FALLA: hay SQL que la app necesita y que producción nunca va a tener.\n` +
    `   Nada aplica \`backend/sql/\` solo: ni el arranque ni el deploy. Lo único que corre en\n` +
    `   producción son las migraciones EF. Un \`fn_\`/\`vw_\` que vive sólo como archivo parece\n` +
    `   trabajo entregado y no existe en la BD.\n` +
    `   Creá la migración que lo aplique (idempotente: CREATE OR REPLACE / IF NOT EXISTS), o\n` +
    `   declaralo en el propio archivo con una línea:\n` +
    `      -- SIN-MIGRACION: <motivo concreto>`
);

process.exit(process.argv.includes('--informe') ? 0 : 1);

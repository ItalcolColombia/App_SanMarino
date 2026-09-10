#!/usr/bin/env node
/**
 * Gate: el nombre interno del módulo de base de datos NO viaja al navegador.
 *
 * Por qué existe
 * --------------
 * En la auditoría externa de septiembre-2026 el analista encontró `/api/DbStudio` haciendo
 * `grep` sobre el `main.js` de producción, y lo marcó como "interfaz de administración de base de
 * datos, verificación manual prioritaria". La respuesta fue renombrar la ruta a
 * `/api/ConfigColores` y el módulo front a `config-colores` (nombre-señuelo).
 *
 * El checkbox que validó ese rename fue:
 *     grep -rn "db-studio\|DbStudio\|db_studio" frontend/src   ⇒ 0
 * ...que es CASE-SENSITIVE, y por eso no vio la forma que sí quedó: el DTO del backend exponía
 * `DbStudioConnections`, que serializa como `dbStudioConnections` y el front lo consumía POR
 * NOMBRE en su modelo y en su template. O sea: el literal seguía en el bundle y un
 * `grep -i dbstudio main.js` —exactamente lo que corrió el analista— volvía a dar positivo.
 *
 * Este gate es ese grep, pero case-insensitive y corriendo en el CI.
 *
 * Alcance
 * -------
 * Solo `frontend/src`: lo que se compila al bundle. El backend conserva a propósito el nombre
 * interno (clases, servicios, config `DbStudio:`, tablas `dbstudio_*`, permiso `db_studio.admin`).
 *
 * Tolerancia cero, sin allowlist: si algún día hace falta una excepción, que se discuta en el PR
 * tocando este archivo. Recordá que esto es obscurity/defensa en profundidad — el control real es
 * la doble validación del backend (correo autorizado Y rol admin). Que este gate pase no autoriza
 * a relajar aquello.
 *
 * Uso:  node scripts/verificar-senuelo-modulo.js     (desde frontend/)
 * Sale 1 si encuentra cualquier ocurrencia.
 */

const fs = require('fs');
const path = require('path');

const RAIZ = path.resolve(__dirname, '..', 'src');

/** Formas prohibidas. Se comparan sobre el texto en minúsculas. */
const PROHIBIDOS = ['dbstudio', 'db-studio', 'db_studio'];

/** Extensiones que terminan (o pueden terminar) dentro del bundle. */
const EXTENSIONES = new Set(['.ts', '.html', '.scss', '.css', '.json', '.js']);

const IGNORAR_DIR = new Set(['node_modules', '.git', 'dist', '.angular']);

/** @returns {string[]} rutas absolutas de archivos a revisar */
function listarArchivos(dir) {
  const salida = [];
  for (const entrada of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entrada.isDirectory()) {
      if (IGNORAR_DIR.has(entrada.name)) continue;
      salida.push(...listarArchivos(path.join(dir, entrada.name)));
    } else if (EXTENSIONES.has(path.extname(entrada.name))) {
      salida.push(path.join(dir, entrada.name));
    }
  }
  return salida;
}

function main() {
  if (!fs.existsSync(RAIZ)) {
    console.error(`No existe ${RAIZ}. ¿Se corrió desde frontend/?`);
    process.exit(1);
  }

  const hallazgos = [];
  let revisados = 0;

  for (const archivo of listarArchivos(RAIZ)) {
    revisados++;
    const lineas = fs.readFileSync(archivo, 'utf8').split(/\r?\n/);
    lineas.forEach((linea, i) => {
      const minuscula = linea.toLowerCase();
      for (const prohibido of PROHIBIDOS) {
        if (minuscula.includes(prohibido)) {
          hallazgos.push({
            archivo: path.relative(path.resolve(__dirname, '..'), archivo).replace(/\\/g, '/'),
            linea: i + 1,
            termino: prohibido,
            texto: linea.trim().slice(0, 160),
          });
          break; // una marca por línea alcanza
        }
      }
    });
  }

  if (hallazgos.length > 0) {
    console.error('');
    console.error('❌ El nombre interno del módulo de base de datos llegaría al bundle.');
    console.error('   Estas ocurrencias se compilan y quedan visibles con `grep -i` sobre main.js,');
    console.error('   que es exactamente como la auditoría de sep-2026 encontró el módulo.');
    console.error('');
    for (const h of hallazgos) {
      console.error(`   ${h.archivo}:${h.linea}  [${h.termino}]`);
      console.error(`      ${h.texto}`);
    }
    console.error('');
    console.error(`   ${hallazgos.length} ocurrencia(s) en ${revisados} archivo(s) revisado(s).`);
    console.error('   Renombrá con un nombre neutro (ambos lados del contrato si es un campo JSON).');
    console.error('   Ver: fase_de_desarrollo/gates_hallazgos_auditoria_2026-09_plan.md');
    console.error('');
    process.exit(1);
  }

  console.log(`✅ Señuelo intacto: 0 ocurrencias de ${PROHIBIDOS.join(' / ')} en ${revisados} archivo(s) de frontend/src.`);
}

main();

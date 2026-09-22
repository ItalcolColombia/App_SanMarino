#!/usr/bin/env node
/**
 * Arma el árbol de la wiki de GitHub a partir de `backend/documentacion`. No publica nada: deja los
 * archivos listos en un directorio (normalmente el clon de `App_SanMarino.wiki.git`) y el push es aparte.
 *
 * Por qué existe
 * --------------
 * La wiki nombra cada página por su archivo, SIN la carpeta, y no conoce el resto del repo. Copiar la
 * carpeta tal cual deja páginas que chocan (hay tres `README.md`) y links que no llevan a ningún lado
 * (`OTRO.md` en vez de `OTRO`, `../sql/...` fuera de la wiki). El contenido se publica tal cual; acá
 * solo se ajusta lo que la wiki necesita para funcionar:
 *
 *   - `README.md` de una subcarpeta → página con el nombre de la carpeta; el de la raíz → `Documentacion`.
 *   - Link a otro `.md` de la carpeta → nombre de página de wiki (conserva el `#ancla`).
 *   - Link que sale de la carpeta o apunta a un archivo que no es `.md` → URL absoluta en GitHub.
 *   - `Home.md` (portada con índice por carpeta), `_Sidebar.md` (menú) y `_Footer.md` (de dónde sale).
 *
 * Los links dentro de bloques de código (```) no se tocan. Falla si dos páginas terminarían con el
 * mismo nombre.
 *
 * Uso:  node backend/scripts/generar-wiki-documentacion.js <destino> [origen=backend/documentacion]
 *       (borra el contenido de <destino> salvo su carpeta .git)
 */

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const REPO_BLOB = 'https://github.com/ItalcolColombia/App_SanMarino/blob/main/';
const RAIZ_EN_REPO = 'backend/documentacion';
const PAGINA_RAIZ = 'Documentacion';
const ESPECIALES = new Set(['home', '_sidebar', '_footer']);

const esMarkdown = (ruta) => /\.md$/i.test(ruta);

/** Nombre de página de wiki de un `.md` (ruta relativa a la carpeta de documentación, con `/`). */
function nombrePagina(rutaRel) {
  const base = path.posix.basename(rutaRel).replace(/\.md$/i, '');
  if (base.toLowerCase() === 'readme') {
    const dir = path.posix.dirname(rutaRel);
    return dir === '.' ? PAGINA_RAIZ : path.posix.basename(dir);
  }
  return base.replace(/ /g, '-');
}

/** Mapa ruta (minúsculas) → nombre de página. Falla ante nombres repetidos o reservados. */
function mapaDePaginas(rutasMd) {
  const paginas = new Map();
  const usados = new Map();
  for (const ruta of rutasMd) {
    const nombre = nombrePagina(ruta);
    const clave = nombre.toLowerCase();
    if (ESPECIALES.has(clave)) throw new Error(`"${ruta}" usaría el nombre reservado de la wiki "${nombre}".`);
    if (usados.has(clave)) throw new Error(`"${ruta}" y "${usados.get(clave)}" serían la misma página "${nombre}".`);
    usados.set(clave, ruta);
    paginas.set(ruta.toLowerCase(), nombre);
  }
  return paginas;
}

/** Reescribe un destino de link; devuelve `null` si hay que dejarlo como está. */
function destinoEnWiki(destino, rutaRel, paginas, sinResolver) {
  if (/^[a-z][a-z0-9+.-]*:/i.test(destino) || destino.startsWith('#')) return null;

  const i = destino.indexOf('#');
  const ruta = i < 0 ? destino : destino.slice(0, i);
  const ancla = i < 0 ? '' : destino.slice(i);
  if (!ruta) return null;

  let decodificada;
  try { decodificada = decodeURI(ruta); } catch { decodificada = ruta; }
  const resuelta = path.posix.normalize(path.posix.join(path.posix.dirname(rutaRel), decodificada));

  if (resuelta.startsWith('../') || resuelta === '..') {
    const enRepo = path.posix.normalize(path.posix.join(RAIZ_EN_REPO, resuelta));
    return enRepo.startsWith('..') ? null : REPO_BLOB + encodeURI(enRepo) + ancla;
  }
  if (esMarkdown(resuelta)) {
    const pagina = paginas.get(resuelta.toLowerCase());
    if (pagina) return pagina + ancla;
    sinResolver.push(`${rutaRel} → ${destino}`);
    return null;
  }
  return REPO_BLOB + encodeURI(`${RAIZ_EN_REPO}/${resuelta}`) + ancla;
}

/** Reescribe los links `](...)` de un markdown, fuera de los bloques de código. */
function reescribirLinks(contenido, rutaRel, paginas, sinResolver = []) {
  const partes = contenido.split(/(^```[^\n]*\n[\s\S]*?^```[^\n]*$)/m);
  return partes
    .map((parte, i) => (i % 2 === 1
      ? parte
      : parte.replace(/\]\(([^)\s]+)((?:\s+"[^"]*")?)\)/g, (todo, destino, titulo) => {
        const nuevo = destinoEnWiki(destino, rutaRel, paginas, sinResolver);
        return nuevo === null ? todo : `](${nuevo}${titulo})`;
      })))
    .join('');
}

function listarArchivos(raiz, rel = '') {
  const salida = [];
  for (const entrada of fs.readdirSync(path.join(raiz, rel), { withFileTypes: true })) {
    const r = rel ? `${rel}/${entrada.name}` : entrada.name;
    if (entrada.isDirectory()) salida.push(...listarArchivos(raiz, r));
    else salida.push(r);
  }
  return salida.sort((a, b) => a.localeCompare(b));
}

const titulo = (pagina) => pagina.replace(/[_-]+/g, ' ').trim();
const grupo = (rutaRel) => (rutaRel.includes('/') ? rutaRel.split('/')[0] : '');

function indice(rutasMd, paginas, encabezado) {
  const grupos = new Map();
  for (const ruta of rutasMd) {
    const g = grupo(ruta);
    if (!grupos.has(g)) grupos.set(g, []);
    grupos.get(g).push(paginas.get(ruta.toLowerCase()));
  }
  const orden = [...grupos.keys()].sort((a, b) => (a === '' ? -1 : b === '' ? 1 : a.localeCompare(b)));
  return orden.map((g) => {
    const lista = grupos.get(g).map((p) => `- [${titulo(p)}](${p})`).join('\n');
    return `${encabezado} ${g === '' ? 'General' : g}\n\n${lista}`;
  }).join('\n\n');
}

function commitActual(origen) {
  try {
    return execFileSync('git', ['log', '-1', '--format=%h', '--', '.'], { cwd: origen, encoding: 'utf8' }).trim();
  } catch {
    return '';
  }
}

/** Genera la wiki en `destino` a partir de `origen`. Devuelve el resumen. */
function generarWiki(origen, destino, { commit = commitActual(origen) } = {}) {
  const archivos = listarArchivos(origen);
  const rutasMd = archivos.filter(esMarkdown);
  const paginas = mapaDePaginas(rutasMd);
  const sinResolver = [];

  fs.mkdirSync(destino, { recursive: true });
  for (const entrada of fs.readdirSync(destino)) {
    if (entrada !== '.git') fs.rmSync(path.join(destino, entrada), { recursive: true, force: true });
  }

  for (const ruta of archivos) {
    const dir = path.posix.dirname(ruta);
    const carpetaDestino = path.join(destino, dir === '.' ? '' : dir);
    fs.mkdirSync(carpetaDestino, { recursive: true });
    if (esMarkdown(ruta)) {
      const contenido = fs.readFileSync(path.join(origen, ruta), 'utf8');
      const salida = reescribirLinks(contenido, ruta, paginas, sinResolver);
      fs.writeFileSync(path.join(carpetaDestino, `${paginas.get(ruta.toLowerCase())}.md`), salida);
    } else {
      fs.copyFileSync(path.join(origen, ruta), path.join(carpetaDestino, path.posix.basename(ruta)));
    }
  }

  const origenTxt = `\`${RAIZ_EN_REPO}\`${commit ? ` @ \`${commit}\`` : ''}`;
  fs.writeFileSync(path.join(destino, 'Home.md'),
    `# Documentación — ZooSanMarino\n\n` +
    `Espejo de ${origenTxt} ([ver en el repo](${REPO_BLOB}${RAIZ_EN_REPO})). ` +
    `${rutasMd.length} páginas.\n\n` +
    `> Esta wiki se genera desde el repositorio. Los cambios se hacen en \`${RAIZ_EN_REPO}\`: lo que se ` +
    `edite acá se pierde en la próxima publicación.\n\n` +
    `${indice(rutasMd, paginas, '##')}\n`);
  fs.writeFileSync(path.join(destino, '_Sidebar.md'),
    `**[Inicio](Home)**\n\n${indice(rutasMd, paginas, '####')}\n`);
  fs.writeFileSync(path.join(destino, '_Footer.md'),
    `Generado desde ${origenTxt}. Editá en el repositorio, no en la wiki.\n`);

  return { paginas: rutasMd.length, adjuntos: archivos.length - rutasMd.length, sinResolver };
}

function main(argv) {
  const [destino, origen = RAIZ_EN_REPO] = argv;
  if (!destino) {
    console.error('Uso: node backend/scripts/generar-wiki-documentacion.js <destino> [origen]');
    return 2;
  }
  const r = generarWiki(path.resolve(origen), path.resolve(destino));
  console.log(`Wiki generada: ${r.paginas} páginas, ${r.adjuntos} adjuntos, Home + _Sidebar + _Footer.`);
  if (r.sinResolver.length) {
    console.log(`Links a .md que no existen en la carpeta (quedan como estaban): ${r.sinResolver.length}`);
    for (const l of r.sinResolver) console.log(`  - ${l}`);
  }
  return 0;
}

module.exports = { nombrePagina, mapaDePaginas, reescribirLinks, generarWiki, REPO_BLOB };

if (require.main === module) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (e) {
    console.error(`Error: ${e.message}`);
    process.exitCode = 1;
  }
}

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { nombrePagina, mapaDePaginas, reescribirLinks, generarWiki, REPO_BLOB } = require('../generar-wiki-documentacion');

const BLOB = `${REPO_BLOB}backend/documentacion`;

test('nombrePagina: README de subcarpeta = carpeta; README raiz = Documentacion; espacios = guiones', () => {
  assert.equal(nombrePagina('aws-infrastructure/README.md'), 'aws-infrastructure');
  assert.equal(nombrePagina('README.md'), 'Documentacion');
  assert.equal(nombrePagina('GUIA_DESPLIEGUE.md'), 'GUIA_DESPLIEGUE');
  assert.equal(nombrePagina('sub/mi guia.md'), 'mi-guia');
});

test('mapaDePaginas: dos archivos con el mismo nombre de pagina -> error', () => {
  assert.throws(() => mapaDePaginas(['a/GUIA.md', 'b/guia.md']), /serían la misma página/);
  assert.throws(() => mapaDePaginas(['Home.md']), /reservado/);
});

test('reescribirLinks: links internos a paginas de wiki, con ancla y titulo', () => {
  const paginas = mapaDePaginas(['README.md', 'X.md', 'sub/Y.md', 'sub/README.md']);
  const md = [
    '[a](X.md) [b](./X.md#uso) [c](sub/Y.md) [d](sub/README.md) [e](README.md)',
    '[f](X.md "titulo")',
  ].join('\n');
  assert.equal(reescribirLinks(md, 'README.md', paginas),
    '[a](X) [b](X#uso) [c](Y) [d](sub) [e](Documentacion)\n[f](X "titulo")');
  assert.equal(reescribirLinks('[g](../X.md) [h](Y.md)', 'sub/Y.md', paginas), '[g](X) [h](Y)');
});

test('reescribirLinks: lo que sale de la carpeta o no es .md va a GitHub', () => {
  const paginas = mapaDePaginas(['A.md', 'sub/B.md']);
  assert.equal(reescribirLinks('[s](../sql/fn_x.sql)', 'A.md', paginas),
    `[s](${REPO_BLOB}backend/sql/fn_x.sql)`);
  assert.equal(reescribirLinks('[p](../../fase_de_desarrollo/p.md#x)', 'A.md', paginas),
    `[p](${REPO_BLOB}fase_de_desarrollo/p.md#x)`);
  assert.equal(reescribirLinks('[s](../../sql/f.sql)', 'sub/B.md', paginas),
    `[s](${REPO_BLOB}backend/sql/f.sql)`);
  assert.equal(reescribirLinks('[j](datos.json)', 'A.md', paginas), `[j](${BLOB}/datos.json)`);
  assert.equal(reescribirLinks('[r](resumen%20de%20consola.txt)', 'A.md', paginas),
    `[r](${BLOB}/resumen%20de%20consola.txt)`);
});

test('reescribirLinks: externos, anclas, mailto y bloques de codigo quedan intactos', () => {
  const paginas = mapaDePaginas(['A.md', 'X.md']);
  const md = '[w](https://x.com/a.md) [n](#seccion) [m](mailto:a@b.c)\n```\n[code](X.md)\n```\n[fuera](X.md)';
  assert.equal(reescribirLinks(md, 'A.md', paginas),
    '[w](https://x.com/a.md) [n](#seccion) [m](mailto:a@b.c)\n```\n[code](X.md)\n```\n[fuera](X)');
});

test('reescribirLinks: un .md que no existe queda igual y se informa', () => {
  const sinResolver = [];
  assert.equal(reescribirLinks('[x](NO_EXISTE.md)', 'A.md', mapaDePaginas(['A.md']), sinResolver), '[x](NO_EXISTE.md)');
  assert.deepEqual(sinResolver, ['A.md → NO_EXISTE.md']);
});

test('generarWiki: arbol completo, conserva .git del destino y el contenido tal cual', () => {
  const base = fs.mkdtempSync(path.join(os.tmpdir(), 'wiki-'));
  const origen = path.join(base, 'doc');
  const destino = path.join(base, 'wiki');
  try {
    fs.mkdirSync(path.join(origen, 'sub'), { recursive: true });
    fs.writeFileSync(path.join(origen, 'README.md'), '# Raiz\nclave: valor-tal-cual\n[a](A.md)');
    fs.writeFileSync(path.join(origen, 'A.md'), '# A\n[b](sub/README.md)');
    fs.writeFileSync(path.join(origen, 'sub', 'README.md'), '# Sub');
    fs.writeFileSync(path.join(origen, 'sub', 'datos.xlsx'), 'binario');
    fs.mkdirSync(path.join(destino, '.git'), { recursive: true });
    fs.writeFileSync(path.join(destino, '.git', 'HEAD'), 'ref');
    fs.writeFileSync(path.join(destino, 'Vieja.md'), 'se borra');

    const r = generarWiki(origen, destino, { commit: 'abc1234' });

    assert.deepEqual(r, { paginas: 3, adjuntos: 1, sinResolver: [] });
    assert.equal(fs.readFileSync(path.join(destino, '.git', 'HEAD'), 'utf8'), 'ref');
    assert.equal(fs.existsSync(path.join(destino, 'Vieja.md')), false);
    assert.equal(fs.readFileSync(path.join(destino, 'Documentacion.md'), 'utf8'), '# Raiz\nclave: valor-tal-cual\n[a](A)');
    assert.equal(fs.readFileSync(path.join(destino, 'A.md'), 'utf8'), '# A\n[b](sub)');
    assert.equal(fs.readFileSync(path.join(destino, 'sub', 'sub.md'), 'utf8'), '# Sub');
    assert.equal(fs.readFileSync(path.join(destino, 'sub', 'datos.xlsx'), 'utf8'), 'binario');
    const home = fs.readFileSync(path.join(destino, 'Home.md'), 'utf8');
    assert.match(home, /## General[\s\S]*\[A\]\(A\)[\s\S]*\[Documentacion\]\(Documentacion\)/);
    assert.match(home, /## sub[\s\S]*\[sub\]\(sub\)/);
    assert.match(fs.readFileSync(path.join(destino, '_Sidebar.md'), 'utf8'), /\[Inicio\]\(Home\)/);
    assert.match(fs.readFileSync(path.join(destino, '_Footer.md'), 'utf8'), /abc1234/);
  } finally {
    fs.rmSync(base, { recursive: true, force: true });
  }
});

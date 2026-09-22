'use strict';

// Contrato del paso «Rotar clave JWT en la TaskDef» del deploy. Corre en el job del back antes de
// rotar: si algo de esto se rompe, no hay deploy.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
const { rotarClaveEnTaskDef, claveNueva, CLAVE, ANTERIOR } = require('../rotar-clave-jwt-taskdef');

const SCRIPT = path.join(__dirname, '..', 'rotar-clave-jwt-taskdef.js');
const VIEJA = 'clave-vieja-de-la-taskdef-0123456789-0123456789-0123456789';
const NUEVA = 'clave-nueva-del-deploy-0123456789-0123456789-0123456789-01';

const taskDef = (environment, extra = {}) => ({
  family: 'sanmarino-back-task',
  containerDefinitions: [
    { name: 'frontend', environment: [{ name: CLAVE, value: 'no-tocar' }] },
    { name: 'backend', image: 'img:1', environment, ...extra },
  ],
});
const env = (td) => td.containerDefinitions.find((c) => c.name === 'backend').environment;
const valor = (td, nombre) => (env(td).find((e) => e.name === nombre) || {}).value;

test('con clave previa: la nueva firma y la previa pasa a PreviousKey', () => {
  const { taskDef: td, estado } = rotarClaveEnTaskDef(taskDef([{ name: CLAVE, value: VIEJA }]), 'backend', NUEVA);
  assert.equal(estado, 'rotada');
  assert.equal(valor(td, CLAVE), NUEVA);
  assert.equal(valor(td, ANTERIOR), VIEJA);
});

test('sin clave previa: solo Key, sin PreviousKey', () => {
  const { taskDef: td, estado } = rotarClaveEnTaskDef(taskDef([]), 'backend', NUEVA);
  assert.equal(estado, 'primera');
  assert.equal(valor(td, CLAVE), NUEVA);
  assert.equal(valor(td, ANTERIOR), undefined);
});

test('una PreviousKey vieja se reemplaza: nunca quedan dos ni tres claves', () => {
  const entrada = taskDef([{ name: ANTERIOR, value: 'la-de-hace-dos-deploys' }, { name: CLAVE, value: VIEJA }]);
  const { taskDef: td } = rotarClaveEnTaskDef(entrada, 'backend', NUEVA);
  assert.equal(env(td).filter((e) => e.name === CLAVE).length, 1);
  assert.equal(env(td).filter((e) => e.name === ANTERIOR).length, 1);
  assert.equal(valor(td, ANTERIOR), VIEJA);
});

test('el resto de las variables, la imagen y los otros contenedores quedan intactos', () => {
  const entrada = taskDef([
    { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' },
    { name: CLAVE, value: VIEJA },
    { name: 'JwtSettings__DurationInMinutes', value: '60' },
  ]);
  const copia = JSON.parse(JSON.stringify(entrada));
  const { taskDef: td } = rotarClaveEnTaskDef(entrada, 'backend', NUEVA);

  assert.deepEqual(entrada, copia, 'no muta la entrada');
  assert.equal(valor(td, 'ASPNETCORE_ENVIRONMENT'), 'Production');
  assert.equal(valor(td, 'JwtSettings__DurationInMinutes'), '60');
  assert.equal(td.containerDefinitions[1].image, 'img:1');
  assert.deepEqual(td.containerDefinitions[0], copia.containerDefinitions[0]);
  assert.equal(td.family, 'sanmarino-back-task');
});

test('si la clave sale de secrets (Secrets Manager) no se toca', () => {
  const entrada = taskDef([], { secrets: [{ name: CLAVE, valueFrom: 'arn:aws:secretsmanager:...' }] });
  const { taskDef: td, estado } = rotarClaveEnTaskDef(entrada, 'backend', NUEVA);
  assert.equal(estado, 'secrets');
  assert.equal(td, entrada);
});

test('contenedor ausente: error, no se inventa nada', () => {
  assert.throws(() => rotarClaveEnTaskDef(taskDef([]), 'otro', NUEVA), /no tiene el contenedor "otro"/);
});

test('claveNueva: 64 bytes aleatorios en base64 (88 caracteres, >= 32 bytes que exige la API)', () => {
  const a = claveNueva();
  const b = claveNueva();
  assert.equal(a.length, 88);
  assert.equal(Buffer.from(a, 'base64').length, 64);
  assert.notEqual(a, b);
  assert.doesNotMatch(a, /Development|YOUR_|REEMPLAZAR|CHANGE_ME/i);
});

test('CLI: rota el archivo y la salida nunca trae una clave sin enmascarar', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'rotar-jwt-'));
  const ruta = path.join(dir, 'td.json');
  try {
    fs.writeFileSync(ruta, JSON.stringify(taskDef([{ name: CLAVE, value: VIEJA }])));
    const salida = execFileSync(process.execPath, [SCRIPT, ruta], { encoding: 'utf8' });
    const td = JSON.parse(fs.readFileSync(ruta, 'utf8'));
    const nueva = valor(td, CLAVE);

    assert.equal(valor(td, ANTERIOR), VIEJA);
    assert.equal(nueva.length, 88);
    const lineas = salida.split(/\r?\n/);
    for (const clave of [nueva, VIEJA]) {
      const conClave = lineas.filter((l) => l.includes(clave));
      assert.deepEqual(conClave, [`::add-mask::${clave}`], 'la clave solo aparece en su ::add-mask::');
    }
    assert.match(salida, /Clave JWT rotada/);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

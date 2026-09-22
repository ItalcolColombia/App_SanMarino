'use strict';

// Contrato del paso «Confiar en el ALB (ReverseProxy) en la TaskDef» del deploy. Corre en el job del
// back antes de editar la TaskDef: si algo de esto se rompe, no hay deploy.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const net = require('net');
const { execFileSync } = require('child_process');
const { confiarProxyEnTaskDef, REDES_PRIVADAS } = require('../confiar-proxy-alb-taskdef');

const SCRIPT = path.join(__dirname, '..', 'confiar-proxy-alb-taskdef.js');

const taskDef = (environment, extra = {}) => ({
  family: 'sanmarino-back-task',
  containerDefinitions: [
    { name: 'frontend', environment: [{ name: 'X', value: 'no-tocar' }] },
    { name: 'backend', image: 'img:1', environment, ...extra },
  ],
});
const env = (td) => td.containerDefinitions.find((c) => c.name === 'backend').environment;
const redes = (td) => env(td).filter((e) => e.name.startsWith('ReverseProxy__KnownNetworks__'));

test('sin ReverseProxy: agrega las tres redes privadas, indexadas 0..2', () => {
  const { taskDef: td, estado } = confiarProxyEnTaskDef(taskDef([{ name: 'A', value: '1' }]), 'backend');
  assert.equal(estado, 'agregada');
  assert.deepEqual(redes(td), [
    { name: 'ReverseProxy__KnownNetworks__0', value: '10.0.0.0/8' },
    { name: 'ReverseProxy__KnownNetworks__1', value: '172.16.0.0/12' },
    { name: 'ReverseProxy__KnownNetworks__2', value: '192.168.0.0/16' },
  ]);
});

test('las redes son CIDR IPv4 privados con prefijo > 0 (lo que exige HttpSecurityConfiguration)', () => {
  for (const red of REDES_PRIVADAS) {
    const [ip, prefijo] = red.split('/');
    assert.ok(net.isIPv4(ip), red);
    assert.ok(Number(prefijo) > 0 && Number(prefijo) <= 32, red);
  }
});

test('si ya hay KnownNetworks o KnownProxies (environment o secrets) no se toca nada', () => {
  for (const entrada of [
    taskDef([{ name: 'ReverseProxy__KnownNetworks__0', value: '10.1.0.0/16' }]),
    taskDef([{ name: 'ReverseProxy__KnownProxies__0', value: '10.1.2.3' }]),
    taskDef([], { secrets: [{ name: 'ReverseProxy__KnownNetworks__0', valueFrom: 'arn:...' }] }),
  ]) {
    const { taskDef: td, estado } = confiarProxyEnTaskDef(entrada, 'backend');
    assert.equal(estado, 'existente');
    assert.equal(td, entrada);
  }
});

test('nunca toca ForwardLimit (un solo salto: el ALB, sin CloudFront delante)', () => {
  const { taskDef: td } = confiarProxyEnTaskDef(taskDef([]), 'backend');
  assert.equal(env(td).some((e) => e.name === 'ReverseProxy__ForwardLimit'), false);
});

test('idempotente: dos pasadas dejan lo mismo que una', () => {
  const una = confiarProxyEnTaskDef(taskDef([]), 'backend').taskDef;
  const dos = confiarProxyEnTaskDef(una, 'backend');
  assert.equal(dos.estado, 'existente');
  assert.deepEqual(dos.taskDef, una);
});

test('el resto de las variables, la imagen y los otros contenedores quedan intactos', () => {
  const entrada = taskDef([{ name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }, { name: 'JwtSettings__Key', value: 'k' }]);
  const copia = JSON.parse(JSON.stringify(entrada));
  const { taskDef: td } = confiarProxyEnTaskDef(entrada, 'backend');

  assert.deepEqual(entrada, copia, 'no muta la entrada');
  assert.deepEqual(env(td).slice(0, 2), copia.containerDefinitions[1].environment);
  assert.equal(td.containerDefinitions[1].image, 'img:1');
  assert.deepEqual(td.containerDefinitions[0], copia.containerDefinitions[0]);
});

test('contenedor ausente: error, no se inventa nada', () => {
  assert.throws(() => confiarProxyEnTaskDef(taskDef([]), 'otro'), /no tiene el contenedor "otro"/);
});

test('CLI: escribe el archivo y una segunda corrida lo deja igual', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'proxy-alb-'));
  const ruta = path.join(dir, 'td.json');
  try {
    fs.writeFileSync(ruta, JSON.stringify(taskDef([])));
    const salida1 = execFileSync(process.execPath, [SCRIPT, ruta], { encoding: 'utf8' });
    const tras1 = fs.readFileSync(ruta, 'utf8');
    const salida2 = execFileSync(process.execPath, [SCRIPT, ruta], { encoding: 'utf8' });

    assert.equal(redes(JSON.parse(tras1)).length, 3);
    assert.match(salida1, /ALB como proxy conocido/);
    assert.match(salida2, /se respeta sin cambios/);
    assert.equal(fs.readFileSync(ruta, 'utf8'), tras1);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

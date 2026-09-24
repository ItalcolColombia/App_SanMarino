'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { leerEnvironment, leerJson, combinarConfiguracion, validarContrato, informar } = require('../verificar-contrato-seguridad-produccion');

// Valores sintéticos, sin copiar ninguna clave del repositorio ni de producción.
function fixture() {
  const frontend = {
    production: true, apiUrl: '/api',
    encryptionKeys: { remitenteFrontend: 'fixture-ida', remitenteBackend: 'fixture-vuelta' },
    platformSecret: { secretUpFrontend: 'fixture-legacy', encryptionKey: 'fixture-cifrado' }
  };
  const backend = {
    Encryption: { RemitenteFrontend: frontend.encryptionKeys.remitenteFrontend, RemitenteBackend: frontend.encryptionKeys.remitenteBackend },
    PlatformSecret: { SecretUpFrontend: frontend.platformSecret.secretUpFrontend, EncryptionKey: frontend.platformSecret.encryptionKey, DerivationKey: 'fixture-derivacion-servidor' },
    JwtSettings: { Key: 'fixture-exclusivo-servidor-'.repeat(2), PreviousKey: 'fixture-anterior-servidor-'.repeat(2), Issuer: 'fixture-emisor', Audience: 'fixture-audiencia' }
  };
  const reemplazo = { replace: 'src/environments/environment.ts', with: 'src/environments/environment.prod.ts' };
  const angular = { projects: { frontend: { architect: { build: {
    defaultConfiguration: 'production',
    configurations: { production: { fileReplacements: [{ ...reemplazo }] }, docker: { fileReplacements: [{ ...reemplazo }] } }
  } } } } };
  return { frontend, backend, angular };
}

function validar(datos, override = {}) {
  return validarContrato(datos.frontend, combinarConfiguracion(datos.backend, override), datos.angular);
}

test('contrato válido con duración JWT por defecto y claves distintas por sentido', () => {
  const resultado = validar(fixture());
  assert.equal(resultado.errores.length, 0);
  assert.equal(resultado.avisos.length, 0);
});

for (const [grupo, nombre, grupoBack, nombreBack] of [
  ['encryptionKeys', 'remitenteFrontend', 'Encryption', 'RemitenteFrontend'],
  ['encryptionKeys', 'remitenteBackend', 'Encryption', 'RemitenteBackend'],
  ['platformSecret', 'secretUpFrontend', 'PlatformSecret', 'SecretUpFrontend'],
  ['platformSecret', 'encryptionKey', 'PlatformSecret', 'EncryptionKey']
]) {
  test(`detecta diferencias y ausencia en cada extremo: ${nombre}`, () => {
    for (const lado of ['frontend', 'backend']) {
      for (const valor of [undefined, '', '   ', 'fixture-diferente']) {
        const datos = fixture();
        if (lado === 'frontend') datos.frontend[grupo][nombre] = valor;
        else datos.backend[grupoBack][nombreBack] = valor;
        assert.ok(validar(datos).errores.length > 0);
      }
    }
  });
}

test('merge de Production preserva hermanos y sobreescribe hojas sin distinguir mayúsculas', () => {
  const datos = fixture();
  const nuevo = 'fixture-override';
  datos.frontend.encryptionKeys.remitenteFrontend = nuevo;
  assert.equal(validar(datos, { encryption: { remitenteFRONTEND: nuevo } }).errores.length, 0);
  assert.ok(validar(datos, { encryption: { remitenteFRONTEND: '' } }).errores.length > 0);
  assert.ok(validar(datos).errores.length > 0);
});

test('Unicode se compara exacto, sin normalizar ni recortar', () => {
  const datos = fixture();
  datos.frontend.encryptionKeys.remitenteFrontend = 'fixture-caf\u00e9';
  datos.backend.Encryption.RemitenteFrontend = 'fixture-caf\u0065\u0301';
  assert.ok(validar(datos).errores.length > 0);
  datos.backend.Encryption.RemitenteFrontend = datos.frontend.encryptionKeys.remitenteFrontend;
  assert.equal(validar(datos).errores.length, 0);
  datos.backend.Encryption.RemitenteFrontend += ' ';
  assert.ok(validar(datos).errores.length > 0);
});

test('JWT cuenta bytes UTF-8 y rechaza opciones inválidas', () => {
  const datos = fixture();
  datos.backend.JwtSettings.Key = '\u00e9'.repeat(16);
  assert.equal(validar(datos).errores.length, 0);
  for (const [campo, valores] of Object.entries({
    Key: [undefined, '', 'x'.repeat(31)], Issuer: [null, '', ' '], Audience: [undefined, ''],
    DurationInMinutes: [0, -1, 0.5, '1.5', '', null, '1e3', 2147483648]
  })) {
    for (const valor of valores) {
      const invalido = fixture();
      invalido.backend.JwtSettings[campo] = valor;
      assert.ok(validar(invalido).errores.length > 0);
    }
  }
  datos.backend.JwtSettings.DurationInMinutes = '240';
  assert.equal(validar(datos).errores.length, 0);
});

test('claves exclusivas del backend no se admiten embebidas en valores frontend', () => {
  for (const [grupo, nombre] of [['JwtSettings', 'Key'], ['JwtSettings', 'PreviousKey'], ['PlatformSecret', 'DerivationKey']]) {
    const datos = fixture();
    datos.frontend.extra = { arreglo: [`prefijo-${datos.backend[grupo][nombre]}-sufijo`] };
    assert.ok(validar(datos).errores.some(e => e.includes('exclusiva del servidor')));
  }
});

test('legacy sin DerivationKey y secretUpBackend extra generan avisos sin exigir igualdad', () => {
  const datos = fixture();
  delete datos.backend.PlatformSecret.DerivationKey;
  datos.frontend.platformSecret.secretUpBackend = 'fixture-extra-sin-consumidor';
  datos.backend.PlatformSecret.SecretUpBackend = 'fixture-otro-valor';
  datos.frontend.recaptcha = { siteKey: 'fixture-publica' };
  datos.backend.Recaptcha = { SecretKey: 'fixture-privada' };
  const resultado = validar(datos);
  assert.equal(resultado.errores.length, 0);
  assert.equal(resultado.avisos.length, 2);
});

test('detecta builds development o reemplazos ausentes, incorrectos y duplicados', () => {
  const datos = fixture();
  datos.angular.projects.frontend.architect.build.defaultConfiguration = 'development';
  assert.ok(validar(datos).errores.length > 0);
  for (const modo of ['production', 'docker']) {
    for (const tipo of ['ausente', 'incorrecto', 'duplicado']) {
      const invalido = fixture();
      const configuracion = invalido.angular.projects.frontend.architect.build.configurations[modo];
      if (tipo === 'ausente') delete configuracion.fileReplacements;
      if (tipo === 'incorrecto') configuracion.fileReplacements[0].with = 'src/environments/environment.ts';
      if (tipo === 'duplicado') configuracion.fileReplacements.push({ ...configuracion.fileReplacements[0] });
      assert.ok(validar(invalido).errores.length > 0);
    }
  }
});

test('producción exige flag y URL segura', () => {
  for (const url of ['http://api.example/api', '//api.example/api', '/otro', 'https://usuario@api.example/api', '']) {
    const datos = fixture();
    datos.frontend.apiUrl = url;
    assert.ok(validar(datos).errores.length > 0);
  }
  const datos = fixture();
  datos.frontend.apiUrl = 'https://api.example/api';
  assert.equal(validar(datos).errores.length, 0);
  datos.frontend.production = false;
  assert.ok(validar(datos).errores.length > 0);
});

test('AST lee literales y rechaza expresiones sin ejecutarlas', () => {
  const datos = fixture();
  const source = `export const environment = ${JSON.stringify(datos.frontend)} as const;`;
  assert.equal(validarContrato(leerEnvironment(source), combinarConfiguracion(datos.backend), datos.angular).errores.length, 0);
  for (const source of [
    'export const environment = (() => { throw new Error("fixture-marcador"); })();',
    'export const environment = { ...otraVariable };',
    'export const environment = { get valor() { return "fixture-marcador"; } };',
    'export const environment = { a: "uno", a: "dos" };',
    'export const environment = {;'
  ]) assert.throws(() => leerEnvironment(source), error => !error.message.includes('fixture-marcador'));
});

test('JSON admite comentarios y comas finales pero rechaza sintaxis inválida', () => {
  const config = leerJson('{ /* comentario */ "Encryption": {"RemitenteFrontend": "fixture-json",}, }');
  assert.ok(combinarConfiguracion(config).has('encryption:remitentefrontend'));
  assert.throws(() => leerJson('{ "dato": fixture-marcador }'), error => !error.message.includes('fixture-marcador'));
  assert.throws(() => leerJson(String.raw`{"dato":"fixture-marcador-\q"}`), error => !error.message.includes('fixture-marcador'));
  assert.throws(() => combinarConfiguracion({ Clave: 'uno', clave: 'dos' }));
});

test('salida no revela claves ni marcadores y declara el límite ECS', () => {
  const datos = fixture();
  const marcador = 'fixture-marcador-confidencial';
  datos.frontend.encryptionKeys.remitenteFrontend = marcador;
  datos.frontend.extra = datos.backend.JwtSettings.Key;
  const lineas = [];
  assert.equal(informar(validar(datos), linea => lineas.push(linea)), 1);
  const salida = lineas.join('\n');
  assert.ok(!salida.includes(marcador));
  assert.ok(!salida.includes(datos.backend.JwtSettings.Key));
  assert.ok(!salida.includes(datos.backend.PlatformSecret.DerivationKey));
  assert.ok(salida.includes('NO certifica'));
});

test('JSON no pierde claves duplicadas antes de validar la configuración', () => {
  for (const texto of [
    '{"JwtSettings":{"Key":"fixture-uno","Key":"fixture-dos"}}',
    '{"Encryption":{"RemitenteFrontend":"fixture-uno","remitentefrontend":"fixture-dos"}}',
    '{"Clave":"fixture-uno","clave":"fixture-dos"}'
  ]) assert.throws(() => leerJson(texto), /duplicadas/);
  assert.throws(() => combinarConfiguracion(leerJson('{"A:B":"fixture-uno","A":{"B":"fixture-dos"}}')), /duplicadas/);
});

test('JSON conserva el lexema numérico que .NET convierte a Int32', () => {
  const datos = fixture();
  for (const numero of ['1e3', '120.0', '1E+2']) {
    const override = leerJson(`{"JwtSettings":{"DurationInMinutes":${numero}}}`);
    assert.ok(validar(datos, override).errores.some(error => error.includes('DurationInMinutes')));
  }
  const valido = leerJson('{"JwtSettings":{"DurationInMinutes":120}}');
  assert.equal(validar(datos, valido).errores.length, 0);
  assert.equal(validar(datos, { JwtSettings: { DurationInMinutes: ' +120 ' } }).errores.length, 0);
});

test('rotación JWT y configuración ALB del pipeline conservan el contrato cifrado front/back', () => {
  const { rotarClaveEnTaskDef } = require('../rotar-clave-jwt-taskdef');
  const { confiarProxyEnTaskDef } = require('../confiar-proxy-alb-taskdef');
  const datos = fixture();
  const clavesCompartidas = Object.fromEntries([...combinarConfiguracion(datos.backend)]
    .filter(([nombre]) => nombre.startsWith('encryption:') ||
      ['platformsecret:secretupfrontend', 'platformsecret:encryptionkey'].includes(nombre)));
  let taskDef = { containerDefinitions: [{ name: 'backend', environment: [
    ...Object.entries(clavesCompartidas).map(([name, value]) => ({ name: name.replace(/:/g, '__'), value })),
    { name: 'JwtSettings__Key', value: datos.backend.JwtSettings.Key }
  ] }] };
  for (const nueva of ['fixture-rotacion-uno-'.repeat(3), 'fixture-rotacion-dos-'.repeat(3)]) {
    const anterior = taskDef.containerDefinitions[0].environment.find(e => e.name === 'JwtSettings__Key').value;
    taskDef = confiarProxyEnTaskDef(rotarClaveEnTaskDef(taskDef, 'backend', nueva).taskDef, 'backend').taskDef;
    const env = taskDef.containerDefinitions[0].environment;
    assert.equal(env.find(e => e.name === 'JwtSettings__Key').value, nueva);
    assert.equal(env.find(e => e.name === 'JwtSettings__PreviousKey').value, anterior);
    const override = Object.fromEntries(env.map(e => [e.name.replace(/__/g, ':'), e.value]));
    for (const [nombre, valor] of Object.entries(clavesCompartidas)) assert.equal(override[nombre], valor);
    assert.equal(validar(datos, override).errores.length, 0);
    assert.deepEqual(confiarProxyEnTaskDef(taskDef, 'backend').taskDef, taskDef);
  }
});

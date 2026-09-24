#!/usr/bin/env node
'use strict';

// Comprueba archivos versionados. Los overrides de ECS/Secrets Manager se verifican aparte.
const fs = require('node:fs');
const path = require('node:path');
const RAIZ = path.resolve(__dirname, '../..');
const ALCANCE = 'Validación local: NO certifica los overrides ni los secretos efectivos de ECS.';

function typescript() {
  return require(path.join(RAIZ, 'frontend/node_modules/typescript'));
}

/** Solo literales: nunca importar, evaluar ni ejecutar el environment. */
function leerEnvironment(texto) {
  const ts = typescript();
  const archivo = ts.createSourceFile('environment.prod.ts', texto, ts.ScriptTarget.Latest, true);
  if (archivo.parseDiagnostics.length) throw new Error('Environment de producción inválido.');
  const declaraciones = archivo.statements
    .filter(s => ts.isVariableStatement(s) && s.modifiers?.some(m => m.kind === ts.SyntaxKind.ExportKeyword))
    .flatMap(s => [...s.declarationList.declarations])
    .filter(d => ts.isIdentifier(d.name) && d.name.text === 'environment');
  if (declaraciones.length !== 1) throw new Error('Se requiere un único environment exportado.');

  function literal(nodo) {
    if (!nodo) throw new Error('Environment sin inicializador literal.');
    if (ts.isAsExpression(nodo) || ts.isSatisfiesExpression(nodo) || ts.isParenthesizedExpression(nodo)) {
      return literal(nodo.expression);
    }
    if (ts.isStringLiteral(nodo) || ts.isNoSubstitutionTemplateLiteral(nodo)) return nodo.text;
    if (ts.isNumericLiteral(nodo)) return Number(nodo.text);
    if (nodo.kind === ts.SyntaxKind.TrueKeyword) return true;
    if (nodo.kind === ts.SyntaxKind.FalseKeyword) return false;
    if (nodo.kind === ts.SyntaxKind.NullKeyword) return null;
    if (ts.isArrayLiteralExpression(nodo)) return nodo.elements.map(literal);
    if (ts.isObjectLiteralExpression(nodo)) {
      const objeto = Object.create(null);
      for (const propiedad of nodo.properties) {
        if (!ts.isPropertyAssignment(propiedad) ||
            !(ts.isIdentifier(propiedad.name) || ts.isStringLiteral(propiedad.name))) {
          throw new Error('Environment contiene una propiedad no literal.');
        }
        const nombre = propiedad.name.text;
        if (Object.hasOwn(objeto, nombre)) throw new Error('Environment contiene propiedades duplicadas.');
        objeto[nombre] = literal(propiedad.initializer);
      }
      return objeto;
    }
    throw new Error('Environment contiene una expresión no literal.');
  }
  return literal(declaraciones[0].initializer);
}

function leerJson(texto) {
  // No convertir primero a objetos JS: se perderían duplicados y el texto numérico
  // que IConfiguration entrega al binder (1e3 y 120.0 no son enteros Int32 válidos).
  const ts = typescript();
  const archivo = ts.parseJsonText('config.json', texto.replace(/^\uFEFF/, ''));
  const raiz = archivo.statements[0]?.expression;
  if (archivo.parseDiagnostics.length || archivo.statements.length !== 1 || !raiz || !ts.isObjectLiteralExpression(raiz)) {
    throw new Error('Archivo de configuración JSON inválido.');
  }
  function cadena(nodo) {
    try { return JSON.parse(nodo.getText(archivo)); }
    catch { throw new Error('Cadena JSON inválida.'); }
  }
  function literal(nodo) {
    if (ts.isObjectLiteralExpression(nodo)) {
      const objeto = Object.create(null);
      const nombres = new Set();
      for (const propiedad of nodo.properties) {
        if (!ts.isPropertyAssignment(propiedad) || !ts.isStringLiteral(propiedad.name) ||
            !propiedad.name.getText(archivo).startsWith('"')) {
          throw new Error('Propiedad JSON inválida.');
        }
        const nombre = cadena(propiedad.name);
        if (nombres.has(nombre.toLowerCase())) throw new Error('Configuración JSON con propiedades duplicadas.');
        nombres.add(nombre.toLowerCase());
        objeto[nombre] = literal(propiedad.initializer);
      }
      return objeto;
    }
    if (ts.isArrayLiteralExpression(nodo)) return nodo.elements.map(literal);
    if (ts.isStringLiteral(nodo) && nodo.getText(archivo).startsWith('"')) return cadena(nodo);
    if (nodo.kind === ts.SyntaxKind.TrueKeyword) return true;
    if (nodo.kind === ts.SyntaxKind.FalseKeyword) return false;
    if (nodo.kind === ts.SyntaxKind.NullKeyword) return null;
    const numero = nodo.getText(archivo);
    if ((ts.isNumericLiteral(nodo) || ts.isPrefixUnaryExpression(nodo)) &&
        /^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$/.test(numero)) return numero;
    throw new Error('Valor JSON inválido.');
  }
  return literal(raiz);
}

/** Los proveedores IConfiguration sobreescriben hojas por ruta, sin distinguir mayúsculas. */
function combinarConfiguracion(...fuentes) {
  const combinado = new Map();
  for (const fuente of fuentes) {
    const capa = new Map();
    function aplanar(valor, ruta) {
      if (valor && typeof valor === 'object' && Object.keys(valor).length) {
        for (const [nombre, hijo] of Object.entries(valor)) aplanar(hijo, ruta ? `${ruta}:${nombre}` : nombre);
      } else if (ruta) {
        const clave = ruta.toLowerCase();
        if (capa.has(clave)) throw new Error('Configuración con rutas duplicadas.');
        capa.set(clave, valor);
      }
    }
    aplanar(fuente, '');
    for (const [clave, valor] of capa) combinado.set(clave, valor);
  }
  return combinado;
}

function validarContrato(frontend, backend, angular) {
  const errores = [];
  const avisos = [];
  const obtener = clave => backend.get(clave.toLowerCase());
  const presente = valor => typeof valor === 'string' && valor.trim().length > 0;
  const pares = [
    ['encryptionKeys', 'remitenteFrontend', 'Encryption:RemitenteFrontend'],
    ['encryptionKeys', 'remitenteBackend', 'Encryption:RemitenteBackend'],
    ['platformSecret', 'secretUpFrontend', 'PlatformSecret:SecretUpFrontend'],
    ['platformSecret', 'encryptionKey', 'PlatformSecret:EncryptionKey']
  ];
  for (const [grupo, nombre, destino] of pares) {
    const origen = frontend?.[grupo]?.[nombre];
    const contraparte = obtener(destino);
    if (!presente(origen) || !presente(contraparte)) errores.push(`${destino}: falta una clave no vacía en front o back.`);
    else if (origen !== contraparte) errores.push(`${destino}: las claves de front y back no coinciden exactamente.`);
  }

  const jwt = obtener('JwtSettings:Key');
  if (!presente(jwt) || Buffer.byteLength(jwt, 'utf8') < 32) errores.push('JwtSettings:Key requiere al menos 32 bytes UTF-8.');
  for (const nombre of ['Issuer', 'Audience']) {
    if (!presente(obtener(`JwtSettings:${nombre}`))) errores.push(`JwtSettings:${nombre} debe estar configurado.`);
  }
  const duracion = backend.has('jwtsettings:durationinminutes') ? obtener('JwtSettings:DurationInMinutes') : 120;
  if (!/^[+]?[0-9]+$/.test(String(duracion).trim()) || Number(duracion) <= 0 || Number(duracion) > 2147483647) {
    errores.push('JwtSettings:DurationInMinutes debe ser un entero positivo de .NET.');
  }

  const valoresFront = [];
  function recoger(valor) {
    if (typeof valor === 'string') valoresFront.push(valor);
    else if (valor && typeof valor === 'object') Object.values(valor).forEach(recoger);
  }
  recoger(frontend);
  for (const nombre of ['JwtSettings:Key', 'JwtSettings:PreviousKey', 'PlatformSecret:DerivationKey']) {
    const secreto = obtener(nombre);
    if (presente(secreto) && valoresFront.some(valor => valor.includes(secreto))) {
      errores.push(`${nombre}: una clave exclusiva del servidor está embebida en el frontend.`);
    }
  }
  if (!presente(obtener('PlatformSecret:DerivationKey'))) {
    avisos.push('PlatformSecret:DerivationKey ausente: la firma por sesión requiere configurarla en el servidor; se conserva compatibilidad legacy.');
  }
  if (presente(frontend?.platformSecret?.secretUpBackend)) {
    avisos.push('platformSecret.secretUpBackend está expuesta en frontend sin consumidor; no se exige igualdad.');
  }

  if (frontend?.production !== true) errores.push('environment.prod.ts debe activar production.');
  let apiSegura = frontend?.apiUrl === '/api';
  try {
    const api = new URL(frontend?.apiUrl);
    apiSegura ||= api.protocol === 'https:' && !!api.hostname && !api.username && !api.password;
  } catch { /* La ruta relativa /api ya está contemplada. */ }
  if (!apiSegura) errores.push('apiUrl de producción debe ser /api o una URL HTTPS sin credenciales.');
  const build = angular?.projects?.frontend?.architect?.build;
  if (build?.defaultConfiguration !== 'production') errores.push('El build por defecto debe usar production.');
  for (const modo of ['production', 'docker']) {
    const reemplazos = build?.configurations?.[modo]?.fileReplacements;
    const entornos = Array.isArray(reemplazos)
      ? reemplazos.filter(r => r.replace === 'src/environments/environment.ts') : [];
    if (entornos.length !== 1 || entornos[0].with !== 'src/environments/environment.prod.ts') {
      errores.push(`Build ${modo}: debe reemplazar environment.ts por environment.prod.ts una sola vez.`);
    }
  }
  return { errores, avisos };
}

function verificarRepositorio(raiz = RAIZ) {
  const leer = relativa => fs.readFileSync(path.join(raiz, relativa), 'utf8');
  const produccion = 'backend/src/ZooSanMarino.API/appsettings.Production.json';
  const fuentes = [leerJson(leer('backend/src/ZooSanMarino.API/appsettings.json'))];
  if (fs.existsSync(path.join(raiz, produccion))) fuentes.push(leerJson(leer(produccion)));
  const resultado = validarContrato(
    leerEnvironment(leer('frontend/src/environments/environment.prod.ts')),
    combinarConfiguracion(...fuentes), leerJson(leer('frontend/angular.json')));
  resultado.avisos.push('JWT: se valida el formato local, no la clave efectiva. Production rechaza claves de desarrollo; el pipeline debe inyectar la clave mediante su paso de rotación o conservar su fuente de secretos.');
  return resultado;
}

function informar(resultado, escribir = console.log) {
  escribir(`[seguridad-produccion] ${ALCANCE}`);
  for (const aviso of resultado.avisos) escribir(`[AVISO] ${aviso}`);
  for (const error of resultado.errores) escribir(`[ERROR] ${error}`);
  escribir(resultado.errores.length ? '[ERROR] Contrato local incompatible.' : '[OK] Cuatro pares de claves compatibles, formato JWT local válido y builds configurados para producción.');
  return resultado.errores.length ? 1 : 0;
}

if (require.main === module) {
  try {
    process.exitCode = informar(verificarRepositorio());
  } catch {
    // No imprimir errores del parser, contenido, rutas configurables ni stacks con datos sensibles.
    console.error(`[seguridad-produccion] ${ALCANCE}`);
    console.error('[ERROR] No se pudo leer el contrato local. Revise los archivos y las dependencias de frontend instaladas.');
    process.exitCode = 1;
  }
}

module.exports = { leerEnvironment, leerJson, combinarConfiguracion, validarContrato, verificarRepositorio, informar };

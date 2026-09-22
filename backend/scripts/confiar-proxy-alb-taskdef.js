#!/usr/bin/env node
/**
 * Declara al ALB como proxy confiable (`ReverseProxy__KnownNetworks__*`) en el JSON de la task
 * definition que el deploy ya descargó.
 *
 * Por qué existe
 * --------------
 * El rate limit (`RateLimitingMiddleware`) cuenta por `RemoteIpAddress` después de
 * `UseForwardedHeaders()`. Detrás del ALB eso solo da la IP del cliente si el ALB es un proxy
 * conocido. Medido el 22-sep-2026 con el binario de producción:
 *
 *   - El Dockerfile fija `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: ASP.NET Core VACÍA las listas de
 *     proxies conocidos (confía en cualquiera) y agrega una segunda pasada de `UseForwardedHeaders`.
 *     Sin `ReverseProxy__*` en la TaskDef, cada pasada consume una entrada de `X-Forwarded-For` y el
 *     cliente elige su propia IP: un `X-Forwarded-For` inventado por request = contador nuevo = el
 *     límite (y el de login, que frena la fuerza bruta) se elude.
 *   - Sin esa variable y sin `ReverseProxy__*`, todos quedan con la IP del ALB: un solo contador para
 *     toda la empresa (429 masivos).
 *   - Con las redes privadas como conocidas, las dos cosas quedan bien: cuenta la IP que agregó el
 *     ALB, y la segunda pasada no hace nada porque el origen ya es la IP pública del cliente.
 *
 * Por qué RFC 1918 y no el CIDR exacto del VPC: nadie del equipo administra AWS, así que no hay forma
 * de leerlo. El ALB siempre llega al back desde una IP privada del VPC, y nadie en internet puede
 * llegar con una IP de origen privada. `ForwardLimit` queda en 1: el dominio apunta directo al ALB, sin
 * CloudFront delante (verificado por DNS).
 *
 * Si la TaskDef ya declara `ReverseProxy__KnownNetworks__*` o `ReverseProxy__KnownProxies__*`, no se
 * toca: alguien puso valores más precisos.
 *
 * Uso:  node backend/scripts/confiar-proxy-alb-taskdef.js <task-def.json> [contenedor=backend]
 */

const fs = require('fs');

/** Redes privadas RFC 1918. `HttpSecurityConfiguration` exige CIDR con prefijo > 0. */
const REDES_PRIVADAS = ['10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16'];
const PREFIJO_REDES = 'ReverseProxy__KnownNetworks__';
const PREFIJO_PROXIES = 'ReverseProxy__KnownProxies__';

const declaraProxies = (v) => v && typeof v.name === 'string'
  && (v.name.startsWith(PREFIJO_REDES) || v.name.startsWith(PREFIJO_PROXIES));

/** Devuelve la task definition con el ALB como proxy conocido (sin mutar la de entrada) y qué pasó. */
function confiarProxyEnTaskDef(taskDef, contenedor) {
  const definiciones = Array.isArray(taskDef && taskDef.containerDefinitions) ? taskDef.containerDefinitions : [];
  const indice = definiciones.findIndex((c) => c && c.name === contenedor);
  if (indice < 0) throw new Error(`La task definition no tiene el contenedor "${contenedor}".`);

  const actual = definiciones[indice];
  const environment = actual.environment || [];
  if (environment.some(declaraProxies) || (actual.secrets || []).some(declaraProxies)) {
    return { taskDef, estado: 'existente' };
  }

  const nuevoEnvironment = environment.concat(
    REDES_PRIVADAS.map((red, i) => ({ name: `${PREFIJO_REDES}${i}`, value: red })));
  const containerDefinitions = definiciones.map((c, i) => (i === indice ? { ...c, environment: nuevoEnvironment } : c));
  return { taskDef: { ...taskDef, containerDefinitions }, estado: 'agregada' };
}

function main(argv) {
  const [ruta, contenedor = 'backend'] = argv;
  if (!ruta) {
    console.error('Uso: node backend/scripts/confiar-proxy-alb-taskdef.js <task-def.json> [contenedor]');
    return 2;
  }

  const { taskDef, estado } = confiarProxyEnTaskDef(JSON.parse(fs.readFileSync(ruta, 'utf8')), contenedor);
  if (estado === 'existente') {
    console.log('La task definition ya declara ReverseProxy__KnownNetworks/KnownProxies: se respeta sin cambios.');
    return 0;
  }

  fs.writeFileSync(ruta, JSON.stringify(taskDef, null, 2));
  console.log(`ALB como proxy conocido: ${PREFIJO_REDES}0..${REDES_PRIVADAS.length - 1} = ${REDES_PRIVADAS.join(', ')}.`);
  return 0;
}

module.exports = { confiarProxyEnTaskDef, REDES_PRIVADAS };

if (require.main === module) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (e) {
    console.log(`::error::${e.message}`);
    process.exitCode = 1;
  }
}

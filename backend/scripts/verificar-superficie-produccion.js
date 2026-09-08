#!/usr/bin/env node
/**
 * Gate: la superficie que la API expone en producción no se ensancha por descuido.
 *
 * Por qué existe
 * --------------
 * La auditoría externa de septiembre-2026 levantó tres hallazgos contra
 * `https://zootecnico.sanmarino.com.co`. Dos de ellos dependen de invariantes del backend que hoy
 * se sostienen SOLOS —nadie los comprueba— y que un refactor distraído rompe sin ruido:
 *
 *   1. «/api/swagger.json responde 401 y no 404 ⇒ hay una API real detrás».
 *      La respuesta correcta es que en producción NO hay Swagger (todo el bloque vive dentro de
 *      `if (!app.Environment.IsProduction())` y el contenedor arranca con
 *      `ASPNETCORE_ENVIRONMENT=Production`), y que ese 401 es UNIFORME: lo emite
 *      `PlatformSecretMiddleware` antes del ruteo, así que responde igual para `/api/Users` que
 *      para `/api/loquesea` — no confirma ninguna ruta concreta.
 *      Eso deja de ser cierto si alguien: mueve un endpoint de Swagger/debug fuera del `if`,
 *      pierde el `ENV` del Dockerfile, o corre el filtro DESPUÉS del ruteo (ahí el 401/404 pasa a
 *      ser un oráculo que enumera rutas).
 *
 *   2. «Referencia a /api/DbStudio en el JS de la app».
 *      La ruta se renombró a `api/ConfigColores` y el controlador se ocultó del contrato Swagger.
 *      El gate del bundle está del lado del front (`frontend/scripts/verificar-senuelo-modulo.js`);
 *      éste cuida la mitad del backend.
 *
 * Ninguno de estos criterios ES el control de seguridad: la autenticación real es el JWT por
 * usuario + authz deny-by-default + alcance multiempresa fail-closed. Lo que se cuida acá es que no
 * volvamos a regalar superficie ni pistas de reconocimiento.
 *
 * Uso:  node scripts/verificar-superficie-produccion.js     (desde backend/)
 * Sale 1 si falla cualquier criterio. Detalle de cada uno en el plan:
 * fase_de_desarrollo/gates_hallazgos_auditoria_2026-09_plan.md
 */

const fs = require('fs');
const path = require('path');

const RAIZ = path.resolve(__dirname, '..');
const rel = (p) => path.relative(RAIZ, p).replace(/\\/g, '/');

const fallos = [];
const oks = [];

function ok(msg) { oks.push(msg); }
function falla(criterio, detalle) { fallos.push({ criterio, detalle }); }

function leer(rutaRelativa) {
  const abs = path.join(RAIZ, rutaRelativa);
  if (!fs.existsSync(abs)) return null;
  return fs.readFileSync(abs, 'utf8');
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 1 — Swagger y /debug/* solo fuera de Production
// ─────────────────────────────────────────────────────────────────────────────
// Se resuelve por BALANCEO DE LLAVES, no por regex de línea: el bloque tiene ~150 líneas con
// literales de CSS y de JS adentro, y cualquier heurística de "está cerca del if" miente.
function rangosNoProduccion(texto) {
  const rangos = [];
  const marcador = /if\s*\(\s*!\s*app\.Environment\.IsProduction\(\)\s*\)/g;
  let m;
  while ((m = marcador.exec(texto)) !== null) {
    const abre = texto.indexOf('{', m.index + m[0].length);
    if (abre === -1) continue;
    let profundidad = 0;
    for (let i = abre; i < texto.length; i++) {
      const c = texto[i];
      if (c === '{') profundidad++;
      else if (c === '}') {
        profundidad--;
        if (profundidad === 0) { rangos.push([abre, i]); break; }
      }
    }
  }
  return rangos;
}

function criterioSwagger() {
  const rutaProgram = 'src/ZooSanMarino.API/Program.cs';
  const texto = leer(rutaProgram);
  if (texto === null) { falla('1 · swagger/debug fuera de prod', `No se encontró ${rutaProgram}`); return; }

  const rangos = rangosNoProduccion(texto);
  if (rangos.length === 0) {
    falla('1 · swagger/debug fuera de prod',
      'No hay ningún bloque `if (!app.Environment.IsProduction())` en Program.cs. ' +
      'Si el guard cambió de forma, actualizá este gate a conciencia.');
    return;
  }

  // Lo que NUNCA puede registrarse en producción.
  const patrones = [
    { re: /app\.UseSwagger\s*\(/g,                            que: 'app.UseSwagger()' },
    { re: /app\.UseSwaggerUI\s*\(/g,                          que: 'app.UseSwaggerUI()' },
    { re: /app\.UseMiddleware<\s*SwaggerPasswordMiddleware/g, que: 'SwaggerPasswordMiddleware' },
    { re: /app\.Map[A-Za-z]*\(\s*"\/swagger/g,                que: 'endpoint /swagger…' },
    { re: /app\.Map[A-Za-z]*\(\s*"\/debug/g,                  que: 'endpoint /debug…' },
  ];

  const lineaDe = (idx) => texto.slice(0, idx).split('\n').length;
  let encontrados = 0;

  for (const { re, que } of patrones) {
    let m;
    while ((m = re.exec(texto)) !== null) {
      encontrados++;
      const dentro = rangos.some(([a, b]) => m.index > a && m.index < b);
      if (!dentro) {
        falla('1 · swagger/debug fuera de prod',
          `${rutaProgram}:${lineaDe(m.index)} — ${que} está FUERA de ` +
          '`if (!app.Environment.IsProduction())`. En producción quedaría expuesto.');
      }
    }
  }

  if (fallos.every(f => !f.criterio.startsWith('1 ·'))) {
    ok(`1 · ${encontrados} registro(s) de swagger/debug, todos dentro de ${rangos.length} bloque(s) !IsProduction()`);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 2 — el entorno del contenedor es Production
// ─────────────────────────────────────────────────────────────────────────────
// El `ENV` del Dockerfile es la garantía DURA: queda horneado en la imagen, así que aunque el task
// definition perdiera la variable, la app sigue arrancando en Production. Los task definitions del
// repo son la segunda capa (y documentación); el vigente en AWS no se puede leer desde acá.
function criterioEntorno() {
  const dockerfile = leer('Dockerfile');
  if (dockerfile === null) {
    falla('2 · ASPNETCORE_ENVIRONMENT', 'No se encontró backend/Dockerfile');
  } else if (!/^\s*ENV\s+ASPNETCORE_ENVIRONMENT\s*=\s*Production\s*$/m.test(dockerfile)) {
    falla('2 · ASPNETCORE_ENVIRONMENT',
      'backend/Dockerfile no fija `ENV ASPNETCORE_ENVIRONMENT=Production`. Sin eso, la imagen ' +
      'arranca en el entorno que le pasen (o ninguno) y el bloque de Swagger vuelve a registrarse.');
  } else {
    ok('2 · Dockerfile fija ASPNETCORE_ENVIRONMENT=Production (horneado en la imagen)');
  }

  const dirDeploy = path.join(RAIZ, 'deploy');
  if (fs.existsSync(dirDeploy)) {
    const taskdefs = fs.readdirSync(dirDeploy)
      .filter(f => /^ecs-taskdef.*\.json$/.test(f));
    for (const f of taskdefs) {
      const contenido = fs.readFileSync(path.join(dirDeploy, f), 'utf8');
      if (!/"ASPNETCORE_ENVIRONMENT"[\s\S]{0,80}"Production"/.test(contenido)) {
        falla('2 · ASPNETCORE_ENVIRONMENT',
          `deploy/${f} no declara ASPNETCORE_ENVIRONMENT=Production.`);
      }
    }
    if (taskdefs.length > 0 && fallos.every(f => !f.criterio.startsWith('2 ·'))) {
      ok(`2 · ${taskdefs.length} task definition(s) del repo declaran Production`);
    }
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 3 — el filtro de plataforma corre ANTES de autenticar y de rutear
// ─────────────────────────────────────────────────────────────────────────────
// Es lo que hace que el 401 sea uniforme para todo path. Si se corriera después del ruteo, una ruta
// inexistente daría 404 y una existente 401: eso es un oráculo que ENUMERA la API para cualquier
// escáner anónimo — justo el hallazgo #1 de la auditoría, pero de verdad.
function criterioOrdenPipeline() {
  const rutaProgram = 'src/ZooSanMarino.API/Program.cs';
  const texto = leer(rutaProgram);
  if (texto === null) return; // ya reportado en el criterio 1

  const pos = (patron) => {
    const m = new RegExp(patron).exec(texto);
    return m ? m.index : -1;
  };

  const filtro = pos('app\\.UsePlatformSecret\\s*\\(');
  const auth   = pos('app\\.UseAuthentication\\s*\\(');
  const mapc   = pos('app\\.MapControllers\\s*\\(');

  if (filtro === -1) {
    falla('3 · orden del pipeline', `${rutaProgram} ya no llama a app.UsePlatformSecret().`);
    return;
  }
  if (auth !== -1 && filtro > auth) {
    falla('3 · orden del pipeline',
      'app.UsePlatformSecret() quedó DESPUÉS de app.UseAuthentication().');
  }
  if (mapc !== -1 && filtro > mapc) {
    falla('3 · orden del pipeline',
      'app.UsePlatformSecret() quedó DESPUÉS de app.MapControllers(): el rechazo dejaría de ser ' +
      'uniforme y el par 401/404 pasaría a enumerar rutas.');
  }
  if (fallos.every(f => !f.criterio.startsWith('3 ·'))) {
    ok('3 · UsePlatformSecret() corre antes de UseAuthentication() y de MapControllers()');
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 4 — las exenciones del filtro de plataforma están congeladas
// ─────────────────────────────────────────────────────────────────────────────
// Cada path exento es superficie alcanzable sin firma de origen. Agregar uno es una decisión de
// seguridad, no un detalle de implementación: tiene que verse en el diff de este archivo.
const EXENCIONES_APROBADAS = new Set([
  '/swagger',                  // el propio Swagger, y solo fuera de Production
  '/swagger-ui',
  '/ping',
  '/health',
  '/auth/login',
  '/auth/register',
  '/auth/recover-password',
]);

function criterioExenciones() {
  const ruta = 'src/ZooSanMarino.API/Middleware/PlatformSecretMiddleware.cs';
  const texto = leer(ruta);
  if (texto === null) { falla('4 · exenciones del filtro', `No se encontró ${ruta}`); return; }

  // Solo el cuerpo de InvokeAsync: los comentarios de la clase mencionan rutas de ejemplo.
  const desde = texto.indexOf('InvokeAsync');
  const cuerpo = desde === -1 ? texto : texto.slice(desde);
  const sinComentarios = cuerpo
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .split('\n').filter(l => !l.trim().startsWith('//')).join('\n');

  const actuales = new Set();
  const re = /path\.(?:StartsWith|Contains|Equals)\s*\(\s*"([^"]+)"/g;
  let m;
  while ((m = re.exec(sinComentarios)) !== null) actuales.add(m[1]);

  const nuevas = [...actuales].filter(p => !EXENCIONES_APROBADAS.has(p));
  const ausentes = [...EXENCIONES_APROBADAS].filter(p => !actuales.has(p));

  if (nuevas.length > 0) {
    falla('4 · exenciones del filtro',
      `${ruta} exime paths no aprobados: ${nuevas.map(p => `"${p}"`).join(', ')}. ` +
      'Cada exención es superficie alcanzable SIN firma de origen (escáneres incluidos). ' +
      'Si la exención es correcta, agregala a EXENCIONES_APROBADAS en este gate para que quede revisada en el PR.');
  }
  if (ausentes.length > 0) {
    falla('4 · exenciones del filtro',
      `Desaparecieron exenciones esperadas: ${ausentes.map(p => `"${p}"`).join(', ')}. ` +
      'Quitar /auth/login o /health rompe el login y el health check de ECS. ' +
      'Si el cambio es intencional, actualizá EXENCIONES_APROBADAS.');
  }
  if (nuevas.length === 0 && ausentes.length === 0) {
    ok(`4 · ${actuales.size} exenciones del filtro de plataforma, todas aprobadas`);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 5 — la consola de BD sigue con ruta-señuelo, autenticada y fuera del contrato
// ─────────────────────────────────────────────────────────────────────────────
function criterioConsolaBd() {
  const ruta = 'src/ZooSanMarino.API/Controllers/DbStudioController.cs';
  const texto = leer(ruta);
  if (texto === null) { falla('5 · consola de BD', `No se encontró ${ruta}`); return; }

  // Encabezado = lo anterior a la declaración de la clase (atributos a nivel de clase).
  const decl = texto.search(/public\s+(?:sealed\s+)?class\s+DbStudioController/);
  const encabezado = decl === -1 ? texto : texto.slice(0, decl);

  if (!/\[Route\("api\/ConfigColores"\)\]/.test(encabezado)) {
    falla('5 · consola de BD',
      `${ruta} ya no expone la ruta-señuelo [Route("api/ConfigColores")]. ` +
      'Volver a `api/[controller]` republica /api/DbStudio, que es literalmente el hallazgo #2.');
  }
  if (!/\[Authorize\]/.test(encabezado)) {
    falla('5 · consola de BD', `${ruta} perdió el [Authorize] a nivel de clase.`);
  }
  if (!/\[ApiExplorerSettings\(IgnoreApi\s*=\s*true\)\]/.test(encabezado)) {
    falla('5 · consola de BD',
      `${ruta} dejó de ocultarse del contrato Swagger a nivel de clase.`);
  }

  const reveladas = (texto.match(/IgnoreApi\s*=\s*false/g) || []).length;
  if (reveladas > 1) {
    falla('5 · consola de BD',
      `${ruta} revela ${reveladas} endpoints en Swagger (IgnoreApi = false). La única capacidad ` +
      'pública es migration-summary; el resto no debe anunciarse aunque igual responda 403.');
  }

  if (fallos.every(f => !f.criterio.startsWith('5 ·'))) {
    ok('5 · consola de BD: ruta-señuelo + [Authorize] + oculta de Swagger (1 endpoint público)');
  }
}

// ─────────────────────────────────────────────────────────────────────────────
function main() {
  criterioSwagger();
  criterioEntorno();
  criterioOrdenPipeline();
  criterioExenciones();
  criterioConsolaBd();

  for (const o of oks) console.log(`  OK    ${o}`);

  if (fallos.length > 0) {
    console.error('');
    console.error('❌ La superficie de producción se ensanchó respecto de lo auditado en sep-2026:');
    console.error('');
    for (const f of fallos) {
      console.error(`   [${f.criterio}]`);
      console.error(`   ${f.detalle}`);
      console.error('');
    }
    console.error('   Contexto: fase_de_desarrollo/respuesta_auditoria_ciberseguridad_2026-09.md');
    console.error('   Gate:     fase_de_desarrollo/gates_hallazgos_auditoria_2026-09_plan.md');
    console.error('');
    process.exit(1);
  }

  console.log('');
  console.log('✅ Superficie de producción intacta (5/5 criterios).');
}

main();

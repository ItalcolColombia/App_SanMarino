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
// Criterio 6 — NINGÚN endpoint de la consola de BD sin guarda explícita
// ─────────────────────────────────────────────────────────────────────────────
// El `[Authorize]` de la clase solo exige "sesión válida": por sí solo dejaría entrar a CUALQUIER
// usuario autenticado. Lo que cierra la consola es la guarda que cada acción llama en su cuerpo, y
// eso no lo garantiza el compilador: un endpoint nuevo sin la línea compila, pasa los tests y queda
// abierto a todo usuario logueado. Medido el 8-sep-2026: 48 endpoints, 46 con doble validación
// (correo autorizado Y admin) y 2 con la de migration-summary. Este criterio congela ese 0 sin guarda.
//
// Las cuatro guardas equivalen a lo mismo (todas terminan en IsAdminAsync => TieneAccesoCompleto);
// tienen nombres distintos por historia, no por semántica.
const GUARDAS_ACCESO_COMPLETO = [
  'EnsureAdminAsync',
  'EnsureFullAccessAsync',
  'EnsureCanReadAsync',        // delega en EnsureFullAccessAsync
  'EnsureCanWriteDataAsync',   // idem
];

/** La única guarda que admite cualquier sesión autenticada. Solo para la superficie pública. */
const GUARDA_PUBLICA = 'EnsureMigrationSummaryAccessAsync';

/** Endpoints que legítimamente responden a cualquier sesión autenticada. */
const PUBLICOS_APROBADOS = new Set(['access-mode', 'migration-summary']);

function criterioGuardasPorEndpoint() {
  const ruta = 'src/ZooSanMarino.API/Controllers/DbStudioController.cs';
  const texto = leer(ruta);
  if (texto === null) return; // ya reportado en el criterio 5

  // Posición de cada atributo [HttpX("plantilla")] / [HttpX]; el cuerpo de un endpoint es lo que va
  // hasta el atributo siguiente.
  const marcas = [];
  let m;
  const conPlantilla = /\[Http(?:Get|Post|Put|Patch|Delete)\("([^"]*)"\)\]/g;
  while ((m = conPlantilla.exec(texto)) !== null) marcas.push({ pos: m.index, ruta: m[1] });
  const sinPlantilla = /\[Http(?:Get|Post|Put|Patch|Delete)\]/g;
  while ((m = sinPlantilla.exec(texto)) !== null) marcas.push({ pos: m.index, ruta: '(sin plantilla)' });
  marcas.sort((a, b) => a.pos - b.pos);

  if (marcas.length === 0) {
    falla('6 · guardas por endpoint', `No se detectó ningún endpoint en ${ruta}. ¿Cambió la forma del archivo?`);
    return;
  }

  const lineaDe = (idx) => texto.slice(0, idx).split('\n').length;
  let completos = 0, publicos = 0;

  marcas.forEach((marca, i) => {
    const fin = i + 1 < marcas.length ? marcas[i + 1].pos : texto.length;
    const cuerpo = texto.slice(marca.pos, fin);

    if (GUARDAS_ACCESO_COMPLETO.some(g => cuerpo.includes(g))) { completos++; return; }

    if (cuerpo.includes(GUARDA_PUBLICA)) {
      publicos++;
      if (!PUBLICOS_APROBADOS.has(marca.ruta)) {
        falla('6 · guardas por endpoint',
          `${ruta}:${lineaDe(marca.pos)} — "${marca.ruta}" quedó accesible a CUALQUIER sesión ` +
          'autenticada. La superficie pública aprobada es solo access-mode y migration-summary; ' +
          'si esto es deliberado, agregalo a PUBLICOS_APROBADOS para que se revise en el PR.');
      }
      return;
    }

    falla('6 · guardas por endpoint',
      `${ruta}:${lineaDe(marca.pos)} — "${marca.ruta}" NO llama a ninguna guarda. El [Authorize] de ` +
      'la clase solo exige sesión válida: sin la guarda, este endpoint de la consola de BD queda ' +
      'abierto a todo usuario logueado.');
  });

  if (fallos.every(f => !f.criterio.startsWith('6 ·'))) {
    ok(`6 · ${marcas.length} endpoints de la consola de BD: ${completos} con doble validación, ${publicos} públicos aprobados, 0 sin guarda`);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 7 — la firma de plataforma se deriva del MISMO `jti` que va en el token
// ─────────────────────────────────────────────────────────────────────────────
// El emisor (AuthService, en el login) y el verificador (PlatformSecretMiddleware, en cada request)
// tienen que estar de acuerdo en dos cosas: la FÓRMULA y el INSUMO. Si divergen, el front manda una
// firma que el backend nunca va a aceptar y **nadie puede usar la aplicación** — falla todo, no una
// pantalla.
//
// La invariante REAL —que DerivarClaveSesion(datos.JtiClaim, key) sea exactamente datos.PlatformKey,
// para cualquier jti— ya NO la vigila un regex sobre el texto de AuthService: la EJECUTA
// `SesionTokenCalculosTests` (xUnit, en Application.Tests), que es donde una invariante se puede
// probar de verdad en vez de suponerse por la forma del código. Antes ese regex tenía dos fallas
// opuestas: falso positivo (renombrar `jti` o extraer su generación a un helper lo rompía sin que
// nada estuviera roto) y falso negativo (miraba la forma, no el valor; un cambio que conservara la
// forma y alterara el insumo real pasaba igual).
//
// Lo que este criterio cuida ahora es más angosto y más estable: que nadie se salga del CARRIL.
// AuthService.GenerateResponseAsync tiene que obtener el claim jti y la firma de plataforma de una
// sola llamada a `SesionTokenCalculos.ConstruirDatosDeSesion` (un solo productor, ver Límite 1 del
// plan `cerrar_limites_verificacion_firma_plataforma_plan.md`), SesionTokenCalculos tiene que derivar
// con `PlatformSecretCalculos.DerivarClaveSesion` (no reimplementar el HMAC), y ni el emisor ni el
// verificador ni el cálculo intermedio se escriben su propio `HMACSHA256` — el "una sola fórmula por
// número" del CLAUDE.md aplicado acá.
//
// Es la parte del smoke (`backend/scripts/smoke-firma-plataforma.js`) que no se puede ejercitar sin
// un login con usuario real: el smoke prueba que el middleware valida bien lo que se le manda; esto
// congela que el login mande exactamente eso.
function criterioFirmaMismoJti() {
  const rutaAuth = 'src/ZooSanMarino.Infrastructure/Services/AuthService.cs';
  const rutaCalc = 'src/ZooSanMarino.Application/Calculos/SesionTokenCalculos.cs';
  const rutaMw = 'src/ZooSanMarino.API/Middleware/PlatformSecretMiddleware.cs';

  const auth = leer(rutaAuth);
  if (auth === null) { falla('7 · firma del mismo jti', `No se encontró ${rutaAuth}`); return; }

  // Si el login todavía no emite firma por sesión (ambiente previo a la Fase A), no hay nada que
  // congelar y el criterio no aplica: el cliente va por el camino legacy.
  if (!auth.includes('SesionTokenCalculos')) {
    ok('7 · el login no emite firma por sesión (camino legacy) — criterio no aplica');
    return;
  }

  // (1) Un solo productor: AuthService obtiene AMBOS valores de SesionTokenCalculos.ConstruirDatosDeSesion.
  const llamadas = (auth.match(/SesionTokenCalculos\.ConstruirDatosDeSesion\s*\(/g) || []).length;
  if (llamadas === 0) {
    falla('7 · firma del mismo jti',
      `${rutaAuth} ya no llama a SesionTokenCalculos.ConstruirDatosDeSesion(...). Tiene que ser el ` +
      'único productor del claim jti y de la firma de plataforma: derivarlos por separado reabre la ' +
      'posibilidad de que diverjan sin que nada avise.');
  } else if (llamadas > 1) {
    falla('7 · firma del mismo jti',
      `${rutaAuth} llama a ConstruirDatosDeSesion ${llamadas} veces. Tiene que ser una sola: dos ` +
      'llamadas pueden terminar recibiendo jti distintos y el front mandaría una firma que el ' +
      'backend nunca acepta — no entra nadie.');
  }

  // (2) SesionTokenCalculos deriva con la fórmula central, no con una propia.
  const calc = leer(rutaCalc);
  if (calc === null) {
    falla('7 · firma del mismo jti', `No se encontró ${rutaCalc}.`);
  } else if (!calc.includes('PlatformSecretCalculos.DerivarClaveSesion')) {
    falla('7 · firma del mismo jti',
      `${rutaCalc} ya no deriva la firma con PlatformSecretCalculos.DerivarClaveSesion.`);
  }

  // (3) Una sola fórmula por número: ni el emisor, ni el cálculo intermedio, ni el verificador se
  // escriben su propio HMAC.
  const mw = leer(rutaMw);
  for (const [ruta, texto] of [[rutaAuth, auth], [rutaCalc, calc], [rutaMw, mw]]) {
    if (texto && /new\s+HMACSHA(256|384|512)\s*\(/.test(texto)) {
      falla('7 · firma del mismo jti',
        `${ruta} construye su propio HMAC. La fórmula vive SOLO en PlatformSecretCalculos: ` +
        'duplicarla es cómo emisor y verificador terminan calculando cosas distintas.');
    }
  }

  // (4) El verificador sigue verificando con la misma función central.
  if (mw && !mw.includes('PlatformSecretCalculos.DerivarClaveSesion')) {
    falla('7 · firma del mismo jti',
      `${rutaMw} ya no verifica con PlatformSecretCalculos.DerivarClaveSesion.`);
  }

  if (fallos.every(f => !f.criterio.startsWith('7 ·'))) {
    ok('7 · un solo productor (SesionTokenCalculos) y una sola fórmula (PlatformSecretCalculos) — ' +
       'la invariante la prueba SesionTokenCalculosTests; esto sólo cuida que nadie se salga del carril');
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criterio 8 — producción y desarrollo no comparten secretos NUEVOS
// ─────────────────────────────────────────────────────────────────────────────
// Medido el 8-sep-2026: ocho claves sensibles tienen EL MISMO VALOR en `appsettings.json`
// (producción) y en `appsettings.Development.json`. No es cosmético: el task definition de ECS sólo
// sobrescribe la connection string, `JwtSettings__*` y las de entorno — todo lo demás corre en
// producción con el valor versionado en el repo, legible por cualquiera que lo clone.
//
// Rotarlas no se puede hacer de un plumazo: `Encryption:*` y `PlatformSecret:SecretUpFrontend`/
// `EncryptionKey` viven TAMBIÉN en el bundle del navegador y en la app móvil, así que cambiar sólo
// el backend deja la aplicación inutilizable para todos. Ese trabajo es coordinado y tiene su propio
// procedimiento (`node backend/scripts/generar-secretos-produccion.js`).
//
// Por eso este criterio NO exige cero: congela la lista de las que ya están así y corta si aparece
// UNA MÁS. La deuda se paga con el plan; lo que el gate impide es que siga creciendo por descuido.
const SECRETOS_COMPARTIDOS_CONOCIDOS = new Set([
  'JwtSettings:Key',                    // en prod la pisa JwtSettings__Key del task definition
  'PlatformSecret:SecretUpFrontend',    // también en el bundle del navegador y en la app móvil
  'PlatformSecret:SecretUpBackend',
  'PlatformSecret:EncryptionKey',       // idem
  'Encryption:RemitenteFrontend',       // idem — es la del cifrado del login
  'Encryption:RemitenteBackend',        // idem
  'Swagger:Password',                   // inocuo en prod: Swagger no se monta ahí
  'Email:Smtp:Password',                // credencial de O365; se rota en el tenant
]);

/** ¿El nombre de la clave sugiere que su valor es un secreto? */
function esClaveSensible(ruta) {
  const r = ruta.toLowerCase();
  return ['key', 'password', 'secret', 'pwd', 'token', 'remitente', 'connectionstring']
    .some((s) => r.includes(s));
}

function aplanarJson(obj, prefijo = '', salida = {}) {
  for (const [k, v] of Object.entries(obj ?? {})) {
    const ruta = prefijo ? `${prefijo}:${k}` : k;
    if (v && typeof v === 'object' && !Array.isArray(v)) aplanarJson(v, ruta, salida);
    else if (typeof v === 'string') salida[ruta] = v;
  }
  return salida;
}

function leerJson(rutaRelativa) {
  const texto = leer(rutaRelativa);
  if (texto === null) return null;
  try {
    return JSON.parse(texto.charCodeAt(0) === 0xfeff ? texto.slice(1) : texto);
  } catch {
    return null;
  }
}

function criterioSecretosDistintos() {
  const rutaProd = 'src/ZooSanMarino.API/appsettings.json';
  const rutaDev = 'src/ZooSanMarino.API/appsettings.Development.json';
  const prod = leerJson(rutaProd);
  const dev = leerJson(rutaDev);

  if (prod === null || dev === null) {
    falla('8 · secretos prod ≠ dev',
      `No se pudieron leer ambos appsettings (${rutaProd} / ${rutaDev}) como JSON.`);
    return;
  }

  const planoProd = aplanarJson(prod);
  const planoDev = aplanarJson(dev);

  const compartidos = Object.keys(planoProd).filter((k) =>
    esClaveSensible(k) &&
    planoProd[k].trim() !== '' &&
    planoDev[k] === planoProd[k]);

  const nuevos = compartidos.filter((k) => !SECRETOS_COMPARTIDOS_CONOCIDOS.has(k));
  const yaRotados = [...SECRETOS_COMPARTIDOS_CONOCIDOS].filter((k) => !compartidos.includes(k));

  if (nuevos.length > 0) {
    falla('8 · secretos prod ≠ dev',
      `Estas claves NUEVAS tienen el mismo valor en producción y en desarrollo: ` +
      `${nuevos.join(', ')}. El task definition de ECS no sobrescribe casi ninguna clave, así que ` +
      'un secreto versionado es el secreto REAL de producción. Generá uno distinto con ' +
      '`node backend/scripts/generar-secretos-produccion.js` y cargalo en el task definition; ' +
      'si la clave no es realmente sensible, renombrala o agregala a SECRETOS_COMPARTIDOS_CONOCIDOS ' +
      'explicando por qué.');
  }

  if (yaRotados.length > 0) {
    // No es un fallo: es deuda saldada. Se avisa para que la lista no quede mintiendo.
    ok(`8 · ya no comparten valor (sacar de la lista del gate): ${yaRotados.join(', ')}`);
  }

  if (nuevos.length === 0) {
    ok(`8 · sin secretos compartidos nuevos entre prod y dev ` +
       `(${compartidos.length} conocidos pendientes de rotar — ver generar-secretos-produccion.js)`);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
function main() {
  criterioSwagger();
  criterioEntorno();
  criterioOrdenPipeline();
  criterioExenciones();
  criterioConsolaBd();
  criterioGuardasPorEndpoint();
  criterioFirmaMismoJti();
  criterioSecretosDistintos();

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
  console.log('✅ Superficie de producción intacta (8/8 criterios).');
}

main();

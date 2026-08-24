/**
 * Endpoints operativos cuyas consultas se guardan para verse sin red.
 *
 * ## Es una LISTA BLANCA, y eso es la decisión
 *
 * Una lista negra deja entrar todo lo que nadie se acordó de excluir: el día que alguien agregue un
 * controller nuevo con datos sensibles, se cachearía solo. Con lista blanca, lo peor que pasa al
 * agregar un módulo es que no funcione sin red hasta que alguien lo agregue acá — un no-evento.
 *
 * Se comparan contra el **primer segmento** después de `/api/`, en minúsculas.
 */
const ENDPOINTS_OPERATIVOS: readonly string[] = [
  // Estructura: lo que hace falta para que cualquier pantalla pueda pintar sus selectores.
  'farm',
  'nucleo',
  'galpon',
  'lote',
  'lotes',
  'loteposturabase',
  'lotebaseengorde',
  'loteposturalevante',
  'loteposturaproduccion',
  'loteaveengorde',
  'lotereproductoraaveengorde',
  'company',
  'companypais',
  'pais',
  'userfarm',
  'regional',

  // Silos: en Santa Reyes el silo ES la ubicación del alimento, así que para esa empresa son
  // estructura igual que núcleo y galpón — sin ellos el inventario no puede pintar sus selectores.
  'silocatalogo',
  'farmsilo',
  'galponsilo',
  'lotesilo',

  // Huevo por lote (F7.3, Santa Reyes): el seguimiento diario de producción pinta sus filas FIJAS
  // a partir de esta lista — mismo rol que `lotesilo`, sin ella el diario no sabe qué tipos ofrecer.
  'lotehuevoitem',

  // Geografía: la usan los formularios de granja y de cliente.
  'countries',
  'cities',
  'departments',
  'departamento',
  'municipio',

  // Catálogos: chicos, casi estáticos, y sin ellos no se puede leer nada más.
  'catalogo-alimentos',
  'item-inventario-ecuador',
  'guia-genetica',
  'guia-genetica-ecuador',
  'produccionavicolaraw',
  'clientes',
  'lesiones',
  'masterlist',

  // Seguimiento diario: el corazón de la operación en granja.
  'seguimientolotelevante',
  'seguimientoavesengorde',
  'seguimientoavesengordeecuador',
  'seguimientodiariolotereproductora',
  'seguimientoproduccion',
  'loteseguimiento',
  'produccion',
  'producciondiaria',
  'dashboard',

  // Inventario y movimientos: saldos y trazabilidad que se consultan en el galpón.
  'inventario-gestion',
  'inventario',
  'inventario-gastos',
  'inventarioaves',
  'historialinventario',
  'movimientoaves',
  'movimientopolloengorde',
  'movimientopolloengordepanama',
  'traslados',
  'trasladonavigation',

  // Vacunación: cronograma y registros que el galponero consulta en el galpón.
  'vacunacioncronograma',
  'vacunacionregistro'
];

/**
 * Endpoints EXCLUIDOS de forma explícita aunque su prefijo coincidiera.
 *
 * Decisión D3 del plan madre: no cifrar el dato local, pero **minimizarlo** — nada de precios ni
 * facturación. Un reporte de costos guardado en una tablet que se pierde es un problema distinto
 * (y peor) que un seguimiento diario. `DbStudio` queda fuera porque devuelve lo que sea que se le
 * pida, o sea que no se puede razonar sobre su contenido; y todo lo de identidad
 * (`Auth`/`Users`/`Roles`/`session`) porque cachear una respuesta de autorización es cómo se
 * construye un bypass sin querer.
 */
const EXCLUIDOS: readonly string[] = [
  // Dinero: costos, liquidaciones y contabilidad.
  'reportediariocostosengorde',
  'reportediariocostospostura',
  'reportecontable',
  'liquidacioncierrelotelevante',
  'liquidaciontecnica',
  'liquidaciontecnicacomparacion',
  'liquidaciontecnicaecuador',

  // El push de la cola offline (F3). No es una consulta: es la escritura misma. Guardar su
  // respuesta no tendría sentido, y servirla desde caché haría creer que se sincronizó algo que
  // nunca salió del dispositivo.
  'sync',

  // Doble validación: es un gate de negocio, no un dato de operación. Cachear `configuracion`
  // congelaría en la tablet un flag que la empresa puede apagar, y `validar` exige red igual, así
  // que guardarlo no habilita nada. Sin caché el cliente ya cae a `SIN_PENDIENTES` (fail-closed).
  'seguimientovalidacion',

  // Plan de vacunación de la EMPRESA (plantillas). Lo que el galponero consulta en el galpón es el
  // cronograma de SU lote —ese sí se cachea—; la plantilla es la fuente aguas arriba, se edita desde
  // una oficina con red y alcanza a todos los lotes futuros. Servirla desde caché mostraría un plan
  // viejo como si fuera el vigente, que es justo el problema que el módulo vino a resolver.
  'vacunacionplantilla',

  // Bajar el plan de la empresa a los lotes (materializador). Es una acción de oficina y de
  // ESCRITURA: el preview tiene que ser fresco —uno servido de caché diría que va a crear filas
  // que ya existen— y aplicar exige red igual. Mismo criterio que la plantilla de la que sale.
  'vacunacionmaterializador',

  // Identidad y autorización.
  'auth',
  'users',
  'user',
  'roles',
  'permission',
  'session',

  // Devuelve lo que se le pida: no se puede razonar sobre su contenido.
  'dbstudio',

  // Reportes: no son operación de campo. Se consultan desde una oficina con red, son pesados, y
  // guardarlos en la tablet agranda el dato en reposo sin que nadie los mire en el galpón.
  'reportetecnico',
  'reportetecnicoproduccion',
  'reportetecnicosemanal',
  'reporteindicadorpanama',
  'indicadorecuador',
  'informesemanalpolloengorde',
  'vacunacionreportes',

  // Cuadre de alimento de engorde y su anomalía R2 (lotes liquidados con alimento sin trasladar).
  // Es un DETECTOR, y cachear un detector lo vuelve mentiroso: servir de caché un «0 galpones
  // descuadrados» taparía un descuadre vivo justo en el momento en que hay que verlo. Además es una
  // pantalla de oficina (tab «Cuadre alimento» de Gestión de Inventario) y su consulta sin empresa
  // devuelve todas a la vez. Cubre también `/liquidados-con-alimento`: comparten primer segmento.
  'cuadrealimentoengorde',

  // Herramientas internas (gestión del área de desarrollo, cargas masivas, integraciones). No las
  // usa un galponero, y varias devuelven datos de todas las empresas a la vez.
  'tickets',
  'ticket-perfiles',
  'italjira',
  'implementacion',
  'mapas',
  'migracion',
  'sincronizacion-panama',
  'excelimport'
];

/**
 * ¿Esta petición se puede guardar para consultarse sin red?
 *
 * Función PURA. Solo GET: cachear la respuesta de una mutación no tiene ningún significado útil y
 * sí uno peligroso (reponer al usuario el "guardado con éxito" de otra operación).
 */
export function decidirCacheable(metodo: string, url: string): boolean {
  if ((metodo ?? '').toUpperCase() !== 'GET') {
    return false;
  }

  const recurso = extraerRecurso(url);
  if (recurso === null) {
    return false;
  }

  if (EXCLUIDOS.includes(recurso)) {
    return false;
  }

  return ENDPOINTS_OPERATIVOS.includes(recurso);
}

/**
 * Devuelve el primer segmento después de `/api/`, en minúsculas, o `null` si la URL no es de la API.
 *
 * Tolera URL absolutas y relativas, y descarta query string y fragmento: la decisión de cachear
 * depende del recurso, nunca de sus parámetros.
 */
export function extraerRecurso(url: string): string | null {
  if (!url) {
    return null;
  }

  const sinQuery = url.split('?')[0].split('#')[0];
  const match = /\/api\/([^/]+)/i.exec(sinQuery);

  return match ? match[1].toLowerCase() : null;
}

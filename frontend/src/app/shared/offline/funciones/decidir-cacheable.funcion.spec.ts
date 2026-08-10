import { decidirCacheable, extraerRecurso } from './decidir-cacheable.funcion';

describe('decidirCacheable', () => {
  it('cachea GET de endpoints operativos', () => {
    expect(decidirCacheable('GET', '/api/Lote')).toBeTrue();
    expect(decidirCacheable('GET', '/api/inventario-gestion/stock?farmId=3')).toBeTrue();
    expect(decidirCacheable('GET', 'https://host/api/SeguimientoLoteLevante/por-lote/5')).toBeTrue();
  });

  it('🔴 NUNCA cachea nada que no sea GET', () => {
    // Guardar la respuesta de una mutación no tiene ningún significado útil, y sí uno peligroso:
    // reponerle al usuario el "guardado con éxito" de otra operación.
    for (const metodo of ['POST', 'PUT', 'PATCH', 'DELETE']) {
      expect(decidirCacheable(metodo, '/api/Lote')).withContext(metodo).toBeFalse();
    }
  });

  it('🔴 excluye reportes de costos y contables (D3: sin precios ni facturación)', () => {
    expect(decidirCacheable('GET', '/api/ReporteDiarioCostosEngorde/resumen')).toBeFalse();
    expect(decidirCacheable('GET', '/api/ReporteDiarioCostosPostura/resumen')).toBeFalse();
    expect(decidirCacheable('GET', '/api/ReporteContable/movimientos')).toBeFalse();
  });

  it('🔴 excluye identidad y autorización', () => {
    // Cachear una respuesta de autorización es cómo se construye un bypass sin querer.
    expect(decidirCacheable('GET', '/api/Auth/perfil')).toBeFalse();
    expect(decidirCacheable('GET', '/api/Users')).toBeFalse();
    expect(decidirCacheable('GET', '/api/Roles')).toBeFalse();
    expect(decidirCacheable('GET', '/api/session/heartbeat')).toBeFalse();
  });

  it('excluye DbStudio: devuelve lo que se le pida, no se puede razonar sobre su contenido', () => {
    expect(decidirCacheable('GET', '/api/DbStudio/query')).toBeFalse();
  });

  it('es LISTA BLANCA: un endpoint desconocido no se cachea', () => {
    // Lo peor que pasa al agregar un módulo nuevo es que no ande sin red hasta que se agregue
    // a la lista. Con lista negra, en cambio, se cachearía solo.
    expect(decidirCacheable('GET', '/api/UnControllerNuevo')).toBeFalse();
  });

  it('ignora una URL que no sea de la API', () => {
    expect(decidirCacheable('GET', '/assets/brand/logo.png')).toBeFalse();
    expect(decidirCacheable('GET', '/version.json')).toBeFalse();
    expect(decidirCacheable('GET', '')).toBeFalse();
  });
});

describe('extraerRecurso', () => {
  it('toma el primer segmento después de /api/, en minúsculas', () => {
    expect(extraerRecurso('/api/Lote/5')).toBe('lote');
    expect(extraerRecurso('https://host/api/Company')).toBe('company');
  });

  it('descarta query y fragmento: la decisión depende del recurso, no de sus parámetros', () => {
    expect(extraerRecurso('/api/Lote?x=1&y=2')).toBe('lote');
    expect(extraerRecurso('/api/Lote#seccion')).toBe('lote');
  });

  it('devuelve null si no hay /api/', () => {
    expect(extraerRecurso('/home')).toBeNull();
    expect(extraerRecurso('')).toBeNull();
  });
});

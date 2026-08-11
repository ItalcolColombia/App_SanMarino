import { decidirPedirPersistencia, EstadoPersistencia } from './decidir-pedir-persistencia.funcion';

describe('decidirPedirPersistencia', () => {
  const base: EstadoPersistencia = {
    apiDisponible: true,
    yaConcedida: false,
    yaPedidaEnEstaSesion: false,
    haySesion: true
  };

  it('con sesión y sin conceder todavía: se pide', () => {
    expect(decidirPedirPersistencia(base)).toBe(true);
  });

  it('si ya está concedida no se vuelve a pedir', () => {
    expect(decidirPedirPersistencia({ ...base, yaConcedida: true })).toBe(false);
  });

  it('si ya se pidió en esta sesión no se repite', () => {
    // Pedir permiso dos veces es la forma más rápida de que lo nieguen.
    expect(decidirPedirPersistencia({ ...base, yaPedidaEnEstaSesion: true })).toBe(false);
  });

  it('sin la Storage API no se pide (navegador viejo o contexto sin permiso)', () => {
    expect(decidirPedirPersistencia({ ...base, apiDisponible: false })).toBe(false);
  });

  it('sin sesión no se pide: antes del login es cuando más lo deniegan', () => {
    expect(decidirPedirPersistencia({ ...base, haySesion: false })).toBe(false);
  });

  it('si no se pudo consultar el estado, se pide igual', () => {
    // El peor caso es una llamada de más; el riesgo contrario es quedarse sin la concesión
    // porque `persisted()` falló una vez.
    expect(decidirPedirPersistencia({ ...base, yaConcedida: null })).toBe(true);
  });

  it('la API ausente manda sobre todo lo demás', () => {
    expect(
      decidirPedirPersistencia({
        apiDisponible: false,
        yaConcedida: null,
        yaPedidaEnEstaSesion: false,
        haySesion: true
      })
    ).toBe(false);
  });
});

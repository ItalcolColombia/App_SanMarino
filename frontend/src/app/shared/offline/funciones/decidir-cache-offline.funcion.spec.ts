import { decidirCacheOffline } from './decidir-cache-offline.funcion';
import type { AuthSession } from '../../../core/auth/auth.models';

/**
 * Contrato de la regla dura de D6. Lo que fija: ninguna cuenta con alcance global o multiempresa
 * acumula datos en el dispositivo, y el operario de una sola empresa sigue funcionando igual.
 */
describe('decidirCacheOffline', () => {
  /** Sesión mínima de un operario normal: una empresa, sin privilegios especiales. */
  function sesionOperario(extra: Partial<AuthSession> = {}): AuthSession {
    return {
      accessToken: 'token',
      user: { id: 'guid-1', userId: 10, isSuperAdmin: false, hasMultipleCompanies: false },
      companies: ['Agroavicola Sanmarino'],
      companyIds: [1],
      activeCompanyId: 1,
      activePaisId: 1,
      menu: [],
      menusByRole: [],
      ...extra
    } as AuthSession;
  }

  it('el operario de una sola empresa SÍ puede cachear (el caso que debe seguir andando)', () => {
    expect(decidirCacheOffline(sesionOperario())).toBe(true);
  });

  it('sin sesión no cachea (fail-closed)', () => {
    expect(decidirCacheOffline(null)).toBe(false);
    expect(decidirCacheOffline(undefined)).toBe(false);
  });

  it('un super admin NO cachea: su alcance es global', () => {
    const sesion = sesionOperario({ user: { id: 'g', userId: 1, isSuperAdmin: true } });
    expect(decidirCacheOffline(sesion)).toBe(false);
  });

  it('hasMultipleCompanies bloquea aunque el resto diga una sola', () => {
    const sesion = sesionOperario({
      user: { id: 'g', userId: 1, isSuperAdmin: false, hasMultipleCompanies: true }
    });
    expect(decidirCacheOffline(sesion)).toBe(false);
  });

  it('dos companyIds bloquean', () => {
    expect(decidirCacheOffline(sesionOperario({ companyIds: [1, 4] }))).toBe(false);
  });

  it('dos nombres de empresa bloquean', () => {
    const sesion = sesionOperario({ companies: ['Sanmarino', 'Demo'] });
    expect(decidirCacheOffline(sesion)).toBe(false);
  });

  it('basta UNA señal para bloquear, aunque las otras digan que hay una sola', () => {
    // Las tres señales las llena el backend por caminos distintos y pueden desfasarse. Ante la
    // duda "¿bajo datos de más?", la respuesta segura es no bajarlos.
    const sesion = sesionOperario({
      user: { id: 'g', userId: 1, isSuperAdmin: false, hasMultipleCompanies: false },
      companies: ['Sanmarino'],
      companyIds: [1, 4]
    });
    expect(decidirCacheOffline(sesion)).toBe(false);
  });

  it('un id repetido o con huecos describe UNA empresa y no bloquea al operario', () => {
    expect(decidirCacheOffline(sesionOperario({ companyIds: [1, 1] }))).toBe(true);
    expect(decidirCacheOffline(sesionOperario({ companyIds: [1, null as never] }))).toBe(true);
    expect(decidirCacheOffline(sesionOperario({ companies: ['Sanmarino', ''] }))).toBe(true);
  });

  it('el 0 no cuenta como empresa (es el hueco de un campo sin llenar)', () => {
    expect(decidirCacheOffline(sesionOperario({ companyIds: [1, 0] }))).toBe(true);
  });

  it('sin arreglos de empresa no se bloquea por eso solo', () => {
    // Un backend que no manda los arreglos no debe dejar sin consulta offline al operario: la
    // ausencia de dato no es evidencia de multiempresa.
    const sesion = sesionOperario({ companies: undefined as never, companyIds: undefined });
    expect(decidirCacheOffline(sesion)).toBe(true);
  });
});

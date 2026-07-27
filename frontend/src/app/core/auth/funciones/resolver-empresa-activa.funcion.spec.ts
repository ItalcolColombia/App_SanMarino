import { CompanyPais } from '../auth.models';
import { resolverEmpresaActiva } from './resolver-empresa-activa.funcion';

const SANMARINO: CompanyPais = {
  companyId: 1,
  companyName: 'Agroavicola Sanmarino',
  companyLogoDataUrl: 'data:image/png;base64,AAA',
  paisId: 10,
  paisNombre: 'Colombia',
  isDefault: true
};

const PANAMA: CompanyPais = {
  companyId: 5,
  companyName: 'ItalcolPanama',
  companyLogoDataUrl: null,
  paisId: 20,
  paisNombre: 'Panama',
  isDefault: false
};

describe('resolverEmpresaActiva', () => {
  it('mueve id, país y logo junto con el nombre', () => {
    const r = resolverEmpresaActiva([SANMARINO, PANAMA], 'ItalcolPanama');

    expect(r).toEqual({
      activeCompany: 'ItalcolPanama',
      activeCompanyId: 5,
      activePaisId: 20,
      activePaisNombre: 'Panama',
      activeCompanyLogoDataUrl: null
    });
  });

  it('el id NUNCA queda apuntando a la empresa anterior', () => {
    // El bug original: sólo cambiaba el nombre y el backend, que prefiere el id,
    // seguía respondiendo por la empresa del login.
    const r = resolverEmpresaActiva([SANMARINO, PANAMA], 'ItalcolPanama');
    expect(r!.activeCompanyId).not.toBe(SANMARINO.companyId);
    expect(r!.activePaisId).not.toBe(SANMARINO.paisId);
  });

  describe('fail-closed', () => {
    it('devuelve null si la empresa no está disponible', () => {
      expect(resolverEmpresaActiva([SANMARINO], 'Empresa Que No Existe')).toBeNull();
    });

    it('devuelve null si no hay companyPaises', () => {
      expect(resolverEmpresaActiva(undefined, 'ItalcolPanama')).toBeNull();
      expect(resolverEmpresaActiva([], 'ItalcolPanama')).toBeNull();
    });

    it('devuelve null con nombre vacío', () => {
      expect(resolverEmpresaActiva([SANMARINO], '')).toBeNull();
      expect(resolverEmpresaActiva([SANMARINO], '   ')).toBeNull();
    });

    it('descarta entradas sin id o sin nombre en vez de resolverlas a medias', () => {
      const rotas = [
        { companyName: 'Sin Id', paisId: 1 } as unknown as CompanyPais,
        { companyId: 9, paisId: 1 } as unknown as CompanyPais
      ];
      expect(resolverEmpresaActiva(rotas, 'Sin Id')).toBeNull();
    });

    it('un companyId 0 se considera inválido', () => {
      const cero = [{ ...SANMARINO, companyId: 0 }];
      expect(resolverEmpresaActiva(cero, 'Agroavicola Sanmarino')).toBeNull();
    });
  });

  describe('tolerancia del contrato', () => {
    it('acepta PascalCase (el login guarda la respuesta del backend tal cual viene)', () => {
      const pascal = [{
        CompanyId: 5,
        CompanyName: 'ItalcolPanama',
        PaisId: 20,
        PaisNombre: 'Panama'
      }] as unknown as CompanyPais[];

      const r = resolverEmpresaActiva(pascal, 'ItalcolPanama');
      expect(r?.activeCompanyId).toBe(5);
      expect(r?.activePaisId).toBe(20);
      expect(r?.activePaisNombre).toBe('Panama');
    });

    it('la coincidencia exacta gana sobre la insensible a mayúsculas', () => {
      const dos = [
        { ...PANAMA, companyId: 5, companyName: 'ItalcolPanama' },
        { ...PANAMA, companyId: 6, companyName: 'italcolpanama' }
      ];
      expect(resolverEmpresaActiva(dos, 'ItalcolPanama')?.activeCompanyId).toBe(5);
      expect(resolverEmpresaActiva(dos, 'italcolpanama')?.activeCompanyId).toBe(6);
    });

    it('resuelve ignorando mayúsculas cuando no hay coincidencia exacta', () => {
      expect(resolverEmpresaActiva([PANAMA], 'ITALCOLPANAMA')?.activeCompanyId).toBe(5);
    });
  });
});

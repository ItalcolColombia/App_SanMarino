import { resolverFirmaPlataforma } from './resolver-firma-plataforma.funcion';

const ESTATICO = 'Fr0nt#SeCr3t!SanM@r1n0X2';

describe('resolverFirmaPlataforma', () => {
  it('usa la firma derivada cuando la sesión trae platformKey', () => {
    const r = resolverFirmaPlataforma({ platformKey: 'abc123==' }, ESTATICO);
    expect(r).toEqual({ modo: 'derivada', valor: 'abc123==' });
  });

  it('cae al secreto estático cuando no hay sesión', () => {
    expect(resolverFirmaPlataforma(null, ESTATICO)).toEqual({ modo: 'legacy', valor: ESTATICO });
    expect(resolverFirmaPlataforma(undefined, ESTATICO)).toEqual({ modo: 'legacy', valor: ESTATICO });
  });

  it('cae al secreto estático cuando la sesión no tiene platformKey', () => {
    expect(resolverFirmaPlataforma({}, ESTATICO)).toEqual({ modo: 'legacy', valor: ESTATICO });
    expect(resolverFirmaPlataforma({ platformKey: undefined }, ESTATICO))
      .toEqual({ modo: 'legacy', valor: ESTATICO });
    expect(resolverFirmaPlataforma({ platformKey: null }, ESTATICO))
      .toEqual({ modo: 'legacy', valor: ESTATICO });
  });

  it('un platformKey vacío o de puros espacios NO cuenta como derivada', () => {
    expect(resolverFirmaPlataforma({ platformKey: '' }, ESTATICO).modo).toBe('legacy');
    expect(resolverFirmaPlataforma({ platformKey: '   ' }, ESTATICO).modo).toBe('legacy');
  });

  it('legacy sin secreto estático disponible ⇒ valor null (el backend lo rechazará)', () => {
    expect(resolverFirmaPlataforma(null, undefined)).toEqual({ modo: 'legacy', valor: null });
    expect(resolverFirmaPlataforma(null, null)).toEqual({ modo: 'legacy', valor: null });
  });
});

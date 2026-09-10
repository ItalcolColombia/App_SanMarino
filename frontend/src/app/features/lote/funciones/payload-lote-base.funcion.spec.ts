// src/app/features/lote/funciones/payload-lote-base.funcion.spec.ts
import { payloadLoteBaseConCamposPreservados } from './payload-lote-base.funcion';
import { LotePosturaBaseDto } from '../services/lote-postura-base.service';

function base(partial: Partial<LotePosturaBaseDto>): LotePosturaBaseDto {
  return {
    lotePosturaBaseId: 1,
    loteNombre: 'BASE-1',
    codigoErp: null,
    descripcionErp: null,
    cantidadHembras: 0,
    cantidadMachos: 0,
    cantidadMixtas: 0,
    raza: null,
    tipoLinea: null,
    fechaEncaset: null,
    companyId: 1,
    companyNombre: null,
    createdByUserId: 1,
    paisId: null,
    paisNombre: null,
    farmId: null,
    farmNombre: null,
    erpCreate: null,
    createdAt: '2026-01-01',
    totalLotes: 0,
    tieneLoteAbierto: true,
    ...partial
  };
}

describe('payloadLoteBaseConCamposPreservados', () => {
  describe('regla anti-borrado: raza / tipoLinea / cantidadMixtas salen del registro, no del form', () => {
    it('editando, devuelve los 3 valores guardados aunque el form ya no los traiga', () => {
      // El form perdió estos controles (salieron del template). Si el payload mandara null/0, la
      // primera edición de cualquiera de las 14 bases con raza cargada pisaría ese histórico.
      const payload = payloadLoteBaseConCamposPreservados(
        { loteNombre: 'BASE-1', cantidadHembras: 100, cantidadMachos: 0 },
        base({ raza: 'BABCOK BROWN', tipoLinea: 'ROJA', cantidadMixtas: 5 })
      );

      expect(payload.raza).toBe('BABCOK BROWN');
      expect(payload.tipoLinea).toBe('ROJA');
      expect(payload.cantidadMixtas).toBe(5);
    });

    it('editando una base sin raza/tipoLinea/mixtas, no inventa nada (null / 0)', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        { loteNombre: 'BASE-1' },
        base({ raza: null, tipoLinea: null, cantidadMixtas: 0 })
      );

      expect(payload.raza).toBeNull();
      expect(payload.tipoLinea).toBeNull();
      expect(payload.cantidadMixtas).toBe(0);
    });

    it('raza y tipoLinea se preservan de forma independiente', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        { loteNombre: 'BASE-1' },
        base({ raza: 'LOHMANN LSL', tipoLinea: null })
      );

      expect(payload.raza).toBe('LOHMANN LSL');
      expect(payload.tipoLinea).toBeNull();
    });
  });

  describe('alta (sin baseEnEdicion)', () => {
    it('los 3 campos preservados salen en su neutro null/0, nunca undefined', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        { loteNombre: 'NUEVA', cantidadHembras: 50, cantidadMachos: 10 },
        null
      );

      expect(payload.raza).toBeNull();
      expect(payload.tipoLinea).toBeNull();
      expect(payload.cantidadMixtas).toBe(0);
      // La clave existe y el backend recibe null, no ausencia.
      expect('raza' in payload).toBe(true);
      expect('tipoLinea' in payload).toBe(true);
      expect(payload.raza).not.toBeUndefined();
      expect(payload.tipoLinea).not.toBeUndefined();
    });

    it('tolera baseEnEdicion undefined igual que null', () => {
      const payload = payloadLoteBaseConCamposPreservados({ loteNombre: 'NUEVA' }, undefined);

      expect(payload.raza).toBeNull();
      expect(payload.tipoLinea).toBeNull();
      expect(payload.cantidadMixtas).toBe(0);
    });
  });

  describe('los campos que siguen en pantalla se toman del formulario, no del registro', () => {
    it('cantidadHembras editada de 100 a 250 devuelve 250', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        { loteNombre: 'BASE-1', cantidadHembras: 250 },
        base({ cantidadHembras: 100 })
      );

      expect(payload.cantidadHembras).toBe(250);
    });

    it('nombre, ERP, fecha de encaset, machos y granja vienen del form (no del registro previo)', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        {
          loteNombre: 'BASE-1-EDIT',
          codigoErp: 'CC-9',
          descripcionErp: 'Centro nuevo',
          fechaEncaset: '2026-03-15',
          cantidadMachos: 12,
          farmId: 7,
          erpCreate: '2026-01-02'
        },
        base({
          loteNombre: 'BASE-1',
          codigoErp: 'VIEJO',
          descripcionErp: 'Centro viejo',
          fechaEncaset: '2020-01-01',
          cantidadMachos: 0,
          farmId: 99,
          erpCreate: null
        })
      );

      expect(payload.loteNombre).toBe('BASE-1-EDIT');
      expect(payload.codigoErp).toBe('CC-9');
      expect(payload.descripcionErp).toBe('Centro nuevo');
      expect(payload.fechaEncaset).toBe('2026-03-15');
      expect(payload.cantidadMachos).toBe(12);
      expect(payload.farmId).toBe(7);
      expect(payload.erpCreate).toBe('2026-01-02');
    });
  });

  describe('normalización idéntica al código inline previo de saveBase()', () => {
    it('recorta strings y colapsa el vacío a null; loteNombre solo se recorta', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        { loteNombre: '  X  ', codigoErp: '  ', descripcionErp: '   texto  ' },
        null
      );

      expect(payload.loteNombre).toBe('X');
      expect(payload.codigoErp).toBeNull();
      expect(payload.descripcionErp).toBe('texto');
    });

    it('cantidades nulas/no numéricas caen a 0; fechas e ids vacíos a null', () => {
      const payload = payloadLoteBaseConCamposPreservados(
        {
          loteNombre: 'BASE-1',
          cantidadHembras: null,
          cantidadMachos: undefined,
          farmId: null,
          fechaEncaset: null,
          erpCreate: ''
        },
        null
      );

      expect(payload.cantidadHembras).toBe(0);
      expect(payload.cantidadMachos).toBe(0);
      expect(payload.farmId).toBeNull();
      expect(payload.fechaEncaset).toBeNull();
      expect(payload.erpCreate).toBeNull();
    });

    it('tolera valoresDelForm null/undefined sin romper y sigue preservando el registro', () => {
      const payload = payloadLoteBaseConCamposPreservados(null, base({ raza: 'ROSS', cantidadMixtas: 2 }));

      expect(payload.loteNombre).toBe('');
      expect(payload.cantidadHembras).toBe(0);
      expect(payload.raza).toBe('ROSS');
      expect(payload.cantidadMixtas).toBe(2);
    });
  });
});

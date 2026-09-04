import {
  DESPLAZAMIENTO_MAX_NUMERACION,
  desplazamientoNumeracion,
  desplazamientoPrimerDia,
  diaDeNegocioDesdeEdad,
  esDiaDePesajeObligatorio,
  menorEdadRegistrada,
  semanaDeNegocio
} from './dia-negocio-engorde.funcion';

/**
 * Reporte 04-sep-2026 (granja DONA MARIA, lote reproductora LR-0023649715 «156», encaset 30/08):
 * sus registros del 31/08, 01/09 y 02/09 salian como «Dia 2, 3 y 4». El corrimiento de un dia solo
 * existia si el lote traia hora de encasetamiento >= 13:00, y la hora NO la tiene nadie (0 de 142
 * lotes reproductora). Desde este cambio el corrimiento lo manda el DATO: la menor edad con
 * registro, con tope de 1 dia.
 */
describe('dia-negocio-engorde — desplazamiento de la numeracion', () => {
  describe('menorEdadRegistrada', () => {
    it('lista vacia ⇒ null (el lote todavia no tiene registros)', () => {
      expect(menorEdadRegistrada([])).toBeNull();
    });

    it('toma el minimo aunque las edades vengan desordenadas', () => {
      expect(menorEdadRegistrada([5, 1, 3, 2])).toBe(1);
    });

    it('ignora null, undefined y NaN, y no los confunde con el minimo', () => {
      expect(menorEdadRegistrada([null, undefined, NaN, 4, 2])).toBe(2);
      expect(menorEdadRegistrada([null, undefined, NaN])).toBeNull();
    });
  });

  describe('desplazamientoNumeracion — sin registros manda la hora (comportamiento previo)', () => {
    it('sin hora ⇒ 0: el dia del encaset es el dia 1', () => {
      expect(desplazamientoNumeracion(null, null)).toBe(0);
      expect(desplazamientoNumeracion(undefined, '')).toBe(0);
    });

    it('hora temprana ⇒ 0; 13:00 en punto ya es tardia ⇒ 1', () => {
      expect(desplazamientoNumeracion(null, '12:59')).toBe(0);
      expect(desplazamientoNumeracion(null, '13:00')).toBe(1);
      expect(desplazamientoNumeracion(null, '21:33')).toBe(1);
    });

    it('coincide con desplazamientoPrimerDia, que sigue rigiendo la fecha sugerida y el guarda', () => {
      for (const hora of [null, '', '00:00', '12:59', '13:00', '23:58']) {
        expect(desplazamientoNumeracion(null, hora)).toBe(desplazamientoPrimerDia(hora));
      }
    });
  });

  describe('desplazamientoNumeracion — con registros manda el dato', () => {
    it('CASO REPORTADO: sin hora y primer registro en la edad 1 ⇒ 1 (ese registro es el dia 1)', () => {
      const desplazamiento = desplazamientoNumeracion(1, null);
      expect(desplazamiento).toBe(1);
      expect(diaDeNegocioDesdeEdad(1, desplazamiento)).toBe(1);
      expect(diaDeNegocioDesdeEdad(2, desplazamiento)).toBe(2);
    });

    it('registro en la edad 0 ⇒ 0 aunque la hora sea tardia (historicos 131/132 conservan 1..7)', () => {
      const desplazamiento = desplazamientoNumeracion(0, '21:33');
      expect(desplazamiento).toBe(0);
      expect(diaDeNegocioDesdeEdad(0, desplazamiento)).toBe(1);
      expect(diaDeNegocioDesdeEdad(6, desplazamiento)).toBe(7);
    });

    it('lote tardio que arranco en la edad 1 ⇒ 1 (igual que antes: hora y dato coinciden)', () => {
      expect(desplazamientoNumeracion(1, '21:33')).toBe(1);
    });

    it('tope de 1 dia: arrancar en la edad 3 NO esconde el hueco (primer registro = dia 3)', () => {
      const desplazamiento = desplazamientoNumeracion(3, null);
      expect(desplazamiento).toBe(DESPLAZAMIENTO_MAX_NUMERACION);
      expect(diaDeNegocioDesdeEdad(3, desplazamiento)).toBe(3);
    });

    it('edad minima negativa (dato sucio) ⇒ 0, nunca un dia menor a 1 en pantalla', () => {
      const desplazamiento = desplazamientoNumeracion(-2, null);
      expect(desplazamiento).toBe(0);
      expect(diaDeNegocioDesdeEdad(-2, desplazamiento)).toBe(-1); // la fila cruda queda fuera de rango
      expect(diaDeNegocioDesdeEdad(0, desplazamiento)).toBe(1);
    });

    it('NaN ⇒ cae a la hora, no rompe la numeracion', () => {
      expect(desplazamientoNumeracion(NaN, '21:33')).toBe(1);
      expect(desplazamientoNumeracion(NaN, null)).toBe(0);
    });
  });

  describe('la semana acompana al dia mostrado', () => {
    it('lote que arranca en la edad 1: los dias 1..7 (edades 1..7) son la semana 1', () => {
      const d = desplazamientoNumeracion(1, null);
      expect(semanaDeNegocio(diaDeNegocioDesdeEdad(1, d))).toBe(1);
      expect(semanaDeNegocio(diaDeNegocioDesdeEdad(7, d))).toBe(1);
      expect(semanaDeNegocio(diaDeNegocioDesdeEdad(8, d))).toBe(2);
    });

    it('lote que arranca en la edad 0: identico a la semana de fn_seguimiento_diario_engorde', () => {
      const d = desplazamientoNumeracion(0, null);
      expect(semanaDeNegocio(diaDeNegocioDesdeEdad(0, d))).toBe(1);
      expect(semanaDeNegocio(diaDeNegocioDesdeEdad(6, d))).toBe(1);
      expect(semanaDeNegocio(diaDeNegocioDesdeEdad(7, d))).toBe(2);
    });
  });

  describe('la regla de pesaje NO se toca (se evalua sobre el numero que recibe)', () => {
    it('dias 1..7 y multiplos de 7 siguen siendo dia de pesaje', () => {
      expect([1, 2, 3, 4, 5, 6, 7, 14, 21].every(esDiaDePesajeObligatorio)).toBe(true);
      expect([0, 8, 9, 13, 15].some(esDiaDePesajeObligatorio)).toBe(false);
    });
  });
});

import {
  ETAPA_CICLO_ALISTAMIENTO,
  ETAPA_CICLO_FUERA_DE_CICLO,
  ETAPA_CICLO_LEVANTE,
  ETAPA_CICLO_LEVANTE_EN_PRODUCCION,
  ETAPA_CICLO_POSTURA,
  calcularEtapaCicloPostura,
  esGrupoBlancaAzur,
  etiquetaEtapaCicloPostura,
  obtenerEtapaCicloPostura
} from './semanas-ciclo-postura.funcion';

/**
 * V52/F3 — etapas del ciclo por raza (Santa Reyes, flag `semanasCicloPosturaPorRaza`).
 *
 * Este archivo es el ESPEJO de `SemanasCicloPosturaCalculosTests.cs`: los mismos cortes y los
 * mismos nulls, caso por caso. La regla del repo es «una sola fórmula por número» — acá la fórmula
 * vive dos veces por necesidad (backend decide, front pinta), así que lo que impide que diverjan es
 * que los dos lados tengan el mismo test. Si alguien mueve un umbral en un solo lado, cae acá.
 */
describe('semanas-ciclo-postura (espejo de SemanasCicloPosturaCalculos)', () => {
  describe('esGrupoBlancaAzur', () => {
    it('reconoce las 5 lineas sembradas en F2.1', () => {
      // Blancas/Azur: 84 semanas de postura.
      expect(esGrupoBlancaAzur('LOHMANN LSL')).toBe(true);
      expect(esGrupoBlancaAzur('Azur')).toBe(true);

      // Rojas/Criollas: 74 semanas.
      expect(esGrupoBlancaAzur('BABCOCK BROWN')).toBe(false);
      expect(esGrupoBlancaAzur('HY LINE BROWN')).toBe(false);
      expect(esGrupoBlancaAzur('Criolla')).toBe(false);
    });

    it('tolera capitalizacion, espacios sobrantes y la variante "BABCOK"', () => {
      // La variante sin la segunda C aparece escrita asi en datos reales del cliente.
      expect(esGrupoBlancaAzur('  babcok brown  ')).toBe(false);
      expect(esGrupoBlancaAzur('lohmann')).toBe(true);
      expect(esGrupoBlancaAzur('HYLINE')).toBe(false);
    });

    it('NO adivina el grupo de una raza desconocida ni de una vacia', () => {
      // `null` y no `false`: elegir un grupo por descarte le pondria 74 o 84 semanas a un ave que
      // no sabemos que es, y el numero se mostraria como si fuera un dato.
      expect(esGrupoBlancaAzur('Ross 308')).toBeNull();
      expect(esGrupoBlancaAzur('')).toBeNull();
      expect(esGrupoBlancaAzur('   ')).toBeNull();
      expect(esGrupoBlancaAzur(null)).toBeNull();
      expect(esGrupoBlancaAzur(undefined)).toBeNull();
    });
  });

  describe('obtenerEtapaCicloPostura — los 3 primeros cortes son iguales en ambos grupos', () => {
    for (const raza of ['LOHMANN LSL', 'BABCOCK BROWN']) {
      it(`${raza}: 1-8 alistamiento, 9-24 levante, 25-28 levante en produccion`, () => {
        expect(obtenerEtapaCicloPostura(raza, 1)).toBe(ETAPA_CICLO_ALISTAMIENTO);
        expect(obtenerEtapaCicloPostura(raza, 8)).toBe(ETAPA_CICLO_ALISTAMIENTO);

        expect(obtenerEtapaCicloPostura(raza, 9)).toBe(ETAPA_CICLO_LEVANTE);
        expect(obtenerEtapaCicloPostura(raza, 24)).toBe(ETAPA_CICLO_LEVANTE);

        expect(obtenerEtapaCicloPostura(raza, 25)).toBe(ETAPA_CICLO_LEVANTE_EN_PRODUCCION);
        expect(obtenerEtapaCicloPostura(raza, 28)).toBe(ETAPA_CICLO_LEVANTE_EN_PRODUCCION);

        expect(obtenerEtapaCicloPostura(raza, 29)).toBe(ETAPA_CICLO_POSTURA);
      });
    }
  });

  describe('obtenerEtapaCicloPostura — solo la duracion de la postura difiere', () => {
    it('roja/criolla cierra a las 102 semanas (28 + 74)', () => {
      expect(obtenerEtapaCicloPostura('BABCOCK BROWN', 102)).toBe(ETAPA_CICLO_POSTURA);
      expect(obtenerEtapaCicloPostura('BABCOCK BROWN', 103)).toBe(ETAPA_CICLO_FUERA_DE_CICLO);
      expect(obtenerEtapaCicloPostura('Criolla', 103)).toBe(ETAPA_CICLO_FUERA_DE_CICLO);
    });

    it('blanca/azur cierra a las 112 semanas (28 + 84)', () => {
      // El corte de las rojas cae DENTRO de la postura de las blancas: es el caso que prueba que
      // el grupo cambia el resultado y no solo la etiqueta.
      expect(obtenerEtapaCicloPostura('LOHMANN LSL', 103)).toBe(ETAPA_CICLO_POSTURA);
      expect(obtenerEtapaCicloPostura('LOHMANN LSL', 112)).toBe(ETAPA_CICLO_POSTURA);
      expect(obtenerEtapaCicloPostura('Azur', 113)).toBe(ETAPA_CICLO_FUERA_DE_CICLO);
    });
  });

  describe('obtenerEtapaCicloPostura — bordes que no se adivinan', () => {
    it('raza no reconocida devuelve null aunque la semana sea valida', () => {
      expect(obtenerEtapaCicloPostura('Ross 308', 30)).toBeNull();
      expect(obtenerEtapaCicloPostura(null, 30)).toBeNull();
    });

    it('semana menor a 1 devuelve null (la semana de vida es 1-based)', () => {
      expect(obtenerEtapaCicloPostura('Azur', 0)).toBeNull();
      expect(obtenerEtapaCicloPostura('Azur', -3)).toBeNull();
    });

    it('semana nula o indefinida devuelve null', () => {
      expect(obtenerEtapaCicloPostura('Azur', null)).toBeNull();
      expect(obtenerEtapaCicloPostura('Azur', undefined)).toBeNull();
    });
  });

  describe('etiquetaEtapaCicloPostura', () => {
    it('traduce cada etapa a lo que se lee en el formulario', () => {
      expect(etiquetaEtapaCicloPostura(ETAPA_CICLO_ALISTAMIENTO)).toBe('Alistamiento');
      expect(etiquetaEtapaCicloPostura(ETAPA_CICLO_LEVANTE)).toBe('Levante');
      expect(etiquetaEtapaCicloPostura(ETAPA_CICLO_LEVANTE_EN_PRODUCCION)).toBe('Levante en producción');
      expect(etiquetaEtapaCicloPostura(ETAPA_CICLO_POSTURA)).toBe('Postura');
      expect(etiquetaEtapaCicloPostura(ETAPA_CICLO_FUERA_DE_CICLO)).toBe('Fuera de ciclo');
    });

    it('sin etapa muestra un guion, NO una etapa inventada', () => {
      expect(etiquetaEtapaCicloPostura(null)).toBe('—');
    });
  });

  describe('calcularEtapaCicloPostura (azucar: fechas -> etapa)', () => {
    it('usa la semana de vida 1-based: el dia del encaset ya es semana 1', () => {
      expect(calcularEtapaCicloPostura('Azur', '2026-01-01', '2026-01-01'))
        .toBe(ETAPA_CICLO_ALISTAMIENTO);
    });

    it('el dia 56 (8 semanas cumplidas) sigue en alistamiento y el 57 pasa a levante', () => {
      // floor(55/7)+1 = 8  ->  ultimo dia de alistamiento.
      expect(calcularEtapaCicloPostura('Azur', '2026-01-01', '2026-02-25'))
        .toBe(ETAPA_CICLO_ALISTAMIENTO);
      // floor(56/7)+1 = 9  ->  primer dia de levante.
      expect(calcularEtapaCicloPostura('Azur', '2026-01-01', '2026-02-26'))
        .toBe(ETAPA_CICLO_LEVANTE);
    });

    it('sin fecha de encaset no hay semana calculable y no se adivina etapa', () => {
      expect(calcularEtapaCicloPostura('Azur', null, '2026-02-26')).toBeNull();
      expect(calcularEtapaCicloPostura('Azur', '2026-01-01', null)).toBeNull();
    });
  });
});

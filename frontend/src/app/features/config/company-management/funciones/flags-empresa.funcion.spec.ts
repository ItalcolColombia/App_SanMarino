import { CompanyFlags } from '../../../../core/services/company-config/active-company-config.service';
import {
  FLAGS_EMPRESA,
  GRUPOS_FLAGS_EMPRESA,
  contarFlagsActivos,
  controlesDeFlags,
  flagsDelFormulario,
  flagsDelGrupo,
  valoresDeFlags
} from './flags-empresa.funcion';

/**
 * V52 — catálogo de flags de comportamiento por empresa.
 *
 * **Por qué existe este archivo.** El fallo recurrente de V52 no fue de cálculo: fue de CABLEADO.
 * F4 y F5 construyeron y probaron la UI y encontraron que el flag no había llegado a
 * `ActiveCompanyConfigService`; F0.1 tuvo que cruzar a mano, carácter por carácter, los cinco
 * `formControlName` contra el `.ts` porque Angular no valida esa coincidencia al compilar. Los dos
 * son el mismo error: un nombre de flag que existe en un lado y no en el otro, y que solo se ve
 * cuando alguien abre la pantalla. Estos tests lo convierten en un fallo de build.
 */
describe('flags-empresa (catálogo de flags por empresa)', () => {
  /**
   * Flags que el runtime lee de la empresa activa. Es el mismo objeto que `CompanyFlags`, escrito
   * como VALOR para poder recorrerlo en tiempo de ejecución (una interfaz de TypeScript no existe
   * en runtime). El `satisfies` lo ata al tipo: si alguien agrega un flag booleano a `CompanyFlags`
   * y no lo agrega acá, el compilador de los specs lo marca — que es justo la mitad que faltaba.
   */
  const FLAGS_QUE_LEE_EL_RUNTIME = {
    manejaCodigosErpAvicola: false,
    clasificacionHuevoPorItems: false,
    permiteTrasladoAvesCrossEtapa: false,
    capturaHuevosEnLevante: false,
    ventaEngordePesoDiferido: false,
    primerRegistroSegunHoraLlegada: false,
    programacionLotesEngorde: false,
    nombreLoteIncluyeCorrida: false,
    manejaInventarioPorSilo: false,
    requiereValidacionSeguimientoDiario: false,
    semanasCicloPosturaPorRaza: false,
    consumoAlimentoSoloHembras: false,
    ocultaMachosEnPostura: false,
    limitaTiposInventarioAlimentoYAves: false,
    separaLotesPosturaPorEtapa: false,
    huevoPrimeraPosturaHastaSemana: null
  } satisfies CompanyFlags;

  it('TODO flag booleano que lee el runtime se puede configurar desde la pantalla de Empresas', () => {
    // El bug de V52/F4 y F5: el flag existía en la BD y en el formulario admin, pero el servicio de
    // runtime no lo exponía (o al revés) y el toggle no hacía nada. Un flag que el runtime lee y
    // nadie puede encender es peor que no tenerlo: parece configurable y no lo es.
    const enElCatalogo = new Set(FLAGS_EMPRESA.map(f => f.key));

    const soloEnElRuntime = Object.entries(FLAGS_QUE_LEE_EL_RUNTIME)
      // El límite de primera postura NO es booleano: va como input numérico propio, fuera del
      // catálogo de checkboxes (mismo patrón que `diasAlimentoPrevioEncaset`).
      .filter(([, valor]) => typeof valor === 'boolean')
      .map(([clave]) => clave)
      .filter(clave => !enElCatalogo.has(clave));

    expect(soloEnElRuntime).toEqual([]);
  });

  it('no hay dos filas con la misma key', () => {
    // Una key repetida rompe `controlesDeFlags` en silencio: el segundo control pisa al primero y
    // uno de los dos checkboxes deja de guardar.
    const keys = FLAGS_EMPRESA.map(f => f.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('toda fila tiene key, título, descripción y grupo — nada a medio cargar', () => {
    for (const f of FLAGS_EMPRESA) {
      expect(f.key.trim().length).toBeGreaterThan(0);
      expect(f.titulo.trim().length).toBeGreaterThan(0);
      expect(f.descripcion.trim().length).toBeGreaterThan(0);
      expect(GRUPOS_FLAGS_EMPRESA).toContain(f.grupo);
    }
  });

  it('las keys son camelCase: es el nombre del campo en el DTO del backend', () => {
    // Un `snake_case` acá viaja al backend como una propiedad que nadie bindea: el flag se guarda
    // en la nada y el formulario lo muestra siempre apagado.
    for (const f of FLAGS_EMPRESA) {
      expect(f.key).toMatch(/^[a-z][A-Za-z0-9]*$/);
    }
  });

  it('cada grupo aparece una sola vez y agrupa a todas sus filas', () => {
    expect(new Set(GRUPOS_FLAGS_EMPRESA).size).toBe(GRUPOS_FLAGS_EMPRESA.length);

    const sumaDeLosGrupos = GRUPOS_FLAGS_EMPRESA
      .reduce((total, g) => total + flagsDelGrupo(g).length, 0);
    expect(sumaDeLosGrupos).toBe(FLAGS_EMPRESA.length);
  });

  describe('controlesDeFlags', () => {
    it('arranca todos los controles en false, NUNCA en null', () => {
      // Un checkbox con valor nulo se ve igual que apagado pero manda `null`, y el backend lee null
      // como «no lo toques»: un flag que el usuario apagó a mano quedaría encendido sin aviso.
      const controles = controlesDeFlags();

      expect(Object.keys(controles).length).toBe(FLAGS_EMPRESA.length);
      for (const f of FLAGS_EMPRESA) {
        expect(controles[f.key]).toEqual([false]);
      }
    });
  });

  describe('valoresDeFlags / flagsDelFormulario — fail-closed', () => {
    it('solo el booleano true enciende: ni 1, ni "true", ni "on"', () => {
      const valores = valoresDeFlags({
        clasificacionHuevoPorItems: true,
        ocultaMachosEnPostura: 1,
        consumoAlimentoSoloHembras: 'true',
        semanasCicloPosturaPorRaza: 'on'
      });

      expect(valores['clasificacionHuevoPorItems']).toBe(true);
      expect(valores['ocultaMachosEnPostura']).toBe(false);
      expect(valores['consumoAlimentoSoloHembras']).toBe(false);
      expect(valores['semanasCicloPosturaPorRaza']).toBe(false);
    });

    it('empresa nula o sin el campo deja todo apagado', () => {
      for (const empresa of [null, undefined, {}]) {
        const valores = valoresDeFlags(empresa);
        expect(Object.keys(valores).length).toBe(FLAGS_EMPRESA.length);
        expect(Object.values(valores).every(v => v === false)).toBe(true);
      }
    });

    it('el payload de guardado siempre lleva los N flags, no solo los encendidos', () => {
      // Mandar solo los encendidos haría que apagar un flag no llegara nunca al backend.
      const payload = flagsDelFormulario({ clasificacionHuevoPorItems: true });

      expect(Object.keys(payload).length).toBe(FLAGS_EMPRESA.length);
      expect(payload['clasificacionHuevoPorItems']).toBe(true);
      expect(payload['manejaInventarioPorSilo']).toBe(false);
    });
  });

  describe('contarFlagsActivos', () => {
    it('cuenta solo los true reales', () => {
      expect(contarFlagsActivos({ clasificacionHuevoPorItems: true, ocultaMachosEnPostura: true })).toBe(2);
      expect(contarFlagsActivos({ clasificacionHuevoPorItems: 'true' })).toBe(0);
      expect(contarFlagsActivos(null)).toBe(0);
    });

    it('no cuenta campos de la empresa que no son flags del catálogo', () => {
      // La ficha de empresa trae decenas de campos; el contador es de flags, no de propiedades.
      expect(contarFlagsActivos({ name: 'Santa Reyes', activo: true, visible: true })).toBe(0);
    });
  });
});

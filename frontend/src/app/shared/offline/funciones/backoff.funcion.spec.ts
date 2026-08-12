import { BASE_MS, TECHO_MS, calcularEspera, esperaDeRetryAfter, proximoIntento } from './backoff.funcion';

/**
 * El backoff.
 *
 * Lo que importa: que crezca, que tenga techo, que no sincronice a todos los equipos de la granja
 * en el mismo instante, y que `Retry-After` del servidor gane sobre el cálculo local.
 */
describe('backoff', () => {
  /** Aleatorio determinista: 0.5 ⇒ jitter nulo, así el test mide el exponencial puro. */
  const sinJitter = () => 0.5;

  describe('calcularEspera', () => {
    it('sin fallos no espera', () => {
      expect(calcularEspera(0, sinJitter)).toBe(0);
    });

    it('el primer fallo espera la base', () => {
      expect(calcularEspera(1, sinJitter)).toBe(BASE_MS);
    });

    it('duplica en cada fallo', () => {
      expect(calcularEspera(2, sinJitter)).toBe(BASE_MS * 2);
      expect(calcularEspera(3, sinJitter)).toBe(BASE_MS * 4);
      expect(calcularEspera(4, sinJitter)).toBe(BASE_MS * 8);
    });

    it('no pasa del techo por más que se acumulen fallos', () => {
      expect(calcularEspera(50, sinJitter)).toBe(TECHO_MS);
      expect(calcularEspera(500, () => 1)).toBeLessThanOrEqual(TECHO_MS);
    });

    it('nunca devuelve un valor negativo', () => {
      expect(calcularEspera(1, () => 0)).toBeGreaterThanOrEqual(0);
    });

    it('el jitter mueve el resultado: veinte tablets no vuelven todas en el mismo milisegundo', () => {
      const conCero = calcularEspera(3, () => 0);
      const conUno = calcularEspera(3, () => 1);

      expect(conCero).toBeLessThan(conUno);
    });
  });

  describe('esperaDeRetryAfter', () => {
    const ahora = Date.parse('2026-08-12T12:00:00Z');

    it('sin cabecera no opina', () => {
      expect(esperaDeRetryAfter(null, ahora)).toBeNull();
      expect(esperaDeRetryAfter('   ', ahora)).toBeNull();
    });

    it('acepta segundos', () => {
      expect(esperaDeRetryAfter('120', ahora)).toBe(120_000);
    });

    it('acepta una fecha HTTP', () => {
      expect(esperaDeRetryAfter('Wed, 12 Aug 2026 12:02:00 GMT', ahora)).toBe(120_000);
    });

    it('una fecha ya pasada no produce espera negativa', () => {
      expect(esperaDeRetryAfter('Wed, 12 Aug 2026 11:00:00 GMT', ahora)).toBe(0);
    });

    it('un valor sin sentido se ignora en vez de romper el envío', () => {
      expect(esperaDeRetryAfter('pronto', ahora)).toBeNull();
    });
  });

  describe('proximoIntento', () => {
    const ahora = 1_000_000;

    it('usa el backoff local cuando el servidor no dice nada', () => {
      expect(proximoIntento(1, ahora, null, sinJitter)).toBe(ahora + BASE_MS);
    });

    it('🔑 Retry-After GANA sobre el backoff local: el servidor sabe mejor cuándo puede atender', () => {
      expect(proximoIntento(1, ahora, '600', sinJitter)).toBe(ahora + 600_000);
    });

    it('Retry-After gana incluso si pide esperar MENOS que el backoff', () => {
      expect(proximoIntento(5, ahora, '1', sinJitter)).toBe(ahora + 1_000);
    });
  });
});

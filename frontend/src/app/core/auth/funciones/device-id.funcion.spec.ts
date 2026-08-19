import { CLAVE_DEVICE_ID, DEVICE_ID_DESCONOCIDO, obtenerDeviceId } from './device-id.funcion';

describe('obtenerDeviceId', () => {
  afterEach(() => {
    localStorage.removeItem(CLAVE_DEVICE_ID);
  });

  it('crea el id y lo persiste la primera vez', () => {
    localStorage.removeItem(CLAVE_DEVICE_ID);

    const id = obtenerDeviceId();

    expect(id).toBeTruthy();
    expect(localStorage.getItem(CLAVE_DEVICE_ID)).toBe(id);
  });

  it('devuelve el MISMO id en la segunda llamada', () => {
    // Si cambiara, cada arranque de la app se vería como una tablet nueva y la lista de
    // dispositivos del usuario se llenaría de sesiones fantasma.
    localStorage.removeItem(CLAVE_DEVICE_ID);

    const primero = obtenerDeviceId();
    const segundo = obtenerDeviceId();

    expect(segundo).toBe(primero);
  });

  it('respeta un id ya guardado (sobrevive al logout)', () => {
    localStorage.setItem(CLAVE_DEVICE_ID, 'tablet-galpon-3');

    expect(obtenerDeviceId()).toBe('tablet-galpon-3');
  });

  it('con el storage bloqueado devuelve "desconocido" en vez de lanzar', () => {
    // Es una etiqueta, no una credencial: que no se pueda leer no puede romper una petición.
    const original = Object.getOwnPropertyDescriptor(window, 'localStorage');
    spyOnProperty(window, 'localStorage', 'get').and.throwError('bloqueado');

    expect(obtenerDeviceId()).toBe(DEVICE_ID_DESCONOCIDO);

    if (original) Object.defineProperty(window, 'localStorage', original);
  });
});

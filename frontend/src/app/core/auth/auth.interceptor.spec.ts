import { HttpClient, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ConexionService } from '../pwa/conexion.service';
import { authInterceptor } from './auth.interceptor';
import type { AuthSession } from './auth.models';
import { EncryptionService } from './encryption.service';
import { SessionTimeoutService } from './session-timeout.service';
import { TokenStorageService } from './token-storage.service';

describe('authInterceptor · límites de credenciales y sesión', () => {
  const api = 'https://api.example/api';
  const apiOriginal = environment.apiUrl;
  let http: HttpClient;
  let peticiones: HttpTestingController;
  let sesion: AuthSession | null;
  let storage: jasmine.SpyObj<TokenStorageService>;
  let encryption: jasmine.SpyObj<EncryptionService>;
  let timeout: jasmine.SpyObj<SessionTimeoutService>;
  let conexion: jasmine.SpyObj<ConexionService>;

  beforeEach(() => {
    environment.apiUrl = api;
    sesion = {
      accessToken: 'token-a', platformKey: 'firma-a',
      activeCompany: 'Empresa A', activeCompanyId: 7,
      activePaisId: 2, activePaisNombre: 'País A'
    } as AuthSession;
    storage = jasmine.createSpyObj<TokenStorageService>('storage', ['get', 'getToken']);
    storage.get.and.callFake(() => sesion);
    storage.getToken.and.callFake(() => sesion?.accessToken ?? null);
    encryption = jasmine.createSpyObj<EncryptionService>('encryption', ['encryptSecretUp']);
    encryption.encryptSecretUp.and.resolveTo('firma-legacy-cifrada');
    timeout = jasmine.createSpyObj<SessionTimeoutService>('timeout', ['onUnauthorized']);
    conexion = jasmine.createSpyObj<ConexionService>('conexion', ['marcarExitoDeRed', 'marcarFalloDeRed']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: TokenStorageService, useValue: storage },
        { provide: EncryptionService, useValue: encryption },
        { provide: SessionTimeoutService, useValue: timeout },
        { provide: ConexionService, useValue: conexion }
      ]
    });
    http = TestBed.inject(HttpClient);
    peticiones = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    peticiones.verify();
    environment.apiUrl = apiOriginal;
  });

  it('envía bearer, firma derivada, dispositivo y scope juntos al backend', () => {
    http.get(`${api}/lotes`).subscribe();
    const r = peticiones.expectOne(`${api}/lotes`);
    expect(r.request.headers.get('Authorization')).toBe('Bearer token-a');
    expect(r.request.headers.get('X-Secret-Up')).toBe('firma-a');
    expect(r.request.headers.get('X-Device-Id')).toBeTruthy();
    expect(r.request.headers.get('X-Active-Company')).toBe('Empresa A');
    expect(r.request.headers.get('X-Active-Company-Id')).toBe('7');
    expect(r.request.headers.get('X-Active-Pais')).toBe('2');
    expect(r.request.headers.get('X-Active-Pais-Nombre')).toBe('País A');
    expect(encryption.encryptSecretUp).not.toHaveBeenCalled();
    r.flush([]);
    expect(conexion.marcarExitoDeRed).toHaveBeenCalledTimes(1);
  });

  for (const url of [
    'https://external.example/api/lotes',
    'https://api.example.evil.test/api/lotes',
    'https://usuario@api.example/api/lotes',
    'https://api.example/api/../externo',
    'https://api.example/api-external/lotes',
    '/assets/config.json'
  ]) {
    it(`no envía credenciales ni procesa 401 externos: ${url}`, () => {
      http.get(url).subscribe({ error: () => undefined });
      const r = peticiones.expectOne(url);
      expect(r.request.headers.keys()).toEqual([]);
      expect(storage.get).not.toHaveBeenCalled();
      expect(encryption.encryptSecretUp).not.toHaveBeenCalled();
      r.flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(timeout.onUnauthorized).not.toHaveBeenCalled();
      expect(conexion.marcarExitoDeRed).not.toHaveBeenCalled();
    });
  }

  it('envía credenciales al API relativo de producción', () => {
    environment.apiUrl = '/api';
    http.get('/api/lotes').subscribe();
    const r = peticiones.expectOne('/api/lotes');
    expect(r.request.headers.get('Authorization')).toBe('Bearer token-a');
    r.flush([]);
  });

  it('login no hereda credenciales ni tenant y su 401 no cierra la sesión previa', () => {
    const headers = new HttpHeaders({
      Authorization: 'Bearer antiguo', 'X-Secret-Up': 'firma-antigua',
      'X-Active-Company': 'Anterior', 'X-Active-Company-Id': '1',
      'X-Active-Pais': '1', 'X-Active-Pais-Nombre': 'Anterior'
    });
    http.post(`${api}/Auth/login`, {}, { headers }).subscribe({ error: () => undefined });
    const r = peticiones.expectOne(`${api}/Auth/login`);
    expect(r.request.headers.keys()).toEqual(['X-Device-Id']);
    expect(storage.get).not.toHaveBeenCalled();
    expect(encryption.encryptSecretUp).not.toHaveBeenCalled();
    r.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(timeout.onUnauthorized).not.toHaveBeenCalled();
    expect(conexion.marcarExitoDeRed).toHaveBeenCalledTimes(1);
  });

  it('un 401 de la sesión actual sí cierra sesión', () => {
    http.get(`${api}/lotes`).subscribe({ error: () => undefined });
    peticiones.expectOne(`${api}/lotes`).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(timeout.onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('un 401 tardío de A no cierra la sesión recién iniciada de B', () => {
    http.get(`${api}/lotes`).subscribe({ error: () => undefined });
    const pendiente = peticiones.expectOne(`${api}/lotes`);
    sesion = { ...sesion!, accessToken: 'token-b', platformKey: 'firma-b' };
    pendiente.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(timeout.onUnauthorized).not.toHaveBeenCalled();
  });

  it('un 401 posterior al logout no vuelve a cerrar sesión', () => {
    http.get(`${api}/lotes`).subscribe({ error: () => undefined });
    const pendiente = peticiones.expectOne(`${api}/lotes`);
    sesion = null;
    pendiente.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(timeout.onUnauthorized).not.toHaveBeenCalled();
  });

  it('el rechazo de plataforma conserva la sesión actual', () => {
    http.get(`${api}/lotes`).subscribe({ error: () => undefined });
    peticiones.expectOne(`${api}/lotes`).flush({ errorCode: 'platform-secret' }, {
      status: 401, statusText: 'Unauthorized'
    });
    expect(timeout.onUnauthorized).not.toHaveBeenCalled();
  });

  it('un fallo de red conserva sesión y registra la desconexión', () => {
    http.get(`${api}/lotes`).subscribe({ error: () => undefined });
    peticiones.expectOne(`${api}/lotes`).error(new ProgressEvent('error'));
    expect(conexion.marcarFalloDeRed).toHaveBeenCalledTimes(1);
    expect(timeout.onUnauthorized).not.toHaveBeenCalled();
  });

  it('conserva la firma legacy para sesiones anteriores', async () => {
    sesion = { ...sesion!, platformKey: undefined };
    http.get(`${api}/lotes`).subscribe();
    await Promise.resolve();
    const r = peticiones.expectOne(`${api}/lotes`);
    expect(r.request.headers.get('Authorization')).toBe('Bearer token-a');
    expect(r.request.headers.get('X-Secret-Up')).toBe('firma-legacy-cifrada');
    r.flush([]);
  });
});

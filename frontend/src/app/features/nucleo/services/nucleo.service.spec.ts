import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { NucleoService, NucleoDto } from './nucleo.service';
import { environment } from '../../../../environments/environment';

describe('NucleoService', () => {
  let service: NucleoService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/Nucleo`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(NucleoService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() cachea: dos llamadas seguidas hacen un solo GET', () => {
    service.getAll().subscribe();
    service.getAll().subscribe();

    const reqs = httpMock.match(baseUrl);
    expect(reqs.length).toBe(1);
    reqs[0].flush([] as NucleoDto[]);
  });

  it('getAll(true) descarta la caché y vuelve a pedir', () => {
    service.getAll().subscribe();
    httpMock.expectOne(baseUrl).flush([] as NucleoDto[]);

    service.getAll(true).subscribe();
    httpMock.expectOne(baseUrl).flush([] as NucleoDto[]);
  });

  it('invalidate() fuerza un nuevo GET en el siguiente getAll()', () => {
    service.getAll().subscribe();
    httpMock.expectOne(baseUrl).flush([] as NucleoDto[]);

    service.invalidate();

    service.getAll().subscribe();
    httpMock.expectOne(baseUrl).flush([] as NucleoDto[]);
  });

  it('delete() invalida la caché (el siguiente getAll refetch)', () => {
    service.getAll().subscribe();
    httpMock.expectOne(baseUrl).flush([] as NucleoDto[]);

    service.delete('N1', 5).subscribe();
    httpMock.expectOne(`${baseUrl}/N1/5`).flush(null);

    service.getAll().subscribe();
    httpMock.expectOne(baseUrl).flush([] as NucleoDto[]);
  });
});

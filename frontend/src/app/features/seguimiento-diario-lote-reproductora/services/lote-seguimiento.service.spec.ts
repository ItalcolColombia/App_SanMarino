import { TestBed } from '@angular/core/testing';

import { LoteSeguimientoService } from './lote-seguimiento.service';

describe('LoteSeguimientoService', () => {
  let service: LoteSeguimientoService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(LoteSeguimientoService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});

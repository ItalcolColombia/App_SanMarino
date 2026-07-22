import { TestBed } from '@angular/core/testing';

import { GestionGranjasRefreshService, GranjaChangeSource } from './gestion-granjas-refresh.service';

describe('GestionGranjasRefreshService', () => {
  let service: GestionGranjasRefreshService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(GestionGranjasRefreshService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('emite en changes$ el origen notificado', () => {
    const recibidos: GranjaChangeSource[] = [];
    const sub = service.changes$.subscribe(s => recibidos.push(s));

    service.notify('farm');
    service.notify('nucleo');
    service.notify('galpon');

    expect(recibidos).toEqual(['farm', 'nucleo', 'galpon']);
    sub.unsubscribe();
  });

  it('no reenvía eventos anteriores a un suscriptor nuevo (Subject, no BehaviorSubject)', () => {
    service.notify('farm');

    const recibidos: GranjaChangeSource[] = [];
    const sub = service.changes$.subscribe(s => recibidos.push(s));
    service.notify('nucleo');

    expect(recibidos).toEqual(['nucleo']);
    sub.unsubscribe();
  });
});

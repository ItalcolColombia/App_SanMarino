import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { RegistrosTrasladosComponent } from './registros-traslados.component';
import { HistorialTrasladoLoteDto } from '../../services/traslados-aves.service';
import { CacheConsultasService } from '../../../../shared/offline/cache-consultas.service';
import { OutboxService } from '../../../../shared/offline/outbox.service';

/**
 * Las columnas Fecha y Usuario de esta tabla mostraban SIEMPRE `—`: la interfaz del front declaraba
 * `fechaTraslado: Date` y `usuarioNombre`, dos campos que el backend nunca emitio. Este test renderiza
 * la tabla REAL con la respuesta REAL del endpoint (copiada de un smoke contra `/Lote/151/historial-
 * traslados`) y fija las dos celdas, para que un rename del wire vuelva a romperlo en el CI y no en
 * la pantalla del usuario.
 */
describe('RegistrosTrasladosComponent · tabla de traslados de lote', () => {
  let fixture: ComponentFixture<RegistrosTrasladosComponent>;

  /** Respuesta textual del backend: fila con fecha y usuario resueltos, y fila legacy sin ninguno. */
  const RESPUESTA: HistorialTrasladoLoteDto[] = [
    {
      id: 1, loteOriginalId: 151, loteNuevoId: 151,
      granjaOrigenId: 4, granjaOrigenNombre: 'NIZA I',
      granjaDestinoId: 5, granjaDestinoNombre: 'NIZA III',
      nucleoDestinoId: '543', nucleoDestinoNombre: 'San maria Uno',
      galponDestinoId: 'G0536', galponDestinoNombre: 'Galpon 2',
      observaciones: 'movido el 25-ago, digitado el 1-sep',
      createdByUserId: 100030333, createdByUserName: 'Cesar Eduardo Hurtado Riascos',
      createdAt: '2026-09-01T14:30:00', fechaTraslado: '2026-08-25'
    },
    {
      id: 2, loteOriginalId: 151, loteNuevoId: 151,
      granjaOrigenId: 3, granjaOrigenNombre: 'Guadalupe',
      granjaDestinoId: 4, granjaDestinoNombre: 'NIZA I',
      nucleoDestinoId: null, nucleoDestinoNombre: null,
      galponDestinoId: null, galponDestinoNombre: null,
      observaciones: 'fila legacy sin fecha_traslado y con usuario irresoluble',
      createdByUserId: 968091594, createdByUserName: null,
      createdAt: '2026-07-14T10:00:00', fechaTraslado: null
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegistrosTrasladosComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(RegistrosTrasladosComponent);
    const cmp = fixture.componentInstance;

    // La tabla vive detras de `@if (selectedFarmId())`; se puebla el estado sin pasar por la red.
    cmp.selectedFarmId.set(5);
    cmp.tabActivo.set('lotes');
    cmp.loadingTrasladosLotes.set(false);
    cmp.historialTrasladosLotes.set(RESPUESTA);
    fixture.detectChanges();
  });

  // Montar el componente real arrastra el cache offline, que abre IndexedDB y NO la cierra sola.
  // Una conexion viva bloquea el upgrade de esquema de `offline-db.spec.ts`, y el sintoma es un
  // timeout que apunta a OTRA prueba (misma advertencia que documenta `cerrarConexion`).
  afterEach(() => {
    fixture.destroy();
    TestBed.inject(CacheConsultasService).cerrarConexion();
    TestBed.inject(OutboxService).cerrarConexion();
  });

  function celdas(fila: number): string[] {
    const tr = fixture.nativeElement.querySelectorAll('tr.ux-row')[fila] as HTMLElement;
    return Array.from(tr.querySelectorAll('td')).map(td => (td.textContent ?? '').trim());
  }

  it('pinta el dia del traslado, no el de digitacion', () => {
    expect(celdas(0)[0]).toBe('25/8/2026');
  });

  it('pinta el nombre del usuario que registro', () => {
    expect(celdas(0)[5]).toBe('Cesar Eduardo Hurtado Riascos');
  });

  it('en la fila legacy cae a la fecha de digitacion y deja el usuario en guion', () => {
    expect(celdas(1)[0]).toBe('14/7/2026');
    expect(celdas(1)[5]).toBe('\u2014');
  });

  it('ninguna de las dos columnas queda en guion cuando el dato existe', () => {
    expect(celdas(0)[0]).not.toBe('\u2014');
    expect(celdas(0)[5]).not.toBe('\u2014');
  });
});

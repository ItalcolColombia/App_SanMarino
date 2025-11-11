// src/app/features/inventario/services/inventario.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../../core/auth/token-storage.service';
import { AuthSession } from '../../../core/auth/auth.models';

/** ======== Tipos comunes ======== */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/** Apoyos (granjas / catálogo) */
export interface FarmDto {
  id: number;
  name: string;
  companyId: number;
}
export interface CatalogItemDto {
  id: number;
  codigo: string;
  nombre: string;
  activo: boolean;
}

/** Inventario (stock actual por granja) */
export interface FarmInventoryDto {
  id: number;
  farmId: number;
  catalogItemId: number;
  codigo: string;
  nombre: string;
  quantity: number;
  unit: string;
  location?: string | null;
  lotNumber?: string | null;
  expirationDate?: string | null; // ISO
  unitCost?: number | null;
  active: boolean;
  metadata?: any;
  responsibleUserId?: string | null;
  createdAt: string; // ISO
  updatedAt: string; // ISO
}

/** CRUD inventario */
export interface FarmInventoryCreateRequest {
  catalogItemId?: number;
  codigo?: string;
  quantity: number;
  unit?: string;
  location?: string | null;
  lotNumber?: string | null;
  expirationDate?: string | null; // ISO
  unitCost?: number | null;
  metadata?: any;
  active: boolean;
  responsibleUserId?: string | null;
}
export interface FarmInventoryUpdateRequest {
  quantity: number;
  unit?: string;
  location?: string | null;
  lotNumber?: string | null;
  expirationDate?: string | null; // ISO
  unitCost?: number | null;
  metadata?: any;
  active: boolean;
  responsibleUserId?: string | null;
}

/** Movimientos */
export type MovementType = 'Entry' | 'Exit' | 'TransferIn' | 'TransferOut' | 'Adjust';
export interface InventoryMovementDto {
  id: number;
  farmId: number;
  catalogItemId: number;
  codigo: string;
  nombre: string;
  quantity: number;
  movementType: MovementType;
  unit: string;
  reference?: string | null;
  reason?: string | null;
  origin?: string | null;
  destination?: string | null;
  transferGroupId?: string | null;
  metadata?: any;
  responsibleUserId?: string | null;
  createdAt: string; // ISO
}
export interface MovementQuery {
  from?: string; // ISO
  to?: string;   // ISO
  catalogItemId?: number;
  codigo?: string;
  type?: MovementType;
  page?: number;
  pageSize?: number;
}

/** Entradas / Salidas / Traslado / Ajuste (requests) */
export interface InventoryEntryRequest {
  catalogItemId?: number;
  codigo?: string;
  quantity: number;      // positivo
  unit?: string;
  reference?: string;
  reason?: string;
  origin?: string;       // Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")
  metadata?: any;
}
export interface InventoryExitRequest extends InventoryEntryRequest {
  destination?: string;  // Destino para salidas (ej: "Venta", "Movimiento", "Devolución")
}
export interface InventoryTransferRequest extends InventoryEntryRequest {
  toFarmId: number;
}
export interface InventoryAdjustRequest extends InventoryEntryRequest {
  // quantity puede ser positivo o negativo según el ajuste
}

/** ====== Kardex ====== */
export interface KardexItemDto {
  fecha: string;           // ISO date
  tipo: MovementType | string;
  referencia?: string | null;
  cantidad: number;        // + entrada / - salida
  unidad: string;
  saldo: number;           // saldo acumulado tras el movimiento
  motivo?: string | null;
}

/** ====== Conteo físico ====== */
export interface StockCountItemRequest {
  catalogItemId: number;
  conteo: number;
}
export interface StockCountRequest {
  items: StockCountItemRequest[];
}

@Injectable({ providedIn: 'root' })
export class InventarioService {
  private readonly api = environment.apiUrl;
  private tokenStorage = inject(TokenStorageService);

  constructor(private http: HttpClient) {}

  // ===== Apoyos =====
  /** Granjas filtradas por usuario en sesión */
  getFarms(): Observable<FarmDto[]> {
    // Obtener el ID del usuario de la sesión actual
    const session: AuthSession | null = this.tokenStorage.get();
    const userId = session?.user?.id;

    // Si no hay sesión o user ID, esperar un poco y reintentar
    if (!session || !userId) {
      return new Observable(observer => {
        setTimeout(() => {
          const retrySession: AuthSession | null = this.tokenStorage.get();
          const retryUserId = retrySession?.user?.id;

          if (retrySession && retryUserId) {
            this.getFarmsWithUserId(retryUserId).subscribe(observer);
          } else {
            observer.error('No hay sesión de usuario disponible');
          }
        }, 500); // Esperar medio segundo
      });
    }

    return this.getFarmsWithUserId(userId);
  }

  /** Obtiene granjas del usuario específico usando id_user_session */
  private getFarmsWithUserId(userId: string): Observable<FarmDto[]> {
    const params = new HttpParams().set('id_user_session', userId);
    return this.http.get<FarmDto[]>(`${this.api}/Farm`, { params });
  }

  /** Catálogo (array) mapeando .items del paginado */
  getCatalogo(q = '', page = 1, pageSize = 1000): Observable<CatalogItemDto[]> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q && q.trim()) params = params.set('q', q.trim());
    return this.http
      .get<PagedResult<CatalogItemDto>>(`${this.api}/catalogo-alimentos`, { params })
      .pipe(map(res => res.items ?? []));
  }

  /** (Opcional) Catálogo paginado completo */
  getCatalogoPaged(q = '', page = 1, pageSize = 20): Observable<PagedResult<CatalogItemDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q && q.trim()) params = params.set('q', q.trim());
    return this.http.get<PagedResult<CatalogItemDto>>(`${this.api}/catalogo-alimentos`, { params });
  }

  // ===== Inventario (stock) =====
  getInventory(farmId: number): Observable<FarmInventoryDto[]> {
    return this.http.get<FarmInventoryDto[]>(`${this.api}/farms/${farmId}/inventory`);
  }
  /** Alias cómodo para los componentes que esperan getStock() */
  getStock(farmId: number): Observable<FarmInventoryDto[]> {
    return this.getInventory(farmId);
  }
  getInventoryById(farmId: number, id: number): Observable<FarmInventoryDto> {
    return this.http.get<FarmInventoryDto>(`${this.api}/farms/${farmId}/inventory/${id}`);
  }
  createOrReplaceInventory(farmId: number, payload: FarmInventoryCreateRequest): Observable<FarmInventoryDto> {
    return this.http.post<FarmInventoryDto>(`${this.api}/farms/${farmId}/inventory`, payload);
  }
  updateInventory(farmId: number, id: number, payload: FarmInventoryUpdateRequest): Observable<FarmInventoryDto> {
    return this.http.put<FarmInventoryDto>(`${this.api}/farms/${farmId}/inventory/${id}`, payload);
  }
  deleteInventory(farmId: number, id: number, hard = false): Observable<void> {
    let params = new HttpParams();
    if (hard) params = params.set('hard', true);
    return this.http.delete<void>(`${this.api}/farms/${farmId}/inventory/${id}`, { params });
  }

  // ===== Movimientos =====
  postEntry(farmId: number, payload: InventoryEntryRequest): Observable<InventoryMovementDto> {
    return this.http.post<InventoryMovementDto>(`${this.api}/farms/${farmId}/inventory/movements/in`, payload);
  }
  postExit(farmId: number, payload: InventoryExitRequest): Observable<InventoryMovementDto> {
    return this.http.post<InventoryMovementDto>(`${this.api}/farms/${farmId}/inventory/movements/out`, payload);
  }
  postTransfer(fromFarmId: number, payload: InventoryTransferRequest): Observable<{ out: InventoryMovementDto; In: InventoryMovementDto } | any> {
    return this.http.post(`${this.api}/farms/${fromFarmId}/inventory/movements/transfer`, payload);
  }

  /** NUEVO: Ajuste (+ / -) */
  postAdjust(farmId: number, payload: InventoryAdjustRequest): Observable<InventoryMovementDto> {
    // quantity puede venir positivo (sumar) o negativo (restar)
    return this.http.post<InventoryMovementDto>(`${this.api}/farms/${farmId}/inventory/movements/adjust`, payload);
  }

  /** (Opcional) Listado/consulta de movimientos si el API lo expone */
  listMovements(farmId: number, q: MovementQuery = {}): Observable<PagedResult<InventoryMovementDto>> {
    let params = new HttpParams();
    if (q.from)          params = params.set('from', q.from);
    if (q.to)            params = params.set('to', q.to);
    if (q.catalogItemId) params = params.set('catalogItemId', q.catalogItemId);
    if (q.codigo)        params = params.set('codigo', q.codigo);
    if (q.type)          params = params.set('type', q.type);
    if (q.page)          params = params.set('page', q.page);
    if (q.pageSize)      params = params.set('pageSize', q.pageSize);
    return this.http.get<PagedResult<InventoryMovementDto>>(
      `${this.api}/farms/${farmId}/inventory/movements`, { params }
    );
  }

  /** (Opcional) Detalle de movimiento */
  getMovementById(farmId: number, movementId: number): Observable<InventoryMovementDto> {
    return this.http.get<InventoryMovementDto>(`${this.api}/farms/${farmId}/inventory/movements/${movementId}`);
  }

  // ===== NUEVO: Kardex =====
  getKardex(
    farmId: number,
    catalogItemId: number,
    from?: string,
    to?: string
  ): Observable<KardexItemDto[]> {
    let params = new HttpParams().set('catalogItemId', catalogItemId);
    if (from) params = params.set('from', from);
    if (to)   params = params.set('to', to);
    // Ajusta la ruta si tu API usa otro path (por ej. /kardex/items)
    return this.http.get<KardexItemDto[]>(`${this.api}/farms/${farmId}/inventory/kardex`, { params });
  }

  // ===== NUEVO: Conteo físico =====
  postConteoFisico(farmId: number, payload: StockCountRequest): Observable<void> {
    // Ajusta el path si tu API real es distinto (e.g. /stock-counts, /counts)
    return this.http.post<void>(`${this.api}/farms/${farmId}/inventory/stock-count`, payload);
  }
}

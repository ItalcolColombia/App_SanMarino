import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, map, of, switchMap, tap } from 'rxjs';
import { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import {
  faTachometerAlt, faClipboardList, faCalendarDay, faChartBar, faHeartbeat,
  faCog, faUsers, faChevronDown, faSignOutAlt, faList, faBuilding,
  faGlobe, faMapMarkerAlt, faCity, faBoxesAlt, faWarehouse, faDollarSign
} from '@fortawesome/free-solid-svg-icons';
import { environment } from '../../../environments/environment';
import { TokenStorageService } from '../../core/auth/token-storage.service';
import { MenuItem as SessionMenuItem } from '../../core/auth/auth.models';

export interface ApiMenuItem {
  id: number;
  label: string;
  icon?: string | null;   // ej: "tachometer-alt"
  route?: string | null;  // ej: "/config/farms-list"
  order: number;
  children: ApiMenuItem[];
}

export interface UiMenuItem {
  id: number;
  label: string;
  icon: IconDefinition | null;
  link?: string[];
  children: UiMenuItem[];
  expanded?: boolean;
}

const ICON_MAP: Record<string, IconDefinition> = {
  'tachometer-alt': faTachometerAlt,
  'clipboard-list': faClipboardList,
  'calendar-day':   faCalendarDay,
  'chart-bar':      faChartBar,
  'heartbeat':      faHeartbeat,
  'cog':            faCog,
  'users':          faUsers,
  'chevron-down':   faChevronDown,
  'sign-out-alt':   faSignOutAlt,
  'list':           faList,
  'building':       faBuilding,
  'globe':          faGlobe,
  'map-marker-alt': faMapMarkerAlt,
  'city':           faCity,
  'boxes-alt':      faBoxesAlt,
  'warehouse':      faWarehouse,
  'dollar-sign':    faDollarSign
};

function toLinkArray(route?: string | null): string[] | undefined {
  if (!route) return undefined;
  const clean = route.startsWith('/') ? route.substring(1) : route;
  // Angular Router espera arrays tipo ['/', 'config', 'farm-management']
  return ['/', ...clean.split('/')];
}

function mapApiToUi(node: ApiMenuItem): UiMenuItem {
  return {
    id: node.id,
    label: node.label,
    icon: node.icon ? (ICON_MAP[node.icon] ?? null) : null,
    link: toLinkArray(node.route ?? undefined),
    children: (node.children ?? []).map(mapApiToUi),
    expanded: false
  };
}

function mapSessionToUi(node: SessionMenuItem): UiMenuItem {
  return {
    id: node.id,
    label: node.label,
    icon: node.icon ? (ICON_MAP[node.icon] ?? null) : null,
    link: toLinkArray(node.route ?? undefined),
    children: (node.children ?? []).map(mapSessionToUi),
    expanded: false
  };
}

@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly subject = new BehaviorSubject<UiMenuItem[]>([]);
  readonly menu$ = this.subject.asObservable();

  constructor(
    private http: HttpClient,
    private storage: TokenStorageService
  ) {
    // Inicializa desde storage al construir (si hay sesión)
    const stored = this.storage.getMenu();
    if (stored?.length) {
      const ui = stored.map(mapSessionToUi);
      this.subject.next(ui);
    }
  }

 /** Carga el menú desde la API del backend y actualiza storage + BehaviorSubject. */
preloadMyMenu(companyId?: number) {
  const url = `${environment.apiUrl}/Roles/menus/me`;
  let params = new HttpParams();
  if (companyId != null) params = params.set('companyId', String(companyId));

  return this.http.get<ApiMenuItem[]>(url, { params }).pipe(
    // 1) guarda en storage a partir del arreglo crudo que devuelve la API
    tap((apiArr: ApiMenuItem[]) => {
      const rawToStore: SessionMenuItem[] = (apiArr ?? []).map(apiToSession);
      this.storage.updateMenu(rawToStore);
    }),
    // 2) transforma a UI para pintar en el sidebar
    map((apiArr: ApiMenuItem[]) => (apiArr ?? []).map(mapApiToUi)),
    // 3) actualiza el subject que consumen los componentes
    tap((ui) => this.subject.next(ui))
  );
}

  /** Asegura tener menú cargado: toma de storage si hay; si no, pide a la API. */
  ensureLoaded(companyId?: number): Observable<UiMenuItem[]> {
    const current = this.subject.value;
    if (current && current.length) return of(current);
    // intenta cargar desde storage
    const stored = this.storage.getMenu();
    if (stored?.length) {
      const ui = stored.map(mapSessionToUi);
      this.subject.next(ui);
      return of(ui);
    }
    // cae a la API
    return this.preloadMyMenu(companyId);
  }

  /** Busca un item por id dentro del árbol en memoria. */
  getDetailsById(id: number): Observable<UiMenuItem | null> {
    return this.ensureLoaded().pipe(
      map(tree => this.findById(tree, id))
    );
  }

  /** Devuelve múltiples items por id (en el mismo orden que los ids). */
  getManyByIds(ids: number[]): Observable<UiMenuItem[]> {
    const set = new Set(ids);
    return this.ensureLoaded().pipe(
      map(tree => {
        const index = new Map<number, UiMenuItem>();
        this.flatten(tree).forEach(n => index.set(n.id, n));
        return ids.map(i => index.get(i)).filter((x): x is UiMenuItem => !!x);
      })
    );
  }

  /** Permite setear manualmente (tests, etc.) */
  setMenuForTest(items: UiMenuItem[]) {
    this.subject.next(items);
  }

  // ===== Helpers internos =====

  private findById(nodes: UiMenuItem[], id: number): UiMenuItem | null {
    for (const n of nodes) {
      if (n.id === id) return n;
      const child = this.findById(n.children ?? [], id);
      if (child) return child;
    }
    return null;
  }

  private flatten(nodes: UiMenuItem[], acc: UiMenuItem[] = []): UiMenuItem[] {
    for (const n of nodes) {
      acc.push(n);
      if (n.children?.length) this.flatten(n.children, acc);
    }
    return acc;
  }

  reset() {
    this.subject.next([]);
  }

}

// helper para guardar en storage (mapea ApiMenuItem -> SessionMenuItem de forma recursiva)
function apiToSession(node: ApiMenuItem): SessionMenuItem {
  return {
    id: node.id,
    label: node.label,
    icon: node.icon ?? undefined,
    route: node.route ?? undefined,
    order: node.order,
    children: (node.children ?? []).map(apiToSession)
  };
}

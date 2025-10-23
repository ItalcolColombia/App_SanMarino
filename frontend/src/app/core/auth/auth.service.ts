import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LoginPayload, LoginResult, AuthSession, MenuItem, RoleMenusLite } from './auth.models';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private storage = inject(TokenStorageService);
  private baseUrl = `${environment.apiUrl}/Auth`;

  // Sesión reactiva
  readonly session$ = this.storage.session$;

  // Selectores útiles
  readonly menu$ = this.session$.pipe(map(s => s?.menu ?? []));
  readonly menusByRole$ = this.session$.pipe(map(s => s?.menusByRole ?? []));
  readonly roles$ = this.session$.pipe(map(s => s?.user.roles ?? []));
  readonly permisos$ = this.session$.pipe(map(s => s?.user.permisos ?? []));

  // Guarda por defecto en sessionStorage (hasta cerrar sesión/pestaña)
  login(payload: LoginPayload, remember = false) {
    return this.http.post<LoginResult>(`${this.baseUrl}/login`, payload).pipe(
      map(res => {
        const companies = res.empresas ?? [];
        const session: AuthSession = {
          accessToken:  res.token,
          refreshToken: res.refreshToken,
          user: {
            id: res.userId,
            username: res.username,
            firstName: res.firstName,
            surName: res.surName,
            fullName: res.fullName,
            roles: res.roles ?? [],
            permisos: res.permisos ?? [],
          },
          companies,
          activeCompany: companies[0] ?? undefined,
          menu: res.menu ?? [],
          menusByRole: res.menusByRole ?? []
        };
        return session;
      }),
      tap(session => this.storage.save(session, remember))
    );
  }

  logout(opts?: { hard?: boolean }) {
    if (opts?.hard) {
      this.storage.clearAllTemporal(); // borra todo lo temporal (sessionStorage completo)
    } else {
      this.storage.clear(); // borra solo la sesión guardada
    }
  }

  isAuthenticated() {
    return !!this.storage.getToken();
  }

  // Actualiza los datos del usuario en la sesión actual
  updateUserData(userData: { firstName?: string; surName?: string; fullName?: string }) {
    const currentSession = this.storage.get();
    if (!currentSession) return;

    const updatedSession = {
      ...currentSession,
      user: {
        ...currentSession.user,
        fullName: userData.fullName || `${userData.firstName || ''} ${userData.surName || ''}`.trim() || currentSession.user.fullName
      }
    };

    const persistedInLocal = !!localStorage.getItem('auth_session');
    this.storage.save(updatedSession, persistedInLocal);
  }

  // Recarga el menú dinámico desde el backend
  reloadMenu() {
    return this.http.get<{ menu: MenuItem[]; menusByRole: RoleMenusLite[] }>(`${this.baseUrl}/bootstrap`).pipe(
      tap(response => {
        const currentSession = this.storage.get();
        if (!currentSession) return;

        const updatedSession = {
          ...currentSession,
          menu: response.menu,
          menusByRole: response.menusByRole
        };

        const persistedInLocal = !!localStorage.getItem('auth_session');
        this.storage.save(updatedSession, persistedInLocal);
      })
    );
  }

  // (Opcional) refrescar menú desde API por cambio de empresa
  refreshMenuForCompany(companyId?: number) {
    const url = `${environment.apiUrl}/Roles/menus/me`;
    let params = new HttpParams();
    if (companyId != null) params = params.set('companyId', String(companyId));

    return this.http.get<MenuItem[]>(url, { params }).pipe(
      tap(menu => this.storage.updateMenu(menu))
    );
  }
}

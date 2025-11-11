import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, tap, switchMap, catchError } from 'rxjs/operators';
import { from, throwError, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginPayload, LoginResult, AuthSession, MenuItem, RoleMenusLite, EmailQueueStatus } from './auth.models';
import { TokenStorageService } from './token-storage.service';
import { EncryptionService } from './encryption.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private storage = inject(TokenStorageService);
  private encryption = inject(EncryptionService);
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
    // 1. Encriptar los datos del login antes de enviarlos al backend
    return from(this.encryption.encryptForBackend(payload)).pipe(
      // 2. Enviar datos encriptados al backend
      switchMap(encryptedPayload =>
        this.http.post(`${this.baseUrl}/login`, { encryptedData: encryptedPayload }, {
          responseType: 'text' // Recibimos texto plano encriptado
        })
      ),
      // 3. Desencriptar la respuesta del backend (puede ser grande con menús y datos)
      switchMap(encryptedResponse => {
        console.log('🔐 Respuesta encriptada recibida, iniciando desencriptación...', encryptedResponse?.substring(0, 50) + '...');
        return from(this.encryption.decryptFromBackend<LoginResult>(encryptedResponse));
      }),
      map(res => {
        // Debug completo del objeto recibido
        const rawRes = res as any;
        console.log('✅ Datos desencriptados correctamente, procesando sesión...', {
          allKeys: Object.keys(rawRes),
          hasToken_camel: !!rawRes.token,
          hasToken_Pascal: !!rawRes.Token,
          tokenValue_camel: rawRes.token ? rawRes.token.substring(0, 30) + '...' : 'undefined',
          tokenValue_Pascal: rawRes.Token ? rawRes.Token.substring(0, 30) + '...' : 'undefined',
          hasMenu: !!(rawRes.menu || rawRes.Menu) && (rawRes.menu || rawRes.Menu)?.length > 0,
          menuItems: (rawRes.menu || rawRes.Menu)?.length ?? 0,
        });

        // Mapear campos del backend al formato esperado
        // El backend ahora envía en camelCase (configurado en EncryptionService)
        // Pero verificamos ambos casos por seguridad
        const token = rawRes.token || rawRes.Token || '';
        const userId = rawRes.userId || rawRes.UserId || '';
        const username = rawRes.username || rawRes.Username || '';
        const firstName = rawRes.firstName || rawRes.FirstName;
        const surName = rawRes.surName || rawRes.SurName;
        const fullName = rawRes.fullName || rawRes.FullName || '';
        const roles = rawRes.roles || rawRes.Roles || [];
        const empresas = rawRes.empresas || rawRes.Empresas || [];
        const companyPaises = rawRes.companyPaises || rawRes.CompanyPaises || [];
        const permisos = rawRes.permisos || rawRes.Permisos || [];
        // NOTA: El menú ya NO viene en el login, se carga por separado después
        const menu: MenuItem[] = [];
        const menusByRole: RoleMenusLite[] = [];

        if (!token) {
          console.error('❌ ERROR CRÍTICO: Token no encontrado en la respuesta!', {
            availableKeys: Object.keys(rawRes),
            rawRes
          });
          throw new Error('Token de autenticación no encontrado en la respuesta del servidor');
        }

        const companies = empresas ?? [];

        // Determinar empresa y país activos por defecto
        // Prioridad: 1) isDefault=true, 2) primera disponible
        const defaultCompanyPais = companyPaises.find((cp: any) => cp.isDefault) || companyPaises[0];
        const activeCompanyId = defaultCompanyPais?.companyId;
        const activePaisId = defaultCompanyPais?.paisId;
        const activeCompany = defaultCompanyPais?.companyName || (companies[0] ?? undefined);

        const session: AuthSession = {
          accessToken: token,
          refreshToken: (res as any).refreshToken || (res as any).RefreshToken, // El backend no retorna refreshToken por ahora
          user: {
            id: userId ? String(userId) : '',
            username: username,
            firstName: firstName ?? undefined,
            surName: surName ?? undefined,
            fullName: fullName,
            roles: roles,
            permisos: permisos,
          },
          companies,
          activeCompany,
          companyPaises: companyPaises.length > 0 ? companyPaises : undefined,
          activeCompanyId,
          activePaisId,
          menu: menu,
          menusByRole: menusByRole
        };

        console.log('💾 Guardando sesión en storage...', {
          hasMenu: (session.menu?.length ?? 0) > 0,
          hasRoles: (session.user.roles?.length ?? 0) > 0
        });

        return session;
      }),
      tap(session => {
        console.log('💾 Guardando sesión en storage...', {
          hasToken: !!session.accessToken,
          tokenLength: session.accessToken?.length ?? 0,
          hasMenu: (session.menu?.length ?? 0) > 0,
          hasRoles: (session.user.roles?.length ?? 0) > 0,
          storageType: remember ? 'localStorage' : 'sessionStorage'
        });

        this.storage.save(session, remember);

        // Verificar que se guardó correctamente
        const saved = this.storage.get();
        console.log('✅ Sesión guardada. Verificación:', {
          saved: !!saved,
          hasToken: !!saved?.accessToken,
          tokenMatches: saved?.accessToken === session.accessToken
        });
      }),
      catchError(error => {
        console.error('❌ Error en el proceso de login:', error);
        // Mejorar mensaje de error según el tipo
        let errorMessage = 'Error al procesar el login';
        if (error.message?.includes('desencriptar')) {
          errorMessage = 'Error al desencriptar la respuesta del servidor. Intenta de nuevo.';
        } else if (error.error?.message) {
          errorMessage = error.error.message;
        } else if (error.message) {
          errorMessage = error.message;
        }
        return throwError(() => new Error(errorMessage));
      })
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

  // Carga el menú del usuario autenticado (separado del login, datos encriptados)
  loadMenu(companyId?: number) {
    console.log('📋 Cargando menú del usuario...', { companyId });

    const params = companyId ? new HttpParams().set('companyId', companyId.toString()) : undefined;

    return this.http.get(`${this.baseUrl}/menu`, {
      params,
      responseType: 'text' // Recibimos texto plano encriptado
    }).pipe(
      switchMap(encryptedResponse => {
        console.log('🔐 Menú encriptado recibido, iniciando desencriptación...');
        return from(this.encryption.decryptFromBackend<{ menu: MenuItem[]; menusByRole: RoleMenusLite[] }>(encryptedResponse));
      }),
      tap(menuData => {
        console.log('✅ Menú desencriptado correctamente', {
          menuItems: menuData.menu?.length ?? 0,
          menusByRole: menuData.menusByRole?.length ?? 0
        });

        // Actualizar la sesión con el menú cargado
        const currentSession = this.storage.get();
        if (currentSession) {
          const updatedSession: AuthSession = {
            ...currentSession,
            menu: menuData.menu ?? [],
            menusByRole: menuData.menusByRole ?? []
          };

          // Guardar en el mismo tipo de storage (localStorage o sessionStorage)
          const persistedInLocal = !!localStorage.getItem('auth_session');
          this.storage.save(updatedSession, persistedInLocal);

          console.log('✅ Menú actualizado en la sesión');
        } else {
          console.warn('⚠️ No hay sesión activa para actualizar el menú');
        }
      }),
      map(menuData => ({
        menu: menuData.menu ?? [],
        menusByRole: menuData.menusByRole ?? []
      })),
      catchError(error => {
        console.error('❌ Error al cargar el menú:', error);
        return throwError(() => new Error('Error al cargar el menú del usuario'));
      })
    );
  }

  // Recarga el menú dinámico desde el backend (método legacy, mantenido para compatibilidad)
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

  /**
   * Consulta el estado de un correo en la cola
   */
  getEmailStatus(emailQueueId: number): Observable<EmailQueueStatus> {
    return this.http.get<string>(
      `${this.baseUrl}/email-status/${emailQueueId}`,
      { responseType: 'text' as 'json' }
    ).pipe(
      switchMap(encryptedResponse => {
        return from(this.encryption.decryptFromBackend<EmailQueueStatus>(encryptedResponse));
      }),
      catchError(error => {
        console.error('Error al consultar estado del correo:', error);
        return throwError(() => error);
      })
    );
  }
}

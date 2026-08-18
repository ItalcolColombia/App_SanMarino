import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AuthSession, MenuItem } from './auth.models';
import { resolverEmpresaActiva } from './funciones/resolver-empresa-activa.funcion';
import { CacheConsultasService } from '../../shared/offline/cache-consultas.service';
import type { IdentidadParticion } from '../../shared/offline/models/offline.model';

const KEY = 'auth_session';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly subject = new BehaviorSubject<AuthSession | null>(this.read());
  readonly session$ = this.subject.asObservable();

  // Caché de consulta offline (F2). No introduce ciclo de DI: `CacheConsultasService` no depende
  // de nadie — recibe la identidad por parámetro justamente para poder vivir en el nivel más bajo.
  private readonly cacheOffline = inject(CacheConsultasService);

  /**
   * Identidad de partición de la sesión actual: quién es el dueño de lo que hay en el dispositivo.
   *
   * Es pública porque la usan dos consumidores con el mismo criterio y una sola derivación posible:
   * la purga de esta clase y el push de `SyncService`, que necesita saber **de quién** es la cola
   * antes de mandarla con el token de la sesión activa. Duplicar el `?? ` en cada llamador es cómo
   * nacen las claves que colapsan.
   */
  identidadActual(): IdentidadParticion {
    const s = this.get();
    return {
      userId: s?.user?.id ?? s?.user?.userId ?? null,
      companyId: s?.activeCompanyId ?? null,
      paisId: s?.activePaisId ?? null
    };
  }

  // Guarda en localStorage si remember=true; caso contrario, en sessionStorage
  save(session: AuthSession, remember = false) {
    try {
      if (!session || !session.accessToken) {
        console.error('❌ TokenStorageService.save() - Intento de guardar sesión sin token!', {
          hasSession: !!session,
          hasAccessToken: !!session?.accessToken
        });
        throw new Error('No se puede guardar una sesión sin token de acceso');
      }

      const store = remember ? localStorage : sessionStorage;
      const sessionJson = JSON.stringify(session);

      store.setItem(KEY, sessionJson);

      // Limpiar el otro storage
      (remember ? sessionStorage : localStorage).removeItem(KEY);

      // Actualizar el BehaviorSubject para que los observables se actualicen
      this.subject.next(session);

      // Verificar que se guardó correctamente
      const saved = this.read();
      if (!saved || !saved.accessToken) {
        console.error('❌ Error: La sesión no se guardó correctamente o no tiene token');
        throw new Error('Error al guardar la sesión: token no encontrado después de guardar');
      }

    } catch (error) {
      console.error('❌ Error al guardar sesión:', error);
      throw error;
    }
  }

  get(): AuthSession | null {
    return this.subject.value ?? this.read();
  }

  getToken(): string | null {
    return this.get()?.accessToken ?? null;
  }

  getMenu(): MenuItem[] {
    return this.get()?.menu ?? [];
  }

  getMenusByRole() {
    return this.get()?.menusByRole ?? [];
  }

  // Actualiza sólo el menú en el storage manteniendo el tipo de persistencia
  updateMenu(menu: MenuItem[]) {
    const current = this.get();
    if (!current) return;
    const updated = { ...current, menu };
    const persistedInLocal = !!localStorage.getItem(KEY);
    this.save(updated, persistedInLocal);
  }

  /**
   * Cambia la empresa activa moviendo **todos** los campos que la definen a la vez:
   * nombre, id, país y logo.
   *
   * Antes sólo escribía el nombre. Como el interceptor manda `X-Active-Company` (nombre) y
   * `X-Active-Company-Id` (id), y el backend **prefiere el id**, cambiar de empresa dejaba a
   * la UI en una empresa y al backend en otra. Ver `resolverEmpresaActiva` para el detalle.
   *
   * Fail-closed: si el nombre no corresponde a ninguna empresa-país disponible, no cambia nada
   * y devuelve `false`. Es preferible no cambiar de empresa a quedar en un estado híbrido.
   *
   * @returns `true` si la empresa activa efectivamente cambió.
   */
  setActiveCompany(name: string): boolean {
    const current = this.get();
    if (!current) return false;

    const empresa = resolverEmpresaActiva(current.companyPaises, name);
    if (!empresa) {
      console.warn(`No se pudo resolver la empresa "${name}" entre las disponibles del usuario.`);
      return false;
    }

    // Se purga la caché offline de la empresa que se está DEJANDO, antes de cambiar. El dato de
    // una empresa no tiene por qué seguir en el dispositivo cuando el usuario pasó a otra, y
    // esperar al TTL de 16 h sería dejarlo ahí toda la jornada.
    void this.cacheOffline.purgarParticionDe(this.identidadActual());

    const updated = { ...current, ...empresa };
    const persistedInLocal = !!localStorage.getItem(KEY);
    this.save(updated, persistedInLocal);
    return true;
  }

  updateActiveCompanyLogo(logoDataUrl: string | null) {
    const current = this.get();
    if (!current) return;
    const updated = { ...current, activeCompanyLogoDataUrl: logoDataUrl };
    const persistedInLocal = !!localStorage.getItem(KEY);
    this.save(updated, persistedInLocal);
  }

  // Actualiza solo los datos del usuario en el storage manteniendo el tipo de persistencia
  updateUserData(userData: { firstName?: string; surName?: string }) {
    
    const current = this.get();
    if (!current) {
      
      return;
    }

    const updatedUser = {
      ...current.user,
      firstName: userData.firstName ?? current.user.firstName,
      surName: userData.surName ?? current.user.surName,
      fullName: `${userData.firstName ?? current.user.firstName} ${userData.surName ?? current.user.surName}`.trim()
    };

    const updated = {
      ...current,
      user: updatedUser
    };

    
    const persistedInLocal = !!localStorage.getItem(KEY);
    this.save(updated, persistedInLocal);
  }

  /**
   * Cierra la sesión de QUIEN está usando el equipo: se purga **su** partición de la caché y nada más.
   *
   * Antes acá iba `purgarTodo()`, o sea que el logout de uno borraba lo cacheado por **todos** los
   * que hubieran entrado alguna vez en esa tablet. Y lo que se destruye no es un archivo temporal:
   * es el **alistamiento** de los otros —instalar la app y entrar una vez con señal—, que en campo
   * cuesta un viaje a la oficina con wifi. Que A se vaya no puede costarle eso a B.
   *
   * La purga va **antes** de soltar la sesión, y el límite exacto es `subject.next(null)`, no los
   * `removeItem`: `get()` lee el `BehaviorSubject` primero, así que la identidad sobrevive a vaciar
   * el storage y muere recién ahí. Correrla después **no da error** —`purgarParticionDe` es
   * fail-closed y con identidad vacía no borra nada—: deja la caché intacta, en silencio. Hay un
   * test que lo fija.
   *
   * **El outbox no se toca** (R9): `purgarParticionDe` opera solo sobre el store `consultas`. Una
   * captura de campo no existe en ningún otro lado.
   */
  clear() {
    void this.cacheOffline.purgarParticionDe(this.identidadActual());

    localStorage.removeItem(KEY);
    sessionStorage.removeItem(KEY);
    this.subject.next(null);
  }

  /**
   * Igual que `clear()`, más el `sessionStorage` completo. Es el botón «Cerrar sesión» del sidebar.
   *
   * Sigue siendo un cierre de sesión, no un borrado del equipo: por eso purga la partición propia y
   * no toda la caché. Para lo otro está `borrarDispositivo()`.
   */
  clearAllTemporal() {
    void this.cacheOffline.purgarParticionDe(this.identidadActual());

    try { sessionStorage.clear(); } catch {}
    try { localStorage.removeItem(KEY); } catch {}
    this.subject.next(null);
  }

  /**
   * Deja el equipo como recién instalado: **toda** la caché de consultas, de todas las particiones.
   *
   * Es la acción deliberada de «este dispositivo cambia de manos», y por eso es un método aparte y
   * no un efecto colateral del logout. Hasta acá llegó confundido con él, y esa confusión era el
   * defecto: quien cierra sesión quiere salir, no formatear la tablet de sus compañeros.
   *
   * ⚠️ **La cola de capturas NO se borra**, ni siquiera acá (R9). Nada la borra salvo la confirmación
   * del servidor o una persona, una por una, desde `/diagnostico`. Lo cacheado se vuelve a pedir; una
   * captura encolada no existe en ningún otro lado.
   */
  borrarDispositivo() {
    void this.cacheOffline.purgarTodo();

    try { sessionStorage.clear(); } catch {}
    try { localStorage.removeItem(KEY); } catch {}
    this.subject.next(null);
  }

  private read(): AuthSession | null {
    const raw = localStorage.getItem(KEY) ?? sessionStorage.getItem(KEY);
    try { return raw ? JSON.parse(raw) as AuthSession : null; } catch { return null; }
  }


  // (Opcional) sincroniza múltiples pestañas
  constructor() {
    window.addEventListener('storage', (e) => {
      if (e.key === KEY) this.subject.next(this.read());
    });
  }
}

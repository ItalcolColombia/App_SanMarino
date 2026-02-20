import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class SidebarService {
  // Estado del sidebar: true = visible, false = oculto
  private _isVisible = signal<boolean>(true);

  // Signal de solo lectura para el estado de visibilidad
  isVisible = this._isVisible.asReadonly();

  toggle(): void {
    this._isVisible.update(visible => !visible);
  }

  show(): void {
    this._isVisible.set(true);
  }

  hide(): void {
    this._isVisible.set(false);
  }
}

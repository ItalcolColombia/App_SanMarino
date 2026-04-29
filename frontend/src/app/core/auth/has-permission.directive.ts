import {
  Directive,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  TemplateRef,
  ViewContainerRef,
  inject,
} from '@angular/core';
import { Subscription } from 'rxjs';
import { UserPermissionService } from './user-permission.service';

/**
 * Structural directive that conditionally renders its host element based on
 * the current user's permissions.  Reacts to session changes (e.g. company
 * switch, logout) without requiring a page reload.
 *
 * Usage:
 *   <!-- show only when the user has manage_users -->
 *   <button *appHasPermission="'manage_users'">Gestionar usuarios</button>
 *
 *   <!-- show when the user has at least one of the listed permissions -->
 *   <section *appHasPermission="['view_reports', 'download_reports']">...</section>
 *
 *   <!-- inverse: hide when the user has the permission -->
 *   <p *appHasPermission="'manage_users'; else noAccess">...</p>
 *   <ng-template #noAccess>Solo administradores</ng-template>
 *
 * Import in the host component:
 *   imports: [HasPermissionDirective]
 */
@Directive({
  selector: '[appHasPermission]',
  standalone: true,
})
export class HasPermissionDirective implements OnInit, OnChanges, OnDestroy {
  private readonly vcr = inject(ViewContainerRef);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly permService = inject(UserPermissionService);

  @Input('appHasPermission') permissions: string | string[] = [];

  private sub?: Subscription;

  ngOnInit(): void {
    this.subscribe();
  }

  ngOnChanges(): void {
    // Re-evaluate if the input binding changes at runtime.
    this.sub?.unsubscribe();
    this.subscribe();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private subscribe(): void {
    const keys = Array.isArray(this.permissions)
      ? this.permissions
      : [this.permissions].filter(Boolean);

    this.sub = this.permService.hasAny$(keys).subscribe(allowed => {
      this.vcr.clear();
      if (allowed) {
        this.vcr.createEmbeddedView(this.template);
      }
    });
  }
}

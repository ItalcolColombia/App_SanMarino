/**
 * Empresas — el checkbox «Acceso móvil» NO puede vivir dentro de `formGroupName="visualPermissions"`.
 *
 * El wizard de empresa declaraba `mobileAccess` en la RAÍZ del formulario pero lo bindeaba dentro
 * del grupo `visualPermissions` (que solo tiene dashboard/reports/farms/users). Angular resuelve
 * `formControlName` contra el ControlContainer más cercano, así que buscaba
 * `visualPermissions.mobileAccess` — que no existe.
 *
 * Este spec fija las dos mitades de la regla para que nadie la vuelva a mover de grupo.
 */
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';

/** Réplica de la forma del formulario real: grupo de módulos + `mobileAccess` en la raíz. */
function construirForm(fb: FormBuilder): FormGroup {
  return fb.group({
    visualPermissions: fb.group({ dashboard: [false], reports: [false], farms: [false], users: [false] }),
    mobileAccess: [false]
  });
}

/** Cómo estaba: el control de la raíz bindeado DENTRO del grupo. */
@Component({
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <div formGroupName="visualPermissions">
        <input type="checkbox" formControlName="dashboard" />
        <input type="checkbox" formControlName="mobileAccess" />
      </div>
    </form>`
})
class BindeoDentroDelGrupoComponent {
  form = construirForm(new FormBuilder());
}

/** Cómo queda: el grupo cierra antes y `mobileAccess` cuelga de la raíz. */
@Component({
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <div formGroupName="visualPermissions">
        <input type="checkbox" formControlName="dashboard" />
      </div>
      <input type="checkbox" formControlName="mobileAccess" />
    </form>`
})
class BindeoEnLaRaizComponent {
  form = construirForm(new FormBuilder());
}

describe('Empresas · binding de «Acceso móvil»', () => {
  it('dentro de formGroupName="visualPermissions" revienta al renderizar', () => {
    TestBed.configureTestingModule({ imports: [BindeoDentroDelGrupoComponent] });
    const fixture = TestBed.createComponent(BindeoDentroDelGrupoComponent);
    expect(() => fixture.detectChanges()).toThrowError(/Cannot find control/);
  });

  it('colgado de la raíz renderiza y escribe sobre el control correcto', () => {
    TestBed.configureTestingModule({ imports: [BindeoEnLaRaizComponent] });
    const fixture = TestBed.createComponent(BindeoEnLaRaizComponent);
    expect(() => fixture.detectChanges()).not.toThrow();

    const inputs = fixture.nativeElement.querySelectorAll('input[type="checkbox"]');
    const movil = inputs[inputs.length - 1] as HTMLInputElement;
    movil.checked = true;
    movil.dispatchEvent(new Event('change'));

    const form = fixture.componentInstance.form;
    expect(form.get('mobileAccess')!.value).toBeTrue();
    expect(form.get('visualPermissions.mobileAccess')).toBeNull();
  });
});

# Servicio de Filtrado por País

Este servicio permite verificar el país del usuario y mostrar/ocultar funcionalidades según el país activo.

## Uso

### Servicio `CountryFilterService`

```typescript
import { CountryFilterService } from '@core/services/country/country-filter.service';

constructor(private countryFilter: CountryFilterService) {}

// Verificar si es Ecuador
if (this.countryFilter.isEcuador()) {
  // Mostrar campos específicos de Ecuador
}

// Verificar por ID de país
if (this.countryFilter.isCountry(2)) {
  // Mostrar campos para país con ID 2
}

// Verificar por nombre de país
if (this.countryFilter.isCountryByName('Ecuador')) {
  // Mostrar campos para Ecuador
}
```

### Directiva `*appShowIfEcuador`

Muestra el elemento solo si el usuario es de Ecuador:

```html
<!-- Campo solo para Ecuador -->
<div *appShowIfEcuador>
  <label>Campo específico de Ecuador</label>
  <input type="text" formControlName="campoEcuador" />
</div>
```

Con template else:

```html
<div *appShowIfEcuador; else notEcuador>
  <input type="text" formControlName="campoEcuador" />
</div>
<ng-template #notEcuador>
  <p>Este campo no está disponible para tu país</p>
</ng-template>
```

### Directiva `*appShowIfCountry`

Muestra el elemento solo si el usuario es del país especificado:

```html
<!-- Por ID -->
<div *appShowIfCountry="2">
  Contenido para país con ID 2
</div>

<!-- Por nombre -->
<div *appShowIfCountry="'Ecuador'">
  Contenido para Ecuador
</div>
```

### Pipe `isEcuador`

```html
<div *ngIf="true | isEcuador">
  Contenido solo para Ecuador
</div>

<span>{{ (true | isEcuador) ? 'Ecuador' : 'Otro país' }}</span>
```

## Ejemplo Completo en un Formulario

```typescript
import { Component } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { ShowIfEcuadorDirective } from '@core/directives';

@Component({
  selector: 'app-mi-formulario',
  standalone: true,
  imports: [ShowIfEcuadorDirective, ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <!-- Campos comunes a todos los países -->
      <input formControlName="campoComun" />
      
      <!-- Campo solo para Ecuador -->
      <div *appShowIfEcuador>
        <label>Campo específico de Ecuador (opcional)</label>
        <input type="text" formControlName="campoEcuador" />
      </div>
      
      <!-- Otro campo solo para Ecuador -->
      <div *appShowIfEcuador>
        <label>Otro campo de Ecuador</label>
        <input type="number" formControlName="otroCampoEcuador" />
      </div>
    </form>
  `
})
export class MiFormularioComponent {
  form: FormGroup;
  
  constructor(private fb: FormBuilder) {
    this.form = this.fb.group({
      campoComun: [''],
      campoEcuador: [''], // No es obligatorio, solo se muestra para Ecuador
      otroCampoEcuador: ['']
    });
  }
}
```

## Notas Importantes

1. **Los campos no son obligatorios**: Los campos específicos de Ecuador no deben tener validadores `required` ya que solo se muestran para usuarios de Ecuador.

2. **El servicio se actualiza automáticamente**: Cuando el usuario cambia de país o empresa, las directivas se actualizan automáticamente.

3. **Configuración de Ecuador**: El ID de Ecuador está configurado como `2` en el servicio. Si cambia, actualizar `ECUADOR_ID` en `CountryFilterService`.

4. **Performance**: Las directivas están optimizadas para no crear/destruir vistas innecesariamente.

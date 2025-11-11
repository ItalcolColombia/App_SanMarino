// src/app/features/config/user-management/components/asignar-usuario-granja/asignar-usuario-granja.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faBuilding, faCheck, faTimes, faSave, faTrash,
  faCrown, faStar, faUser, faSearch
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil, forkJoin } from 'rxjs';

import { UserFarmService, UserFarmDto, UserFarmLiteDto } from '../../../../../core/services/user-farm/user-farm.service';
import { FarmService, FarmDto } from '../../../../../core/services/farm/farm.service';

@Component({
  selector: 'app-asignar-usuario-granja',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './asignar-usuario-granja.component.html',
  styleUrls: ['./asignar-usuario-granja.component.scss']
})
export class AsignarUsuarioGranjaComponent implements OnInit, OnDestroy {
  @Input() userId: string = '';
  @Input() userName: string = '';
  @Input() companyId: number = 0;
  @Input() isOpen: boolean = false;

  @Output() close = new EventEmitter<void>();
  @Output() granjasUpdated = new EventEmitter<void>();

  // Iconos
  faBuilding = faBuilding;
  faCheck = faCheck;
  faTimes = faTimes;
  faSave = faSave;
  faTrash = faTrash;
  faCrown = faCrown;
  faStar = faStar;
  faUser = faUser;
  faSearch = faSearch;

  // Estado
  loading = false;
  saving = false;
  searchTerm = '';

  // Datos
  userFarms: UserFarmDto[] = [];
  availableFarms: FarmDto[] = [];
  accessibleFarms: UserFarmLiteDto[] = [];

  // Filtros
  showOnlyAssigned = false;
  showOnlyAvailable = false;

  private destroy$ = new Subject<void>();

  constructor(
    private userFarmService: UserFarmService,
    private farmService: FarmService
  ) {}

  ngOnInit(): void {
    if (this.isOpen) {
      this.loadData();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngOnChanges(): void {
    if (this.isOpen && this.userId) {
      this.loadData();
    }
  }

  loadData(): void {
    if (!this.userId) return;

    this.loading = true;

    forkJoin({
      userFarms: this.userFarmService.getUserFarms(this.userId),
      accessibleFarms: this.userFarmService.getUserAccessibleFarms(this.userId),
      allFarms: this.farmService.getAll()
    })
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.userFarms = data.userFarms.farms;
        this.accessibleFarms = data.accessibleFarms;

        // Filtrar granjas disponibles - mostrar todas las granjas activas
        this.availableFarms = data.allFarms.filter(farm => {
          return farm.status === 'A';
        });

        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading farm data:', error);
        this.loading = false;
      }
    });
  }

  get filteredAvailableFarms(): FarmDto[] {
    let farms = this.availableFarms.filter(farm =>
      !this.userFarms.some(uf => uf.farmId === farm.id)
    );

    if (this.searchTerm) {
      farms = farms.filter(farm =>
        farm.name.toLowerCase().includes(this.searchTerm.toLowerCase())
      );
    }

    return farms;
  }

  get filteredUserFarms(): UserFarmDto[] {
    let farms = this.userFarms;

    if (this.searchTerm) {
      farms = farms.filter(farm =>
        farm.farmName.toLowerCase().includes(this.searchTerm.toLowerCase())
      );
    }

    return farms;
  }

  assignFarm(farm: FarmDto): void {
    this.saving = true;

    const dto = {
      UserId: this.userId,
      FarmId: farm.id,
      IsAdmin: false,
      IsDefault: this.userFarms.length === 0 // Primera granja es default
    };

    console.log('Enviando petición para asignar granja:', dto); // Debug log

    this.userFarmService.createUserFarm(dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (newUserFarm) => {
          console.log('Granja asignada exitosamente:', newUserFarm); // Debug log
          this.userFarms.push(newUserFarm);
          this.saving = false;
          this.granjasUpdated.emit();
        },
        error: (error) => {
          console.error('Error asignando granja:', error); // Debug log
          console.error('Detalles del error:', error.error); // Debug log
          this.saving = false;
        }
      });
  }

  removeFarm(userFarm: UserFarmDto): void {
    if (!confirm(`¿Está seguro de que desea remover la granja "${userFarm.farmName}" del usuario?`)) {
      return;
    }

    this.saving = true;

    this.userFarmService.deleteUserFarm(this.userId, userFarm.farmId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.userFarms = this.userFarms.filter(uf => uf.farmId !== userFarm.farmId);
          this.saving = false;
          this.granjasUpdated.emit();
        },
        error: (error) => {
          console.error('Error removing farm:', error);
          this.saving = false;
        }
      });
  }

  toggleAdmin(userFarm: UserFarmDto): void {
    const dto = {
      isAdmin: !userFarm.isAdmin,
      isDefault: userFarm.isDefault
    };

    this.saving = true;

    this.userFarmService.updateUserFarm(this.userId, userFarm.farmId, dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (updatedUserFarm) => {
          const index = this.userFarms.findIndex(uf => uf.farmId === userFarm.farmId);
          if (index !== -1) {
            this.userFarms[index] = updatedUserFarm;
          }
          this.saving = false;
          this.granjasUpdated.emit();
        },
        error: (error) => {
          console.error('Error updating farm permissions:', error);
          this.saving = false;
        }
      });
  }

  toggleDefault(userFarm: UserFarmDto): void {
    if (userFarm.isDefault) return; // Ya es default

    // Primero quitar el default actual
    const currentDefault = this.userFarms.find(uf => uf.isDefault);
    if (currentDefault) {
      const removeDefaultDto = {
        isAdmin: currentDefault.isAdmin,
        isDefault: false
      };

      this.userFarmService.updateUserFarm(this.userId, currentDefault.farmId, removeDefaultDto)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            // Ahora establecer el nuevo default
            this.setNewDefault(userFarm);
          },
          error: (error) => {
            console.error('Error removing current default:', error);
            this.saving = false;
          }
        });
    } else {
      this.setNewDefault(userFarm);
    }
  }

  private setNewDefault(userFarm: UserFarmDto): void {
    const dto = {
      isAdmin: userFarm.isAdmin,
      isDefault: true
    };

    this.userFarmService.updateUserFarm(this.userId, userFarm.farmId, dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (updatedUserFarm) => {
          const index = this.userFarms.findIndex(uf => uf.farmId === userFarm.farmId);
          if (index !== -1) {
            this.userFarms[index] = updatedUserFarm;
          }
          this.saving = false;
          this.granjasUpdated.emit();
        },
        error: (error) => {
          console.error('Error setting new default:', error);
          this.saving = false;
        }
      });
  }

  closeModal(): void {
    this.close.emit();
  }

  clearSearch(): void {
    this.searchTerm = '';
  }
}

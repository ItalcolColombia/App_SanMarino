import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { DbStudioMainComponent } from './pages/db-studio-main/db-studio-main.component';

const routes: Routes = [
  { path: '', component: DbStudioMainComponent, data: { title: 'DB Studio' } },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class DbStudioRoutingModule {}

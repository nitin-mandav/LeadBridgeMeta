import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { ShellComponent } from './layout/shell.component';
import { LoginComponent } from './pages/login/login.component';
import { RegisterComponent } from './pages/register/register.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ConnectionsComponent } from './pages/connections/connections.component';
import { MappingsComponent } from './pages/mappings/mappings.component';
import { CallbackComponent } from './pages/callback/callback.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'api/meta/callback', component: CallbackComponent },
  { path: 'api/ghl/callback', component: CallbackComponent },
  { path: 'meta/callback', component: CallbackComponent },
  { path: 'ghl/callback', component: CallbackComponent },
  { path: 'callback', component: CallbackComponent },
  { path: 'oauth/callback', component: CallbackComponent },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'connections', component: ConnectionsComponent },
      { path: 'mappings', component: MappingsComponent },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];


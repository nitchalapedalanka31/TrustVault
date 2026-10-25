import { Routes } from '@angular/router';
import { LoginComponent } from './pages/login/login.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { MyDocumentsComponent } from './pages/my-documents/my-documents.component';
import { SharedWithMeComponent } from './pages/shared-with-me/shared-with-me.component';
import { DocumentDetailComponent } from './pages/document-detail/document-detail.component';
import { AdminDashboardComponent } from './pages/admin-dashboard/admin-dashboard.component';
import { AccessDeniedComponent } from './pages/access-denied/access-denied.component';
import { NotFoundComponent } from './pages/not-found/not-found.component';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'my-documents', component: MyDocumentsComponent, canActivate: [authGuard] },
  { path: 'shared-with-me', component: SharedWithMeComponent, canActivate: [authGuard] },
  { path: 'documents/:id', component: DocumentDetailComponent, canActivate: [authGuard] },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [adminGuard] },
  { path: 'access-denied', component: AccessDeniedComponent },
  { path: 'not-found', component: NotFoundComponent },
  { path: '**', redirectTo: 'not-found' }
];

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { map } from 'rxjs';

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    if (authService.isAdmin()) return true;
    router.navigate(['/access-denied']);
    return false;
  }

  return authService.initAuth().pipe(
    map(user => {
      if (user && authService.isAdmin()) return true;
      router.navigate(['/access-denied']);
      return false;
    })
  );
};

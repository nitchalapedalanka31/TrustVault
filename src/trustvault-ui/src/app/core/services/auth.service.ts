import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of } from 'rxjs';
import { UserProfile, UserRole } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly baseUrl = 'http://localhost:5000/api/auth';
  
  public currentUser = signal<UserProfile | null>(null);
  public isAuthenticated = computed(() => this.currentUser() !== null);
  public isAdmin = computed(() => this.currentUser()?.role === UserRole.Admin);
  public isOwner = computed(() => this.currentUser()?.role === UserRole.Owner);
  public isViewer = computed(() => this.currentUser()?.role === UserRole.Viewer);

  constructor(private http: HttpClient) {}

  public initAuth(): Observable<UserProfile | null> {
    return this.http.get<UserProfile>(`${this.baseUrl}/me`).pipe(
      tap(user => this.currentUser.set(user)),
      catchError(() => {
        this.currentUser.set(null);
        return of(null);
      })
    );
  }

  public login(credentials: { email: string; password: string }): Observable<UserProfile> {
    return this.http.post<UserProfile>(`${this.baseUrl}/login`, credentials).pipe(
      tap(user => this.currentUser.set(user))
    );
  }

  public logout(): Observable<any> {
    return this.http.post(`${this.baseUrl}/logout`, {}).pipe(
      tap(() => this.currentUser.set(null)),
      catchError(() => {
        this.currentUser.set(null);
        return of(null);
      })
    );
  }

  public getCsrf(): Observable<any> {
    return this.http.get(`${this.baseUrl}/csrf`);
  }
}

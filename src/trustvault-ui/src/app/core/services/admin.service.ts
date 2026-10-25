import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AdminStats, AuditEventItem, AuditVerificationResult, PagedResult, UserProfile, UserRole } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly baseUrl = 'http://localhost:5000/api/admin';

  constructor(private http: HttpClient) {}

  public getStats(): Observable<AdminStats> {
    return this.http.get<AdminStats>(`${this.baseUrl}/stats`);
  }

  public getUsers(): Observable<UserProfile[]> {
    return this.http.get<UserProfile[]>(`${this.baseUrl}/users`);
  }

  public createUser(payload: { email: string; password: string; fullName: string; role: UserRole }): Observable<UserProfile> {
    return this.http.post<UserProfile>(`${this.baseUrl}/users`, payload);
  }

  public updateUserStatus(id: string, isActive: boolean): Observable<any> {
    return this.http.put(`${this.baseUrl}/users/${id}/status`, { isActive });
  }

  public getAuditLogs(filter: { actorId?: string; action?: string; resourceId?: string; result?: string; pageNumber?: number; pageSize?: number } = {}): Observable<PagedResult<AuditEventItem>> {
    let params = new HttpParams();
    if (filter.actorId) params = params.set('actorId', filter.actorId);
    if (filter.action) params = params.set('action', filter.action);
    if (filter.resourceId) params = params.set('resourceId', filter.resourceId);
    if (filter.result) params = params.set('result', filter.result);
    if (filter.pageNumber) params = params.set('pageNumber', filter.pageNumber.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PagedResult<AuditEventItem>>(`${this.baseUrl}/audit`, { params });
  }

  public verifyAuditIntegrity(): Observable<AuditVerificationResult> {
    return this.http.post<AuditVerificationResult>(`${this.baseUrl}/audit/verify`, {});
  }
}

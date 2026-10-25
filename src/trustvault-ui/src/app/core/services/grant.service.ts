import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DocumentGrant, DocumentPermission, RecipientSuggestion } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class GrantService {
  private readonly baseUrl = 'http://localhost:5000/api/documents';

  constructor(private http: HttpClient) {}

  public getGrants(documentId: string): Observable<DocumentGrant[]> {
    return this.http.get<DocumentGrant[]>(`${this.baseUrl}/${documentId}/grants`);
  }

  public createGrant(documentId: string, payload: { userEmail: string; permission: DocumentPermission }): Observable<DocumentGrant> {
    return this.http.post<DocumentGrant>(`${this.baseUrl}/${documentId}/grants`, payload);
  }

  public revokeGrant(documentId: string, grantId: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/${documentId}/grants/${grantId}`);
  }

  public getSuggestions(documentId: string): Observable<RecipientSuggestion[]> {
    return this.http.get<RecipientSuggestion[]>(`${this.baseUrl}/${documentId}/grants/suggestions`);
  }
}

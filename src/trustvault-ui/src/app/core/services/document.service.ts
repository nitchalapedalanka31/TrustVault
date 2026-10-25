import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DocumentDetail, DocumentItem, DocumentVersion, PagedResult } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private readonly baseUrl = 'http://localhost:5000/api/documents';

  constructor(private http: HttpClient) {}

  public getDocuments(filter: { query?: string; category?: string; tag?: string; pageNumber?: number; pageSize?: number } = {}): Observable<PagedResult<DocumentItem>> {
    let params = new HttpParams();
    if (filter.query) params = params.set('query', filter.query);
    if (filter.category) params = params.set('category', filter.category);
    if (filter.tag) params = params.set('tag', filter.tag);
    if (filter.pageNumber) params = params.set('pageNumber', filter.pageNumber.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PagedResult<DocumentItem>>(this.baseUrl, { params });
  }

  public getDocument(id: string): Observable<DocumentDetail> {
    return this.http.get<DocumentDetail>(`${this.baseUrl}/${id}`);
  }

  public uploadDocument(formData: FormData): Observable<DocumentItem> {
    return this.http.post<DocumentItem>(this.baseUrl, formData);
  }

  public uploadNewVersion(id: string, file: File): Observable<DocumentVersion> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<DocumentVersion>(`${this.baseUrl}/${id}/versions`, formData);
  }

  public updateMetadata(id: string, data: { title: string; description?: string; category: string; tags: string }): Observable<any> {
    return this.http.put(`${this.baseUrl}/${id}`, data);
  }

  public deleteDocument(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }

  public restoreDocument(id: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/${id}/restore`, {});
  }

  public downloadDocument(id: string, versionNumber?: number): Observable<Blob> {
    let params = new HttpParams();
    if (versionNumber) params = params.set('versionNumber', versionNumber.toString());

    return this.http.get(`${this.baseUrl}/${id}/download`, {
      params,
      responseType: 'blob'
    });
  }
}

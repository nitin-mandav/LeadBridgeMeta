import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FieldMapping, FieldMappingRequest } from '../models/mapping.models';

@Injectable({ providedIn: 'root' })
export class MappingsService {
  private readonly base = `${environment.apiBaseUrl}/mappings`;

  constructor(private http: HttpClient) {}

  setFormGhlConnection(formId: string, ghlConnectionId: string): Observable<void> {
    return this.http.put<void>(`${this.base}/forms/${formId}/ghl-connection`, { ghlConnectionId });
  }

  getFieldMappings(formId: string | null): Observable<FieldMapping[]> {
    const query = formId ? `?formId=${formId}` : '';
    return this.http.get<FieldMapping[]>(`${this.base}/fields${query}`);
  }

  upsertFieldMapping(request: FieldMappingRequest): Observable<FieldMapping> {
    return this.http.post<FieldMapping>(`${this.base}/fields`, request);
  }

  getFormFields(formId: string): Observable<MetaFormField[]> {
    return this.http.get<MetaFormField[]>(`${this.base}/forms/${formId}/fields`);
  }

  deleteFieldMapping(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/fields/${id}`);
  }
}

export interface MetaFormField {
  key: string;
  label: string;
  type?: string;
}

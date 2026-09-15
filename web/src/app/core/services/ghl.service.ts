import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GhlConnection } from '../models/ghl.models';

@Injectable({ providedIn: 'root' })
export class GhlService {
  private readonly base = `${environment.apiBaseUrl}/ghl`;

  constructor(private http: HttpClient) {}

  getConnectUrl(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.base}/connect-url`);
  }

  handleCallback(code: string, state: string): Observable<{ success: boolean; locationId?: string }> {
    return this.http.get<{ success: boolean; locationId?: string }>(`${this.base}/callback`, {
      params: { code, state, json: 'true' },
    });
  }

  getConnections(): Observable<GhlConnection[]> {
    return this.http.get<GhlConnection[]>(`${this.base}/connections`);
  }
}

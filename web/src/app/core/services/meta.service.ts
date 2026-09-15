import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MetaConnection } from '../models/meta.models';

@Injectable({ providedIn: 'root' })
export class MetaService {
  private readonly base = `${environment.apiBaseUrl}/meta`;

  constructor(private http: HttpClient) {}

  getConnectUrl(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.base}/connect-url`);
  }

  handleCallback(code: string, state: string): Observable<{ success: boolean; userName?: string }> {
    return this.http.get<{ success: boolean; userName?: string }>(`${this.base}/callback`, {
      params: { code, state, json: 'true' },
    });
  }

  getConnections(): Observable<MetaConnection[]> {
    return this.http.get<MetaConnection[]>(`${this.base}/connections`);
  }

  subscribePage(pageId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/pages/${pageId}/subscribe`, {});
  }
}

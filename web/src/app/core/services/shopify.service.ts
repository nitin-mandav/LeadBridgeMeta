import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ShopifyConnection, ShopifyFieldOption } from '../models/shopify.models';

@Injectable({ providedIn: 'root' })
export class ShopifyService {
  private readonly base = `${environment.apiBaseUrl}/shopify`;

  constructor(private http: HttpClient) {}

  getConnectUrl(shop: string): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.base}/connect-url`, {
      params: { shop },
    });
  }

  handleCallback(code: string, shop: string, state?: string | null): Observable<{ success: boolean; shopDomain?: string }> {
    const params: Record<string, string> = { code, shop, json: 'true' };
    if (state) {
      params['state'] = state;
    }
    return this.http.get<{ success: boolean; shopDomain?: string }>(`${this.base}/callback`, { params });
  }

  getConnections(): Observable<ShopifyConnection[]> {
    return this.http.get<ShopifyConnection[]>(`${this.base}/connections`);
  }

  getFields(connectionId?: string): Observable<ShopifyFieldOption[]> {
    const params: Record<string, string> = {};
    if (connectionId) {
      params['connectionId'] = connectionId;
    }
    return this.http.get<ShopifyFieldOption[]>(`${this.base}/fields`, { params });
  }

  getConnectionFields(connectionId: string): Observable<ShopifyFieldOption[]> {
    return this.http.get<ShopifyFieldOption[]>(`${this.base}/connections/${connectionId}/fields`);
  }

  disconnect(connectionId: string): Observable<{ success: boolean; message?: string }> {
    return this.http.delete<{ success: boolean; message?: string }>(`${this.base}/connections/${connectionId}`);
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EmailConfigStatus, EmailConnection } from '../models/email.models';

@Injectable({ providedIn: 'root' })
export class EmailService {
  private readonly base = `${environment.apiBaseUrl}/email`;

  constructor(private http: HttpClient) {}

  getConnections(): Observable<EmailConnection[]> {
    return this.http.get<EmailConnection[]>(`${this.base}/connections`);
  }

  addConnection(email: string): Observable<EmailConnection> {
    return this.http.post<EmailConnection>(`${this.base}/connections`, { email });
  }

  disconnect(id: string): Observable<{ success: boolean; message?: string }> {
    return this.http.delete<{ success: boolean; message?: string }>(`${this.base}/connections/${id}`);
  }

  toggle(id: string, isActive: boolean): Observable<{ success: boolean }> {
    return this.http.patch<{ success: boolean }>(`${this.base}/connections/${id}/toggle`, { isActive });
  }

  sendTestEmail(email: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(`${this.base}/test`, { email });
  }

  getStatus(): Observable<EmailConfigStatus> {
    return this.http.get<EmailConfigStatus>(`${this.base}/status`);
  }
}

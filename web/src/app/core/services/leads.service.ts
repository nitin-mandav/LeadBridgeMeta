import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LeadEvent, LeadEventStatus } from '../models/lead.models';

@Injectable({ providedIn: 'root' })
export class LeadsService {
  private readonly base = `${environment.apiBaseUrl}/leads`;

  constructor(private http: HttpClient) {}

  getLeads(status?: LeadEventStatus): Observable<LeadEvent[]> {
    const query = status !== undefined ? `?status=${status}` : '';
    return this.http.get<LeadEvent[]>(`${this.base}${query}`);
  }

  retry(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/retry`, {});
  }
}

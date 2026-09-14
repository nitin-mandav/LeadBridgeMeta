export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  companyName: string;
  email: string;
  password: string;
  displayName: string;
}

export interface AuthResponse {
  accessToken: string;
  email: string;
  displayName: string;
  tenantId: string;
}

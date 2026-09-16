export interface GhlConnection {
  id: string;
  locationId: string;
  locationName: string;
  accessTokenExpiresAtUtc: string;
}

export interface GhlFieldOption {
  key: string;
  label: string;
  dataType?: string;
  isStandard: boolean;
  category?: string;
}


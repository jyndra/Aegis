export interface HandshakePayload {
  componentId: string;
  timestamp: number;
  hmacSignature: string;
}

export interface HandshakeResponse {
  token: string;
  expiresInSeconds: number;
  nonce: string;
  currentState: string;
  isLocked: boolean;
}

export interface EvaluationPayload {
  url: string;
  domain?: string;
  path?: string;
  query?: string;
  title?: string;
  component: string;
  timestamp: string;
}

export interface EvaluationResponse {
  decision: 'Allow' | 'Block' | 'Redirect' | 'Degraded';
  reason: string;
  severity: string;
  action: string;
  componentState: string;
}

export interface AegisStorageState {
  token?: string;
  tokenExpiresAt?: number;
  serviceStatus?: 'Healthy' | 'Degraded';
  lastCheckedAt?: number;
}

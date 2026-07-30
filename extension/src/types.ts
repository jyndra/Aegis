export interface HandshakePayload {
  componentId: string;
  timestamp: number;
  hmacSignature: string;
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
}

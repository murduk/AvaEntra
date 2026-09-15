export type Tenant = {
  id: string;
  name: string;
  domain: string;
};

export type GroupRef = { id: string; displayName: string };

export type RoleRef = {
  id: string;
  appRoleId: string;
  applicationId: string;
  role?: string;
  app?: string;
};

export type User = {
  id: string;
  userPrincipalName: string;
  displayName: string;
  givenName?: string | null;
  surname?: string | null;
  mail?: string | null;
  enabled: boolean;
  createdAt: string;
  groups: GroupRef[];
  roles?: RoleRef[] | null;
};

export type Group = {
  id: string;
  displayName: string;
  description?: string | null;
  createdAt: string;
  memberCount: number;
  members?: { id: string; displayName: string; userPrincipalName: string }[] | null;
};

export type Redirect = { id: string; applicationId: string; uri: string; type: string };
export type Secret = { id: string; displayName: string; hint: string; createdAt: string; expiresAt?: string | null };
export type ApiScope = { id: string; applicationId: string; value: string; displayName: string; description?: string | null };
export type AppRole = {
  id: string;
  applicationId: string;
  value: string;
  displayName: string;
  description?: string | null;
  allowedUser: boolean;
  allowedApplication: boolean;
};

export type Application = {
  id: string;
  clientId: string;
  displayName: string;
  kind: string;
  identifierUri?: string | null;
  isPublicClient: boolean;
  requirePkce: boolean;
  allowClientCredentials: boolean;
  allowOnBehalfOf: boolean;
  createdAt: string;
  redirectCount?: number;
  secretCount?: number;
  redirects?: Redirect[];
  secrets?: Secret[];
  scopes?: ApiScope[];
  roles?: AppRole[];
  assignments?: {
    id: string;
    userId: string;
    user?: string;
    upn?: string;
    appRoleId: string;
    role?: string;
  }[];
};

export type Overview = {
  tenant: Tenant;
  origin: string;
  authority: string;
  issuer: string;
  discovery: string;
  authorize: string;
  token: string;
  jwks: string;
  graph: string;
  allowPasswordlessDevLogin: boolean;
  counts: { users: number; groups: number; applications: number; logs: number };
  seed: {
    spaClientId: string;
    apiClientId: string;
    apiIdentifier: string;
    backendClientId: string;
    backendSecret: string;
    sampleScope: string;
    defaultPassword: string;
  };
  msalBrowser: { auth: { clientId: string; authority: string; knownAuthorities: string[]; redirectUri: string } };
  identityWeb: { instance: string; tenantId: string; clientId: string; audience: string };
};

export type SignInLog = {
  id: string;
  at: string;
  grantType: string;
  success: boolean;
  error?: string | null;
  scopes?: string | null;
  audience?: string | null;
  user?: string | null;
  client?: string | null;
};

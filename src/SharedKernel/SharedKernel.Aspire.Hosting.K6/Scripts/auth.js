import http from 'k6/http';

function requiredEnv(name) {
  const value = __ENV[name];

  if (!value) {
    throw new Error(`${name} is required.`);
  }

  return value;
}

/**
 * Requests an access token with the OAuth 2.0 client-credentials grant. Call it from setup() and pass the result
 * to the default function. The token endpoint, client id, and client secret come from SK_K6_TOKEN_URL,
 * SK_K6_CLIENT_ID, and SK_K6_CLIENT_SECRET. When SK_K6_TOKEN_HOST is set, the request carries it as the Host header
 * so the token's issuer matches the issuer that services reach through localhost. The token and the secret are
 * never logged.
 */
export function getAccessToken() {
  const headers = __ENV.SK_K6_TOKEN_HOST ? { Host: __ENV.SK_K6_TOKEN_HOST } : {};
  const response = http.post(
    requiredEnv('SK_K6_TOKEN_URL'),
    {
      grant_type: 'client_credentials',
      client_id: requiredEnv('SK_K6_CLIENT_ID'),
      client_secret: requiredEnv('SK_K6_CLIENT_SECRET'),
    },
    { headers, tags: { name: 'token' }, responseType: 'text' },
  );

  if (response.status !== 200) {
    throw new Error(`The token request failed with status ${response.status}.`);
  }

  const token = response.json('access_token');

  if (!token) {
    throw new Error('The token response has no access_token.');
  }

  return token;
}

/** Returns the request headers that carry the bearer token. */
export function bearerHeaders(token, extraHeaders = {}) {
  return Object.assign({ Authorization: `Bearer ${token}` }, extraHeaders);
}

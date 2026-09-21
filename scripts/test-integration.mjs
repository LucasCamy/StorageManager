#!/usr/bin/env node
/**
 * HTTP integration checks for the first StorageManager increment.
 *
 * Run only against a dedicated, empty test database. This script provisions that
 * database once and leaves its data in place for inspection. It NEVER resets,
 * truncates, or connects directly to a database. A second run must use a newly
 * prepared test database; a provisioned API is rejected before any mutation.
 *
 * PowerShell:
 *   $env:API_BASE_URL = 'http://localhost:5080'
 *   $env:TEST_SETUP_TOKEN = '<the test API Setup__Token>'
 *   node scripts/test-integration.mjs
 *
 * Requires Node.js 24+ (fetch and Headers.getSetCookie are built in).
 */

import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

const HELP = `Usage: node scripts/test-integration.mjs

Required:
  TEST_SETUP_TOKEN  Setup__Token configured on the dedicated test API.

Optional:
  API_BASE_URL     Test API origin, default http://localhost:5080.
                   An origin ending in /api/v1 is also accepted.

WARNING: Creates users, locations, products and movements in an empty database.
The script aborts if /api/v1/setup/status does not report required: true.
It never deletes or resets data. Prepare a new dedicated database before reruns.
`;

let passed = 0;
const createdMovementIds = new Set();
const suppliedBase = process.env.API_BASE_URL ?? 'http://localhost:5080';
const apiUrl = new URL(suppliedBase.endsWith('/') ? suppliedBase : `${suppliedBase}/`);
apiUrl.pathname = `${apiUrl.pathname.replace(/\/$/, '').replace(/\/api\/v1$/, '')}/api/v1/`;
apiUrl.search = '';
apiUrl.hash = '';

function summarize(result) {
  return `HTTP ${result.status}${result.body?.code ? ` (${result.body.code})` : ''}`;
}

function expectStatus(result, expected, context) {
  const statuses = Array.isArray(expected) ? expected : [expected];
  assert.ok(statuses.includes(result.status), `${context}: expected ${statuses.join('/')}, got ${summarize(result)}`);
  return result.body;
}

function expectClientError(result, context) {
  assert.ok(result.status >= 400 && result.status < 500, `${context}: expected 4xx, got ${summarize(result)}`);
  return result;
}

function resource(body, context) {
  assert.ok(body && typeof body.id === 'string', `${context}: response must contain an id`);
  assert.match(body.id, /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, `${context}: id must be a GUID`);
  return body;
}

function quantity(value, expected, context) {
  assert.equal(typeof value, 'string', `${context}: quantities must be JSON strings`);
  assert.match(value, /^-?\d+(?:\.\d+)?$/, `${context}: use invariant decimal notation`);
  // Test quantities are small, exactly representable values; production clients
  // must retain decimal strings instead of calculating money/stock with Number.
  assert.equal(Number(value), expected, context);
}

class ApiSession {
  cookies = new Map();
  csrfToken = null;

  async request(path, { method = 'GET', body, csrf = true, token, key } = {}) {
    const headers = new Headers({ Accept: 'application/json' });
    if (body !== undefined) headers.set('Content-Type', 'application/json');
    if (this.cookies.size) headers.set('Cookie', [...this.cookies].map(([name, value]) => `${name}=${value}`).join('; '));
    if (csrf && method !== 'GET' && method !== 'HEAD' && (token ?? this.csrfToken)) {
      headers.set('X-CSRF-TOKEN', token ?? this.csrfToken);
    }
    if (key) headers.set('Idempotency-Key', key);

    const response = await fetch(new URL(path.replace(/^\//, ''), apiUrl), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      redirect: 'manual',
      signal: AbortSignal.timeout(20_000),
    });

    for (const cookie of response.headers.getSetCookie()) {
      const parts = cookie.split(';').map(part => part.trim());
      const separator = parts[0].indexOf('=');
      if (separator < 1) continue;
      const name = parts[0].slice(0, separator);
      const value = parts[0].slice(separator + 1);
      const attributes = new Map(parts.slice(1).map(part => {
        const position = part.indexOf('=');
        return [part.slice(0, position < 0 ? part.length : position).toLowerCase(), position < 0 ? '' : part.slice(position + 1)];
      }));
      const maxAge = attributes.get('max-age');
      const expires = attributes.get('expires');
      const expired = maxAge !== undefined
        ? Number(maxAge) <= 0
        : expires !== undefined && Date.parse(expires) <= Date.now();
      if (expired || value === '') this.cookies.delete(name);
      else this.cookies.set(name, value);
    }

    const content = await response.text();
    let responseBody = null;
    if (content) {
      try { responseBody = JSON.parse(content); }
      catch { assert.fail(`${method} ${path}: expected JSON, received ${response.headers.get('content-type') ?? 'unknown content type'}`); }
    }
    assert.ok(response.status < 300 || response.status >= 400, `${method} ${path}: API must not redirect to an HTML login page`);
    return { status: response.status, body: responseBody, headers: response.headers };
  }

  async refreshCsrf() {
    const body = expectStatus(await this.request('/auth/csrf'), 200, 'Get CSRF token');
    assert.equal(typeof body.token, 'string', 'CSRF response has a token');
    assert.ok(body.token.length > 0, 'CSRF token cannot be empty');
    assert.ok(this.cookies.size > 0, 'Antiforgery endpoint must issue a cookie');
    this.csrfToken = body.token;
  }

  async login(email, password) {
    await this.refreshCsrf();
    expectStatus(await this.request('/auth/login', { method: 'POST', body: { email, password } }), 200, 'Login');
    // ASP.NET antiforgery tokens are identity-bound; an anonymous token cannot
    // be reused after a successful login.
    await this.refreshCsrf();
    return expectStatus(await this.request('/me'), 200, 'Authenticated profile');
  }
}

async function check(name, action) {
  await action();
  passed += 1;
  console.log(`PASS ${String(passed).padStart(2, '0')} ${name}`);
}

async function create(session, path, body) {
  return resource(expectStatus(await session.request(path, { method: 'POST', body }), 201, `Create ${path}`), path);
}

async function movement(session, body, key = randomUUID()) {
  return session.request('/movements', { method: 'POST', body, key });
}

function recordMovement(result, expected = 201) {
  const item = resource(expectStatus(result, expected, 'Movement'), 'Movement');
  assert.ok(item.number !== undefined && String(item.number).length > 0, 'Movement has a readable number');
  assert.equal(typeof item.createdAt, 'string');
  assert.ok(Number.isFinite(Date.parse(item.createdAt)), 'Movement date must be parseable');
  assert.equal(typeof item.actorName, 'string');
  assert.ok(item.actorName.length > 0, 'Movement identifies its actor');
  createdMovementIds.add(item.id);
  return item;
}

async function stockRows(session, productCode) {
  const body = expectStatus(await session.request(`/stock?search=${encodeURIComponent(productCode)}&page=1&pageSize=100`), 200, 'Stock search');
  assert.ok(Array.isArray(body.items), 'Stock page contains items');
  return body.items.filter(item => item.productCode === productCode);
}

async function assertBalance(session, product, location, expected) {
  const matches = (await stockRows(session, product.code)).filter(item => item.locationId === location.id);
  assert.equal(matches.length, 1, `Exactly one balance for ${product.code} / ${location.code}`);
  const balance = matches[0];
  quantity(balance.quantity, expected, 'Physical quantity');
  quantity(balance.reserved, 0, 'Reserved quantity');
  quantity(balance.available, expected, 'Available quantity');
  assert.equal(balance.productId, product.id);
  return balance;
}

async function main() {
  if (process.argv.includes('--help') || process.argv.includes('-h')) {
    console.log(HELP);
    return;
  }
  assert.ok(Number(process.versions.node.split('.')[0]) >= 24, 'Use Node.js 24 or newer');
  const setupToken = process.env.TEST_SETUP_TOKEN;
  assert.ok(setupToken, 'TEST_SETUP_TOKEN is required. Run with --help for the dedicated-test-database requirements.');
  assert.ok(['http:', 'https:'].includes(apiUrl.protocol), 'API_BASE_URL must be an HTTP(S) URL');
  assert.ok(!apiUrl.username && !apiUrl.password, 'Do not put credentials in API_BASE_URL');

  const admin = new ApiSession();
  const operator = new ApiSession();
  const viewer = new ApiSession();
  const anonymous = new ApiSession();
  const runId = randomUUID().slice(0, 8);
  const password = process.env.TEST_ADMIN_PASSWORD ?? `Integration-${randomUUID()}-aA9!`;
  const emails = {
    admin: process.env.TEST_ADMIN_EMAIL ?? `admin-${runId}@integration.example`,
    operator: `operator-${runId}@integration.example`,
    viewer: `viewer-${runId}@integration.example`,
  };
  const state = {};

  await check('Safety gate: API must use an empty, unprovisioned test database', async () => {
    const status = expectStatus(await admin.request('/setup/status'), 200, 'Setup status');
    assert.equal(status.required, true, 'ABORTED: database is already provisioned. No reset will be attempted. Point API_BASE_URL at a fresh dedicated test instance.');
    console.log(`Target: ${apiUrl.origin}${apiUrl.pathname} (unprovisioned database confirmed)`);
  });

  await check('Setup validates CSRF and setup secret, then provisions once', async () => {
    await admin.refreshCsrf();
    const setup = { organizationName: `Integration ${runId}`, name: 'Administrador de teste', email: emails.admin, password, setupToken };
    expectClientError(await admin.request('/setup', { method: 'POST', body: setup, token: 'invalid-csrf-token' }), 'Invalid setup CSRF');
    expectClientError(await admin.request('/setup', { method: 'POST', body: { ...setup, setupToken: `incorrect-${randomUUID()}` } }), 'Invalid setup secret');
    expectStatus(await admin.request('/setup'), [404, 405], 'Setup cannot be invoked with GET');
    expectStatus(await admin.request('/setup', { method: 'POST', body: setup }), 201, 'Provision setup');
    assert.equal(expectStatus(await admin.request('/setup/status'), 200, 'Setup state after provisioning').required, false);
    expectClientError(await admin.request('/setup', { method: 'POST', body: setup }), 'Second setup must be rejected');
  });

  await check('Login establishes session and returns organization-scoped identity', async () => {
    expectStatus(await anonymous.request('/me'), 401, 'Anonymous profile');
    expectStatus(await admin.request('/auth/login', { method: 'POST', body: { email: emails.admin, password: 'not-the-password' } }), 401, 'Invalid login');
    state.admin = await admin.login(emails.admin, password);
    assert.equal(state.admin.role, 'Admin');
    assert.equal(state.admin.email, emails.admin);
    assert.ok(state.admin.organizationId && state.admin.organizationName, 'Profile contains organization');
    expectStatus(await anonymous.request('/stock'), 401, 'Anonymous stock query');
  });

  await check('Admin creates Operator and Viewer; role and account rules are enforced', async () => {
    state.operator = await create(admin, '/users', { name: 'Operador de teste', email: emails.operator, password, role: 'Operator' });
    state.viewer = await create(admin, '/users', { name: 'Consulta de teste', email: emails.viewer, password, role: 'Viewer' });
    const operatorProfile = await operator.login(emails.operator, password);
    const viewerProfile = await viewer.login(emails.viewer, password);
    assert.equal(operatorProfile.role, 'Operator');
    assert.equal(viewerProfile.role, 'Viewer');
    assert.equal(operatorProfile.organizationId, state.admin.organizationId);
    assert.equal(viewerProfile.organizationId, state.admin.organizationId);
    const users = expectStatus(await admin.request('/users'), 200, 'Admin user listing');
    assert.equal(users.length, 3, 'Exactly the three provisioned test users');
    assert.ok(users.every(user => !('password' in user) && !('passwordHash' in user)), 'User queries never expose credentials');
    expectStatus(await operator.request('/users'), 403, 'Operator cannot list users');
    expectStatus(await viewer.request('/users'), 403, 'Viewer cannot list users');
    const extra = { name: 'Forbidden', email: `forbidden-${runId}@integration.example`, password, role: 'Admin' };
    expectStatus(await operator.request('/users', { method: 'POST', body: extra }), 403, 'Operator cannot create admin');
    expectStatus(await viewer.request('/users', { method: 'POST', body: extra }), 403, 'Viewer cannot create users');
    expectClientError(await admin.request(`/users/${state.admin.id}/status`, { method: 'PATCH', body: { isActive: false } }), 'Admin cannot disable self / last admin');
  });

  await check('Locations support parent/child hierarchy and reject nonexistent parents', async () => {
    state.room = await create(admin, '/locations', { name: 'Sala de testes', code: `ROOM-${runId}`, type: 'Room', parentId: null, canStore: false });
    state.cabinet = await create(operator, '/locations', { name: 'Armário A', code: `CAB-A-${runId}`, type: 'Cabinet', parentId: state.room.id, canStore: true });
    state.cabinetB = await create(admin, '/locations', { name: 'Armário B', code: `CAB-B-${runId}`, type: 'Cabinet', parentId: state.room.id, canStore: true });
    assert.equal(state.cabinet.parentId, state.room.id);
    assert.ok(state.cabinet.path.includes(state.room.name) && state.cabinet.path.includes(state.cabinet.name), 'Location path includes its parent and itself');
    expectClientError(await admin.request('/locations', { method: 'POST', body: { name: 'Invalid parent', code: `BAD-${runId}`, type: 'Cabinet', parentId: randomUUID(), canStore: true } }), 'Unknown / out-of-scope parent');
    const locations = expectStatus(await viewer.request('/locations'), 200, 'Viewer can read locations');
    assert.equal(locations.length, 3, 'Invalid parent did not create a location');
  });

  await check('Product catalogue supports consumables, returnables and decimal precision', async () => {
    const product = { code: `CONS-${runId}`, name: 'Luvas de teste', category: 'Proteção', unit: 'un', kind: 'Consumable', minimumStock: '5', quantityScale: 0 };
    state.consumable = await create(operator, '/products', product);
    state.returnable = await create(admin, '/products', { ...product, code: `RET-${runId}`, name: 'Furadeira de teste', category: 'Ferramentas', kind: 'Returnable', minimumStock: '1' });
    state.decimal = await create(admin, '/products', { ...product, code: `DEC-${runId}`, name: 'Líquido de teste', unit: 'L', minimumStock: '0.125', quantityScale: 3 });
    state.concurrent = await create(admin, '/products', { ...product, code: `RACE-${runId}`, name: 'Produto concorrente', minimumStock: '4' });
    state.idempotent = await create(admin, '/products', { ...product, code: `IDEM-${runId}`, name: 'Produto idempotente', minimumStock: '0' });
    quantity(state.consumable.minimumStock, 5, 'Product minimum is a decimal string');
    expectClientError(await admin.request('/products', { method: 'POST', body: product }), 'Duplicate SKU');
    expectClientError(await admin.request('/products', { method: 'POST', body: { ...product, code: `BAD-SCALE-${runId}`, quantityScale: 7 } }), 'Unsupported precision');
    expectClientError(await admin.request('/products', { method: 'POST', body: { ...product, code: `BAD-MIN-${runId}`, minimumStock: '-1' } }), 'Negative minimum');
    const search = expectStatus(await viewer.request(`/products?search=${state.consumable.code}&page=1&pageSize=25`), 200, 'Catalogue search');
    assert.equal(search.total, 1);
    assert.equal(search.items[0].id, state.consumable.id);
    assert.equal((await stockRows(admin, state.consumable.code)).length, 0, 'New SKU does not invent a storage position');
  });

  const receipt = (product, amount, location = state.cabinet) => ({ type: 'Receipt', productId: product.id, locationId: location.id, quantity: amount, reference: `TEST-${runId}`, notes: 'Integration test receipt', recipient: null });
  const consumption = (product, amount, location = state.cabinet) => ({ type: 'Consumption', productId: product.id, locationId: location.id, quantity: amount, reference: `TEST-${runId}`, notes: 'Integration test consumption', recipient: 'Equipe de teste' });

  await check('Receipt 10 and consumption 3 produce balance 7 with immutable history', async () => {
    state.receipt = recordMovement(await movement(operator, receipt(state.consumable, '10')));
    quantity(state.receipt.quantity, 10, 'Receipt quantity');
    state.consumption = recordMovement(await movement(operator, consumption(state.consumable, '3')));
    const balance = await assertBalance(viewer, state.consumable, state.cabinet, 7);
    assert.equal(balance.status, 'Healthy');
    const history = expectStatus(await viewer.request(`/movements?search=${state.consumable.code}&page=1&pageSize=100`), 200, 'Movement history');
    assert.equal(history.total, 2);
    assert.deepEqual(new Set(history.items.map(item => item.id)), new Set([state.receipt.id, state.consumption.id]));
    assert.equal(history.items[0].type, 'Consumption', 'Most recent first');
    assert.equal(history.items[0].recipient, 'Equipe de teste');
    expectClientError(await admin.request(`/movements/${state.receipt.id}`, { method: 'PATCH', body: { quantity: '100' } }), 'Posted ledger cannot be edited');
    expectClientError(await admin.request(`/movements/${state.receipt.id}`, { method: 'DELETE' }), 'Posted ledger cannot be deleted');
    await assertBalance(admin, state.consumable, state.cabinet, 7);
  });

  await check('Rejected quantities and invalid references leave stock and ledger unchanged', async () => {
    const invalid = [
      ['zero quantity', { ...receipt(state.consumable, '0') }],
      ['negative quantity', { ...receipt(state.consumable, '-1') }],
      ['excess decimal places', { ...receipt(state.consumable, '0.5') }],
      ['comma decimal', { ...receipt(state.consumable, '1,5') }],
      ['missing recipient', { ...consumption(state.consumable, '1'), recipient: '' }],
      ['unknown product', { ...receipt(state.consumable, '1'), productId: randomUUID() }],
      ['unknown location', { ...receipt(state.consumable, '1'), locationId: randomUUID() }],
      ['non-storage parent', receipt(state.consumable, '1', state.room)],
    ];
    for (const [label, body] of invalid) expectClientError(await movement(operator, body), label);
    expectClientError(await movement(operator, consumption(state.consumable, '8')), 'Insufficient stock');
    expectClientError(await movement(operator, consumption(state.consumable, '1', state.cabinetB)), 'Cannot withdraw from nonexistent balance');
    await assertBalance(admin, state.consumable, state.cabinet, 7);
    const history = expectStatus(await admin.request(`/movements?search=${state.consumable.code}&pageSize=100`), 200, 'History after failed mutations');
    assert.equal(history.total, 2, 'Failed movements add no ledger entries');
    assert.equal((await stockRows(admin, state.consumable.code)).length, 1, 'Failed withdrawal does not create a phantom balance');
  });

  await check('CSRF and Viewer permissions protect writes while preserving read access', async () => {
    expectStatus(await viewer.request('/stock'), 200, 'Viewer reads stock');
    expectStatus(await viewer.request('/dashboard'), 200, 'Viewer reads dashboard');
    expectStatus(await movement(viewer, receipt(state.consumable, '1')), 403, 'Viewer cannot receive stock');
    expectStatus(await viewer.request('/products', { method: 'POST', body: { code: `VIEW-${runId}`, name: 'Forbidden', category: 'Test', unit: 'un', kind: 'Consumable', minimumStock: '0', quantityScale: 0 } }), 403, 'Viewer cannot create products');
    expectStatus(await viewer.request('/locations', { method: 'POST', body: { name: 'Forbidden', code: `VIEW-${runId}`, type: 'Room', parentId: null, canStore: true } }), 403, 'Viewer cannot create locations');
    expectClientError(await operator.request('/movements', { method: 'POST', body: receipt(state.consumable, '1'), key: randomUUID(), token: 'invalid-csrf-token' }), 'Authenticated mutation rejects invalid CSRF');
    expectClientError(await operator.request('/movements', { method: 'POST', body: receipt(state.consumable, '1'), key: randomUUID(), csrf: false }), 'Authenticated mutation rejects missing CSRF');
    expectClientError(await operator.request('/movements', { method: 'POST', body: receipt(state.consumable, '1') }), 'Movement requires idempotency key');
    await assertBalance(admin, state.consumable, state.cabinet, 7);
  });

  await check('Two simultaneous consumptions of 7 from stock 10 confirm exactly once', async () => {
    recordMovement(await movement(admin, receipt(state.concurrent, '10')));
    const results = await Promise.all([
      movement(admin, consumption(state.concurrent, '7')),
      movement(operator, consumption(state.concurrent, '7')),
    ]);
    const successful = results.filter(result => result.status === 201);
    const rejected = results.filter(result => result.status >= 400 && result.status < 500);
    assert.equal(successful.length, 1, `Exactly one concurrent withdrawal succeeds (got ${results.map(result => result.status).join(', ')})`);
    assert.equal(rejected.length, 1, 'Other withdrawal fails with a business error, never HTTP 500');
    recordMovement(successful[0]);
    const balance = await assertBalance(admin, state.concurrent, state.cabinet, 3);
    assert.equal(balance.status, 'Low');
    const history = expectStatus(await admin.request(`/movements?search=${state.concurrent.code}&pageSize=100`), 200, 'Concurrent ledger');
    assert.equal(history.total, 2, 'Only receipt and successful withdrawal are recorded');
  });

  await check('Idempotency replays the original result and rejects payload mismatch', async () => {
    const key = randomUUID();
    const body = receipt(state.idempotent, '10');
    const original = recordMovement(await movement(admin, body, key));
    const replay = recordMovement(await movement(admin, body, key), 200);
    assert.equal(replay.id, original.id);
    assert.equal(replay.number, original.number);
    expectStatus(await movement(admin, { ...body, quantity: '11' }, key), 409, 'Same key with changed payload');
    await assertBalance(admin, state.idempotent, state.cabinet, 10);

    const concurrentKey = randomUUID();
    const concurrentBody = receipt(state.idempotent, '2');
    const simultaneous = await Promise.all([
      movement(admin, concurrentBody, concurrentKey),
      movement(admin, concurrentBody, concurrentKey),
    ]);
    assert.deepEqual(simultaneous.map(result => result.status).sort(), [200, 201], 'Simultaneous duplicate resolves into one create and one replay');
    const items = simultaneous.map(result => recordMovement(result, [200, 201]));
    assert.equal(items[0].id, items[1].id);
    await assertBalance(admin, state.idempotent, state.cabinet, 12);
    const history = expectStatus(await admin.request(`/movements?search=${state.idempotent.code}&pageSize=100`), 200, 'Idempotent ledger');
    assert.equal(history.total, 2, 'Duplicate and mismatched requests create no extra ledger entries');
  });

  await check('Returnables can be received but not consumed; decimal balances remain exact', async () => {
    recordMovement(await movement(operator, receipt(state.returnable, '2')));
    expectClientError(await movement(operator, consumption(state.returnable, '1')), 'Returnable consumption must wait for loan workflow');
    await assertBalance(admin, state.returnable, state.cabinet, 2);
    recordMovement(await movement(operator, receipt(state.decimal, '1.125')));
    recordMovement(await movement(operator, consumption(state.decimal, '0.125')));
    await assertBalance(admin, state.decimal, state.cabinet, 1);
    expectClientError(await movement(operator, receipt(state.decimal, '0.0001')), 'SKU precision is enforced');
    await assertBalance(admin, state.decimal, state.cabinet, 1);
  });

  await check('Filters, pagination, dashboard and ledger agree on committed stock', async () => {
    const products = expectStatus(await admin.request('/products?page=1&pageSize=2'), 200, 'Product pagination');
    assert.equal(products.items.length, 2);
    assert.equal(products.total, 5);
    assert.equal(products.page, 1);
    assert.equal(products.pageSize, 2);
    const next = expectStatus(await admin.request('/products?page=2&pageSize=2'), 200, 'Second product page');
    assert.ok(next.items.every(item => !products.items.some(first => first.id === item.id)), 'Pages have no duplicate products');
    const hugePage = await admin.request('/products?pageSize=1000');
    if (hugePage.status === 200) assert.ok(hugePage.body.pageSize <= 100, 'Page size must be capped at 100');
    else expectClientError(hugePage, 'Oversized page is rejected');
    const cabinetB = expectStatus(await admin.request(`/stock?locationId=${state.cabinetB.id}&pageSize=100`), 200, 'Location filter');
    assert.equal(cabinetB.total, 0, 'Unused location has no balances');
    const low = expectStatus(await admin.request('/stock?lowStockOnly=true&pageSize=100'), 200, 'Low-stock filter');
    assert.equal(low.total, 1);
    assert.equal(low.items[0].productId, state.concurrent.id);
    const allHistory = expectStatus(await admin.request('/movements?pageSize=100'), 200, 'All movements');
    assert.equal(allHistory.total, createdMovementIds.size, 'Every and only committed movements are present');
    assert.deepEqual(new Set(allHistory.items.map(item => item.id)), createdMovementIds);
    const consumptionHistory = expectStatus(await admin.request('/movements?type=Consumption&pageSize=100'), 200, 'Movement type filter');
    assert.equal(consumptionHistory.total, 3);
    assert.ok(consumptionHistory.items.every(item => item.type === 'Consumption'));
    const dashboard = expectStatus(await admin.request('/dashboard'), 200, 'Dashboard');
    assert.equal(dashboard.productCount, 5);
    assert.equal(dashboard.locationCount, 3);
    assert.equal(dashboard.stockedPositionCount, 5);
    assert.equal(dashboard.lowStockCount, 1);
    assert.ok(dashboard.todayMovementCount >= 0 && dashboard.todayMovementCount <= createdMovementIds.size, 'Today count is bounded (test may cross midnight)');
    assert.ok(dashboard.recentMovements.length <= 6);
    assert.ok(dashboard.lowStockItems.length <= 5);
    assert.ok(dashboard.lowStockItems.some(item => item.productId === state.concurrent.id));
  });

  await check('Suspending an operator invalidates an existing authenticated session', async () => {
    expectStatus(await admin.request(`/users/${state.operator.id}/status`, { method: 'PATCH', body: { isActive: false } }), [200, 204], 'Suspend operator');
    expectStatus(await operator.request('/me'), [401, 403], 'Suspended operator profile');
    expectStatus(await operator.request('/stock'), [401, 403], 'Suspended operator reads');
    expectStatus(await movement(operator, receipt(state.consumable, '1')), [401, 403], 'Suspended operator writes');
    const suspendedLogin = new ApiSession();
    await suspendedLogin.refreshCsrf();
    expectStatus(await suspendedLogin.request('/auth/login', { method: 'POST', body: { email: emails.operator, password } }), [401, 403], 'Suspended operator cannot log back in');
    await assertBalance(admin, state.consumable, state.cabinet, 7);
    const users = expectStatus(await admin.request('/users'), 200, 'User status confirmation');
    assert.equal(users.find(user => user.id === state.operator.id).isActive, false);
  });

  await check('Logout clears authentication', async () => {
    expectStatus(await viewer.request('/auth/logout', { method: 'POST' }), 204, 'Logout');
    expectStatus(await viewer.request('/me'), 401, 'Logged-out profile');
  });

  console.log(`\n${passed} integration checks passed. ${createdMovementIds.size} committed movements verified.`);
  console.log('The test issued no destructive reset; the PowerShell runner discards only its dedicated ephemeral database.');
  console.log('Cross-organization isolation still requires a separate two-organization fixture; public setup only creates one organization.');
}

main().catch(error => {
  console.error(`\nFAIL after ${passed} checks: ${error.stack ?? error.message}`);
  console.error('The test issued no destructive reset; the PowerShell runner will discard only its dedicated ephemeral database.');
  process.exitCode = 1;
});

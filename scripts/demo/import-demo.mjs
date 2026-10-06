import { readFile, writeFile, mkdir, rename } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

/** Validate every referenced model and asset before the first database mutation. */
export function validateManifest(manifest) {
  if (manifest.version !== 'tech-demo-v1' || manifest.products?.length !== 200) throw new Error('Expected the versioned 200-product manifest.');
  const slugs = new Set(manifest.categories.map((category) => category.slug));
  const seen = new Set();
  const sourceIds = new Set();
  for (const product of manifest.products) {
    if (!/^DEMO-(KEYCHRON|UGREEN|BASEUS|SATECHI)-\d+$/.test(product.sku) || seen.has(product.sku)) throw new Error('Invalid or duplicate SKU.');
    const sourceKey = `${product.sourceVendor}:${product.sourceProductId}`;
    if (sourceIds.has(sourceKey)) throw new Error('Duplicate source model.');
    sourceIds.add(sourceKey); seen.add(product.sku);
    if (!slugs.has(product.categorySlug) || !product.name || product.name.length > 200 || product.description.length > 2000 ||
      !product.brand || !Number.isFinite(product.priceAmount) || product.priceAmount <= 0 || product.priceCurrency !== 'USD' ||
      !Number.isInteger(product.initialStock) || product.initialStock < 0 ||
      !/^\/api\/catalog\/media\/demo-[a-z0-9-]+\.(jpg|png|webp|avif)$/.test(product.imageUrl) ||
      !/^https:\/\//.test(product.sourceUrl) || !/^[a-f0-9]{64}$/.test(product.imageSha256)) throw new Error(`Invalid model: ${product.sku}`);
  }
  for (const category of manifest.categories) {
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(category.slug) || !category.name || category.name.length > 120 ||
      category.productCount !== manifest.products.filter((product) => product.categorySlug === category.slug).length)
      throw new Error('Invalid category counts.');
  }
  return manifest;
}

/** Calculate the immutable order expectation in integer cents. */
export function totalCents(items) {
  return items.reduce((sum, item) => sum + Math.round(item.priceAmount * 100) * item.quantity, 0);
}

/** Choose a deterministic, bounded cart without consuming reserved low-stock demonstration products. */
export function plannedItems(products, index) {
  if (index % 10 === 9) return [{ ...products.find((product) => product.initialStock === 0), quantity: 1 }];
  const stocked = products.filter((product) => product.initialStock >= 25);
  return Array.from({ length: 1 + index % 3 }, (_, offset) => ({ ...stocked[(index * 7 + offset * 31) % stocked.length], quantity: 1 + (index + offset) % 2 }));
}

/** Return sixty fictional, repeatable logins; no actual personal records are imported. */
export function demoAccounts() {
  const given = ['Minh', 'Linh', 'An', 'Trang', 'Khoa', 'Vy', 'Nam', 'Mai', 'Huy', 'Nhi', 'Long', 'Ha', 'Phuc'];
  const surnames = ['Nguyen', 'Tran', 'Le', 'Pham'];
  const customers = Array.from({ length: 52 }, (_, index) => ({
    email: `${given[index % given.length].toLowerCase()}.${surnames[Math.floor(index / given.length)].toLowerCase()}.${String(index + 1).padStart(2, '0')}@demo.example.test`,
    name: `${surnames[Math.floor(index / given.length)]} ${given[index % given.length]}`, role: 'Customer',
  }));
  const staff = ['CatalogManager', 'WarehouseManager', 'OrderManager', 'SupportAgent'].flatMap((role) => [1, 2].map((number) =>
    ({ email: `${role.toLowerCase()}.${number}@demo.example.test`, name: `${role} ${number}`, role })));
  return [...customers, ...staff];
}

/** A single operator can import into a local Gateway; no service database or internal endpoint is accessed. */
async function main() {
  const base = (process.env.DEMO_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');
  if (!['localhost', '127.0.0.1', '[::1]'].includes(new URL(base).hostname)) throw new Error('This importer is restricted to the local demo Gateway.');
  const mode = process.argv[2] || 'all';
  if (!['all', 'catalog', 'orders', 'verify'].includes(mode)) throw new Error('Use all, catalog, orders or verify.');
  const manifest = validateManifest(JSON.parse(await readFile(resolve(root, 'demo/catalog.json'), 'utf8')));
  const manifestHash = createHash('sha256').update(JSON.stringify(manifest)).digest('hex');
  let galleries = new Map();
  try {
    const supplement = JSON.parse(await readFile(resolve(root, 'demo/galleries.json'), 'utf8'));
    if (supplement.version !== 'tech-demo-gallery-v1' || supplement.baseVersion !== manifest.version || supplement.products.length !== 200)
      throw new Error('Invalid supplementary gallery manifest.');
    for (const row of supplement.products) {
      const model = manifest.products.find((product) => product.sku === row.sku);
      if (!model || galleries.has(row.sku) || row.images.length < 1 || row.images.length > 8 || row.images[0].imageUrl !== model.imageUrl ||
        new Set(row.images.map((image) => image.imageUrl)).size !== row.images.length) throw new Error('Gallery model, primary photo or ordering mismatch.');
      galleries.set(row.sku, row.images);
    }
  } catch (error) { if (error.code !== 'ENOENT') throw error; }
  for (const product of manifest.products) {
    const bytes = await readFile(resolve(root, 'src/Services/Catalog/Catalog.Api/wwwroot/demo-products', product.imageUrl.split('/').at(-1)));
    if (createHash('sha256').update(bytes).digest('hex') !== product.imageSha256) throw new Error(`Image checksum mismatch: ${product.sku}`);
    for (const image of galleries.get(product.sku) || []) {
      if (!/^\/api\/catalog\/media\/demo-[a-z0-9-]+\.(jpg|png|webp|avif)$/.test(image.imageUrl) || !/^[a-f0-9]{64}$/.test(image.imageSha256))
        throw new Error('Unsafe gallery asset path or checksum.');
      const asset = await readFile(resolve(root, 'src/Services/Catalog/Catalog.Api/wwwroot/demo-products', image.imageUrl.split('/').at(-1)));
      if (createHash('sha256').update(asset).digest('hex') !== image.imageSha256) throw new Error(`Alternate image checksum mismatch: ${product.sku}`);
    }
  }
  const stateDirectory = resolve(root, 'artifacts/demo-state');
  await mkdir(stateDirectory, { recursive: true });
  const statePath = resolve(stateDirectory, 'checkpoint.local.json');
  let state;
  try { state = JSON.parse(await readFile(statePath, 'utf8')); }
  catch (error) { if (error.code !== 'ENOENT') throw error; state = { version: manifest.version, manifestHash, gateway: base, products: {}, users: {}, orders: {} }; }
  if (state.gateway !== base || state.manifestHash !== manifestHash) throw new Error('Checkpoint belongs to another Gateway or manifest. Preserve it and choose a separate demo environment.');
  const persist = async () => {
    await writeFile(statePath + '.tmp', JSON.stringify(state, null, 2) + '\n');
    await rename(statePath + '.tmp', statePath);
  };
  const config = {};
  try { for (const line of (await readFile(resolve(root, '.env'), 'utf8')).split(/\r?\n/)) {
    const match = /^([^#=]+)=(.*)$/.exec(line); if (match) config[match[1].trim()] = match[2];
  } } catch (error) { if (error.code !== 'ENOENT') throw error; }
  const adminEmail = process.env.DEMO_ADMIN_EMAIL || config.ADMIN_EMAIL;
  const adminPassword = process.env.DEMO_ADMIN_PASSWORD || config.ADMIN_PASSWORD;
  const password = process.env.DEMO_CUSTOMER_PASSWORD || config.CUSTOMER_PASSWORD;
  if (!adminEmail || !adminPassword || !password) throw new Error('Provide local administrator and demo customer credentials via .env or DEMO_* environment variables.');
  let lastRequest = 0;
  async function call(path, { method = 'GET', token, body, headers = {}, allow = [] } = {}) {
    // Keep below Gateway's shared anonymous rate limit without automatically retrying mutations.
    await delay(Math.max(0, 225 - (Date.now() - lastRequest)));
    lastRequest = Date.now();
    const response = await fetch(base + path, { method, headers: { ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers }, body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(30_000) });
    if (!response.ok && !allow.includes(response.status)) throw new Error(`${method} ${path}: HTTP ${response.status}. No mutation retry was attempted; rerun to resume the checkpoint.`);
    if (!response.ok) return { httpStatus: response.status };
    return response.status === 204 ? null : response.json();
  }
  const login = async (email) => call('/api/auth/login', { method: 'POST', body: { email, password: email === adminEmail ? adminPassword : password } });
  const ready = await fetch(base + '/health/ready', { signal: AbortSignal.timeout(5000) });
  if (!ready.ok) throw new Error('Gateway is not ready.');
  let admin = await login(adminEmail);
  async function pageAll(path, totalKey) {
    const rows = [];
    for (let page = 1; ; page++) {
      const response = await call(path + (path.includes('?') ? '&' : '?') + `pageSize=100&page=${page}`, { token: admin.accessToken });
      rows.push(...response.items);
      if (rows.length >= response[totalKey]) return rows;
      if (!response.items.length) throw new Error(`Pagination mismatch: ${path}`);
    }
  }

  if (mode === 'all' || mode === 'catalog') {
    const categories = await call('/api/catalog/categories?includeInactive=true', { token: admin.accessToken });
    for (const category of manifest.categories) {
      const existing = categories.find((group) => group.slug === category.slug);
      if (!existing) await call('/api/catalog/categories', { method: 'POST', token: admin.accessToken, body: { name: category.name, slug: category.slug } });
      else if (!existing.isActive) throw new Error(`Demo group was deliberately disabled: ${category.slug}. Enable it in administration before resuming.`);
    }
    const existing = await pageAll('/api/catalog/products', 'total');
    for (const [index, product] of manifest.products.entries()) {
      let found = existing.find((row) => row.sku === product.sku);
      if (!found) {
        const created = await call('/api/catalog/products', { method: 'POST', token: admin.accessToken,
          body: { ...product, ...(galleries.has(product.sku) ? { imageUrls: galleries.get(product.sku).map((image) => image.imageUrl) } : {}) } });
        found = { ...product, id: created.productId, status: 'Draft' };
      }
      else if (found.sourceUrl !== product.sourceUrl || found.imageUrl !== product.imageUrl || found.categorySlug !== product.categorySlug ||
        found.brand !== product.brand || found.name !== product.name || found.priceAmount !== product.priceAmount || found.priceCurrency !== product.priceCurrency)
        throw new Error(`Existing SKU has different merchandising data: ${product.sku}. Review instead of overwriting.`);
      const gallery = galleries.get(product.sku)?.map((image) => image.imageUrl);
      if (gallery && JSON.stringify(found.imageUrls) !== JSON.stringify(gallery)) {
        if (found.imageUrls?.length > 1) throw new Error(`The gallery was edited independently: ${product.sku}. Review instead of overwriting.`);
        await call(`/api/catalog/products/${found.id}`, { method: 'PUT', token: admin.accessToken, body: { ...product, imageUrls: gallery } });
      }
      state.products[product.sku] = { ...state.products[product.sku], id: found.id };
      await persist();
      if (found.status === 'Draft') await call(`/api/catalog/products/${found.id}/activate`, { method: 'POST', token: admin.accessToken });
      else if (found.status !== 'Active') throw new Error(`Demo product was deliberately deactivated: ${product.sku}.`);
      if (!state.products[product.sku].stockChecked) {
        let stock = await call(`/api/inventory/${found.id}`, { token: admin.accessToken, allow: [404] });
        // A stock row proves a previous receipt already committed, even after a lost HTTP response or depleted stock.
        if (stock.httpStatus === 404 && product.initialStock > 0)
          await call(`/api/inventory/${found.id}/receipts`, { method: 'POST', token: admin.accessToken, body: { quantity: product.initialStock } });
        if (product.initialStock === 0) {
          // Create a real zero-stock row through a receipt and an audited depletion, resuming safely after either response is lost.
          state.products[product.sku].zeroStockPreparation = true; await persist();
          if (stock.httpStatus === 404)
            stock = await call(`/api/inventory/${found.id}/receipts`, { method: 'POST', token: admin.accessToken, body: { quantity: 1 } });
          if (stock.quantityOnHand === 1 && stock.reservedQuantity === 0)
            stock = await call(`/api/inventory/${found.id}/adjustments`, { method: 'POST', token: admin.accessToken,
              body: { delta: -1, reason: 'Demo sold-out scenario: remove initial setup unit', version: stock.version } });
          if (stock.quantityOnHand !== 0 || stock.reservedQuantity !== 0) throw new Error(`Review sold-out demo stock before resuming: ${product.sku}`);
        }
        state.products[product.sku].stockChecked = true; await persist();
      }
      if ((index + 1) % 20 === 0) console.log(`Catalog ready: ${index + 1}/200`);
    }
  }

  const accounts = demoAccounts();
  if (mode === 'all' || mode === 'orders') {
    const existingUsers = await pageAll('/api/auth/users', 'totalCount');
    for (const account of accounts) {
      let user = existingUsers.find((entry) => entry.email === account.email);
      if (!user) {
        const tokens = await call('/api/auth/register', { method: 'POST', body: { email: account.email, password } });
        user = await call('/api/auth/me', { token: tokens.accessToken });
        // End the setup session; credentials are never placed in the checkpoint or generated reports.
        await call('/api/auth/logout', { method: 'POST', body: { refreshToken: tokens.refreshToken } });
        existingUsers.push(user);
      }
      if (account.role !== 'Customer' && !user.roles.includes(account.role))
        await call(`/api/auth/users/${user.id}/roles`, { method: 'PUT', token: admin.accessToken, body: { roles: [account.role] } });
      state.users[account.email] = { id: user.id, role: account.role }; await persist();
    }
    console.log('60 demo accounts ready (52 customers, 8 staff).');
    const products = manifest.products.map((product) => ({ ...product, id: state.products[product.sku]?.id }));
    if (products.some((product) => !product.id)) throw new Error('Import the catalog before orders.');
    const cities = [['Ho Chi Minh City', '700000'], ['Hanoi', '100000'], ['Da Nang', '550000'], ['Can Tho', '900000'], ['Hai Phong', '180000'], ['Nha Trang', '650000']];
    for (let index = 0; index < 120; index++) {
      const key = `${manifest.version}-order-${String(index + 1).padStart(3, '0')}`;
      if (state.orders[key]?.verified) continue;
      const account = accounts[index % 40];
      const tokens = await login(account.email);
      const [city, postalCode] = cities[index % cities.length];
      const planned = plannedItems(products, index);
      const address = { recipientName: account.name, addressLine1: `Demo Building ${1 + index % 12}, Floor ${1 + index % 8} (presentation address)`, city, postalCode, countryCode: 'VN' };
      let attempt = state.orders[key];
      if (!attempt) {
        attempt = { ownerId: state.users[account.email].id, email: account.email, address,
          items: planned.map((product) => ({ productId: product.id, sku: product.sku, quantity: product.quantity, priceAmount: product.priceAmount })),
          expectedCents: totalCents(planned), expectedStatus: index % 10 === 9 ? 'Cancelled' : 'Shipped',
          targetShipmentStatus: ['Created', 'ReadyForPickup', 'InTransit', 'Delivered'][index % 4] };
        state.orders[key] = attempt; await persist();
      }
      if (attempt.ownerId !== state.users[account.email].id || JSON.stringify(attempt.address) !== JSON.stringify(address)) throw new Error('Immutable checkout owner/address mismatch.');
      if (!attempt.orderId) {
        await call('/api/cart', { method: 'DELETE', token: tokens.accessToken });
        for (const item of attempt.items) await call(`/api/cart/items/${item.productId}`, { method: 'PUT', token: tokens.accessToken, body: { quantity: item.quantity } });
        // Persist payload before submitting; a lost response reuses the same owner-scoped durable checkout key.
        const checkout = await call('/api/orders/checkout', { method: 'POST', token: tokens.accessToken,
          body: attempt.address, headers: { 'Idempotency-Key': key } });
        attempt.orderId = checkout.orderId; await persist();
      }
      let order;
      for (let poll = 0; poll < 60; poll++) {
        order = await call(`/api/orders/${attempt.orderId}`, { token: tokens.accessToken });
        if (['Shipped', 'Delivered', 'Cancelled'].includes(order.status)) break;
        await delay(1000);
      }
      if (Math.round(order.totalAmount * 100) !== attempt.expectedCents || order.customerId !== attempt.ownerId ||
        (attempt.expectedStatus === 'Cancelled' ? order.status !== 'Cancelled' || order.sagaStatus !== 'Cancelled' : !['Shipped', 'Delivered'].includes(order.status)))
        throw new Error(`Order state, amount or ownership mismatch: ${key}`);
      if (attempt.expectedStatus !== 'Cancelled') {
        let shipment = await call(`/api/shipping/orders/${attempt.orderId}`, { token: admin.accessToken });
        const stages = ['Created', 'ReadyForPickup', 'InTransit', 'Delivered'];
        while (stages.indexOf(shipment.status) < stages.indexOf(attempt.targetShipmentStatus))
          shipment = await call(`/api/shipping/${shipment.id}/advance`, { method: 'POST', token: admin.accessToken });
        const payment = await call(`/api/payments/orders/${attempt.orderId}`, { token: admin.accessToken });
        if (payment.status !== 'Succeeded' || Math.round(payment.amount * 100) !== attempt.expectedCents) throw new Error(`Payment mismatch: ${key}`);
        attempt.shipmentStatus = shipment.status;
      }
      attempt.status = order.status; attempt.verified = true; await persist();
      await call('/api/cart', { method: 'DELETE', token: tokens.accessToken });
      await call('/api/auth/logout', { method: 'POST', body: { refreshToken: tokens.refreshToken } });
      if ((index + 1) % 10 === 0) {
        console.log(`Verified checkout workflows: ${index + 1}/120`);
        // Keep setup authentication within the existing short-lived JWT window.
        admin = await login(adminEmail);
      }
    }
  }

  const catalog = await pageAll('/api/catalog/products', 'total');
  const liveCategories = await call('/api/catalog/categories');
  const committedOrders = Object.values(state.orders).filter((order) => order.verified);
  if (new Set(committedOrders.map((order) => order.orderId)).size !== committedOrders.length)
    throw new Error('Duplicate order identities were found in the checkpoint.');
  let verifiedUsers = 0;
  let verifiedLiveOrders = 0;
  let verifiedIdempotentReplays = 0;
  if (mode === 'verify') {
    const users = await pageAll('/api/auth/users', 'totalCount');
    for (const account of accounts) {
      const user = users.find((entry) => entry.email === account.email);
      if (!user || user.id !== state.users[account.email]?.id || !user.roles.includes(account.role))
        throw new Error(`Demo account identity or role mismatch: ${account.email}`);
      verifiedUsers++;
    }
    if (committedOrders.length !== 120) throw new Error('Finish all 120 checkout workflows before the final verification.');
    for (const attempt of committedOrders) {
      const order = await call(`/api/orders/admin/${attempt.orderId}`, { token: admin.accessToken });
      if (order.customerId !== attempt.ownerId || Math.round(order.totalAmount * 100) !== attempt.expectedCents || order.currency !== 'USD')
        throw new Error('Persisted order ownership, currency or amount changed.');
      if (attempt.expectedStatus === 'Cancelled') {
        if (order.status !== 'Cancelled' || order.sagaStatus !== 'Cancelled') throw new Error('Stock failure did not complete cancellation.');
        const payment = await call(`/api/payments/orders/${attempt.orderId}`, { token: admin.accessToken, allow: [404] });
        const shipment = await call(`/api/shipping/orders/${attempt.orderId}`, { token: admin.accessToken, allow: [404] });
        if (payment.httpStatus !== 404 || shipment.httpStatus !== 404) throw new Error('A stock-rejected order unexpectedly has payment or shipping.');
      } else {
        const payment = await call(`/api/payments/orders/${attempt.orderId}`, { token: admin.accessToken });
        const shipment = await call(`/api/shipping/orders/${attempt.orderId}`, { token: admin.accessToken });
        const expectedOrderStatus = attempt.targetShipmentStatus === 'Delivered' ? 'Delivered' : 'Shipped';
        if (order.status !== expectedOrderStatus || payment.status !== 'Succeeded' || payment.currency !== 'USD' ||
          Math.round(payment.amount * 100) !== attempt.expectedCents || shipment.status !== attempt.targetShipmentStatus)
          throw new Error('Persisted order, payment and shipment no longer agree.');
      }
      verifiedLiveOrders++;
    }
    // Replay completed and cancelled owner-scoped requests with empty carts; durable keys must return the original order.
    for (const number of [1, 4, 10]) {
      const key = `${manifest.version}-order-${String(number).padStart(3, '0')}`;
      const attempt = state.orders[key];
      const tokens = await login(attempt.email);
      const replay = await call('/api/orders/checkout', { method: 'POST', token: tokens.accessToken,
        body: attempt.address, headers: { 'Idempotency-Key': key } });
      if (replay.orderId !== attempt.orderId) throw new Error('Durable checkout replay created a different order.');
      await call('/api/auth/logout', { method: 'POST', body: { refreshToken: tokens.refreshToken } });
      verifiedIdempotentReplays++;
    }
  }
  const sold = new Map();
  for (const order of committedOrders.filter((entry) => entry.expectedStatus !== 'Cancelled'))
    for (const item of order.items) sold.set(item.sku, (sold.get(item.sku) || 0) + item.quantity);
  let verifiedStockRows = 0;
  let verifiedImages = 0;
  for (const product of manifest.products) {
    const live = catalog.find((row) => row.sku === product.sku);
    if (!live || live.imageUrl !== product.imageUrl || live.categorySlug !== product.categorySlug || live.status !== 'Active' ||
      live.brand !== product.brand || live.name !== product.name || live.priceAmount !== product.priceAmount || live.priceCurrency !== product.priceCurrency)
      throw new Error(`Catalog verification failed: ${product.sku}`);
    const stock = await call(`/api/inventory/${live.id}`, { token: admin.accessToken });
    if (stock.quantityOnHand !== product.initialStock - (sold.get(product.sku) || 0) || stock.reservedQuantity !== 0)
      throw new Error(`Stock does not reconcile to demo receipts and confirmed orders: ${product.sku}. Review any additional activity instead of replacing stock.`);
    verifiedStockRows++;
    const photos = galleries.get(product.sku) || [product];
    if (galleries.has(product.sku) && JSON.stringify(live.imageUrls) !== JSON.stringify(photos.map((photo) => photo.imageUrl)))
      throw new Error(`Persisted gallery does not match verified source photos: ${product.sku}`);
    for (const photo of photos) {
      await delay(Math.max(0, 225 - (Date.now() - lastRequest))); lastRequest = Date.now();
      const image = await fetch(base + photo.imageUrl, { signal: AbortSignal.timeout(30_000) });
      if (!image.ok || !image.headers.get('content-type')?.startsWith('image/') ||
        createHash('sha256').update(Buffer.from(await image.arrayBuffer())).digest('hex') !== photo.imageSha256)
        throw new Error(`Gateway image verification failed: ${product.sku}`);
      verifiedImages++;
    }
    if (verifiedStockRows % 50 === 0) console.log(`Reconciled stock and photos: ${verifiedStockRows}/200`);
  }
  for (const category of manifest.categories) {
    const live = liveCategories.find((group) => group.slug === category.slug);
    if (!live || live.productCount !== category.productCount) throw new Error(`Category count mismatch: ${category.slug}`);
  }
  const summaries = mode === 'catalog' ? {} : {
    orders: await call('/api/orders/summary', { token: admin.accessToken }),
    payments: await call('/api/payments/summary', { token: admin.accessToken }),
    shipping: await call('/api/shipping/summary', { token: admin.accessToken }),
  };
  const report = { verifiedAtUtc: new Date().toISOString(), sourceCapturedAtUtc: manifest.capturedAtUtc,
    importedProducts: 200, catalogTotal: catalog.length, categories: liveCategories, verifiedStockRows, verifiedImages,
    multiplePhotoModels: [...galleries.values()].filter((photos) => photos.length > 1).length,
    verifiedUsers, verifiedLiveOrders, verifiedIdempotentReplays,
    demoUsers: Object.keys(state.users).length, demoOrders: Object.values(state.orders).filter((order) => order.verified).length,
    demoOrderOutcomes: Object.values(state.orders).reduce((counts, order) => { const outcome = order.expectedStatus === 'Cancelled' ? 'Cancelled' : order.shipmentStatus || 'Pending'; counts[outcome] = (counts[outcome] || 0) + 1; return counts; }, {}), summaries };
  await writeFile(resolve(stateDirectory, 'verification.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
  await call('/api/auth/logout', { method: 'POST', body: { refreshToken: admin.refreshToken } });
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => { console.error(error.message); process.exitCode = 1; });
}

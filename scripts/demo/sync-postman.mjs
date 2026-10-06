import { readFile, writeFile } from 'node:fs/promises';
const target = new URL('../../docs/postman/commerce-microservices-api.postman_collection.json', import.meta.url);
const collection = JSON.parse(await readFile(target, 'utf8'));
const manifest = JSON.parse(await readFile(new URL('../../demo/catalog.json', import.meta.url), 'utf8'));
const catalog = collection.item.find((folder) => folder.name === 'Catalog');
const names = ['Public collections', 'Administrative collections', 'Create collection', 'Rename or disable collection', 'Product photograph', 'Upload product photograph'];
catalog.item = catalog.item.filter((item) => !names.includes(item.name));
const browse = catalog.item.find((item) => item.name === 'Browse products');
browse.request.url = '{{baseUrl}}/api/catalog/products?status=Active&page=1&pageSize=20&sort=name&category={{categorySlug}}';
browse.request.description = 'Public through YARP. Filter by an exact collection slug; clear categorySlug for all products. Responses add nullable brand, imageUrl, sourceUrl and categorySlug.';
for (const item of catalog.item.filter((entry) => entry.request.body)) {
  const body = JSON.parse(item.request.body.raw);
  body.brand = 'Reference brand'; body.categorySlug = '{{categorySlug}}'; body.imageUrls = ['{{imageUrl}}'];
  item.request.body.raw = JSON.stringify(body, null, 2);
  item.request.description = (item.request.method === 'POST' ? 'Requires catalog.products.create.' : 'Requires catalog.products.update.') +
    ' All traffic uses YARP. Category must exist and be active for a new assignment. imageUrls holds up to eight ordered Catalog paths, first is primary; omitted/null preserves, [] clears the gallery. imageUrl remains backward compatible. sourceUrl accepts HTTPS. This sample replaces the gallery with imageUrl captured after an upload or product detail.';
}
const request = (name, method, path, body, permission) => ({ name, request: { method, url: '{{baseUrl}}' + path,
  header: body ? [{ key: 'Content-Type', value: 'application/json' }] : [],
  description: permission === 'public' ? 'Public through YARP.' : 'Requires ' + permission + ' through YARP.',
  ...(permission === 'public' ? { auth: { type: 'noauth' } } : {}),
  ...(body ? { body: { mode: 'raw', raw: JSON.stringify(body, null, 2), options: { raw: { language: 'json' } } } } : {}) },
  event: [{ listen: 'test', script: { type: 'text/javascript', exec: ["pm.test('No server error', () => pm.expect(pm.response.code).to.be.below(500));"] } }] });
catalog.item.push(
  request('Public collections', 'GET', '/api/catalog/categories', null, 'public'),
  request('Administrative collections', 'GET', '/api/catalog/categories?includeInactive=true', null, 'catalog.products.update'),
  request('Create collection', 'POST', '/api/catalog/categories', { name: 'Reference collection', slug: '{{newCategorySlug}}' }, 'catalog.products.create'),
  request('Rename or disable collection', 'PUT', '/api/catalog/categories/{{categorySlug}}', { name: 'Keyboards', isActive: true }, 'catalog.products.update'),
  request('Product photograph', 'GET', '{{imageUrl}}', null, 'public'),
);
catalog.item.push({ name: 'Upload product photograph', request: {
  method: 'POST', url: '{{baseUrl}}/api/catalog/images',
  description: 'Requires catalog.products.create OR catalog.products.update. Select a real JPEG, PNG or WebP file (max 5 MB). The browser/Postman supplies the multipart boundary. Returns an immutable shared-volume path; assign it through imageUrls when saving the product.',
  body: { mode: 'formdata', formdata: [{ key: 'file', type: 'file', src: '' }] },
}, event: [{ listen: 'test', script: { type: 'text/javascript', exec: [
  "pm.test('Photo uploaded', () => pm.response.to.have.status(201));",
  "if (pm.response.code === 201) pm.collectionVariables.set('imageUrl', pm.response.json().imageUrl);",
] } }] });
const detail = catalog.item.find((item) => item.name === 'Product details');
detail.event = [{ listen: 'test', script: { type: 'text/javascript', exec: [
  "pm.test('Product exists', () => pm.response.to.have.status(200));",
  "if (pm.response.code === 200 && pm.response.json().imageUrl) pm.collectionVariables.set('imageUrl', pm.response.json().imageUrl);",
] } }];
for (const [key, value] of [['categorySlug', 'keyboards'], ['newCategorySlug', 'reference-collection'], ['imageUrl', manifest.products[0].imageUrl]]) {
  if (!collection.variable.some((variable) => variable.key === key)) collection.variable.push({ key, value, type: 'string' });
  else if (key === 'imageUrl' && !collection.variable.find((variable) => variable.key === key).value)
    collection.variable.find((variable) => variable.key === key).value = value;
}
await writeFile(target, JSON.stringify(collection, null, 2) + '\n');
console.log('Updated the importable Gateway-only Postman collection; no credentials or tokens were added.');

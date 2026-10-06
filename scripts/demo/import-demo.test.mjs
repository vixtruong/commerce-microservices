import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { validateManifest, plannedItems, totalCents, demoAccounts } from './import-demo.mjs';
import { classify } from './prepare-catalog.mjs';
import { createHash } from 'node:crypto';
const manifest = JSON.parse(await readFile(new URL('../../demo/catalog.json', import.meta.url), 'utf8'));

test('source galleries retain the primary model photo and all 589 shipped asset checksums', async () => {
  const galleries = JSON.parse(await readFile(new URL('../../demo/galleries.json', import.meta.url), 'utf8'));
  assert.equal(galleries.products.length, 200);
  assert.equal(galleries.products.filter((row) => row.images.length > 1).length, 195);
  assert.equal(galleries.products.reduce((sum, row) => sum + row.images.length, 0), 589);
  for (const row of galleries.products) {
    assert.equal(row.images[0].imageUrl, manifest.products.find((model) => model.sku === row.sku).imageUrl);
    assert.equal(new Set(row.images.map((image) => image.imageSha256)).size, row.images.length);
    for (const image of row.images) {
      const asset = new URL('../../src/Services/Catalog/Catalog.Api/wwwroot/demo-products/' + image.imageUrl.split('/').at(-1), import.meta.url);
      assert.equal(createHash('sha256').update(await readFile(asset)).digest('hex'), image.imageSha256);
    }
  }
});

test('the shipped catalog has 200 unique models/photos and correct group counts', () => {
  assert.equal(validateManifest(manifest).products.length, 200);
  assert.equal(new Set(manifest.products.map((product) => product.imageSha256)).size, 200);
  assert.equal(manifest.categories.reduce((sum, category) => sum + category.productCount, 0), 200);
});
test('unsafe media and duplicate models are rejected before writes', () => {
  const copy = structuredClone(manifest); copy.products[0].imageUrl = '/api/catalog/media/../../secret.png';
  assert.throws(() => validateManifest(copy));
  copy.products[0] = copy.products[1]; assert.throws(() => validateManifest(copy));
});
test('planned order totals use cents and quantities; successful carts cannot exhaust demo stock', () => {
  assert.equal(totalCents([{ priceAmount: 19.99, quantity: 3 }, { priceAmount: 0.1, quantity: 3 }]), 6027);
  const sold = new Map(); let cancelled = 0;
  for (let index = 0; index < 120; index++) {
    const items = plannedItems(manifest.products, index);
    if (index % 10 === 9) { assert.equal(items[0].initialStock, 0); cancelled++; }
    else for (const item of items) sold.set(item.sku, (sold.get(item.sku) || 0) + item.quantity);
  }
  for (const product of manifest.products) assert.ok((sold.get(product.sku) || 0) <= product.initialStock, product.sku);
  assert.equal(cancelled, 12);
});
test('sixty synthetic accounts stay in reserved test domains with established role bundles', () => {
  const accounts = demoAccounts(); assert.equal(accounts.length, 60);
  assert.equal(new Set(accounts.map((account) => account.email)).size, 60);
  assert.ok(accounts.every((account) => account.email.endsWith('@demo.example.test')));
  assert.equal(accounts.filter((account) => account.role === 'Customer').length, 52);
});
test('classification separates connection adapters and input devices from misleading title words', () => {
  assert.equal(classify('USB C to Lightning Audio Adapter'), 'cables');
  assert.equal(classify('Wireless Custom Number Pad'), 'mice');
  assert.equal(classify('Universal Mouse Grip Tape'), 'workspace');
  assert.equal(classify('10000mAh Magnetic Power Bank'), 'power-banks');
});

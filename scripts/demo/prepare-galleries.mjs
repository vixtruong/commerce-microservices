import { readFile, writeFile, readdir, unlink } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const target = resolve(root, 'src/Services/Catalog/Catalog.Api/wwwroot/demo-products');

/** Add source-matched alternate views without changing model, variant, price or the original import checkpoint. */
async function main() {
  const manifest = JSON.parse(await readFile(resolve(root, 'demo/catalog.json'), 'utf8'));
  const sources = new Map();
  for (const vendor of new Set(manifest.products.map((product) => product.sourceVendor)))
    sources.set(vendor, JSON.parse(await readFile(resolve(root, `artifacts/demo-sources/${vendor}.json`), 'utf8')).products);
  const products = [];
  for (let offset = 0; offset < manifest.products.length; offset += 4) {
    const rows = await Promise.all(manifest.products.slice(offset, offset + 4).map(async (product) => {
      const source = sources.get(product.sourceVendor).find((entry) => String(entry.id) === product.sourceProductId);
      if (!source) throw new Error(`Source model missing: ${product.sku}`);
      const images = [{ imageUrl: product.imageUrl, imageSha256: product.imageSha256, imageBytes: product.imageBytes, imageSourceUrl: product.imageSourceUrl }];
      for (const candidate of source.images.filter((image) => !image.variant_ids?.length || image.variant_ids.map(String).includes(product.sourceVariantId))) {
        if (images.length === 3) break;
        if (images.some((image) => image.imageSourceUrl === candidate.src)) continue;
        const url = new URL(candidate.src); url.searchParams.set('width', '960');
        let response;
        for (let attempt = 0; ; attempt++) {
          try { response = await fetch(url, { signal: AbortSignal.timeout(45_000) }); if (!response.ok) throw new Error(`Image HTTP ${response.status}`); break; }
          catch (error) { if (attempt === 2) throw error; }
        }
        const extension = { 'image/jpeg': 'jpg', 'image/png': 'png', 'image/webp': 'webp', 'image/avif': 'avif' }[response.headers.get('content-type')?.split(';')[0]];
        const bytes = Buffer.from(await response.arrayBuffer());
        if (!extension || bytes.length < 1000 || bytes.length > 8_000_000) throw new Error(`Invalid alternate photo: ${product.sku}`);
        const sha256 = createHash('sha256').update(bytes).digest('hex');
        if (images.some((image) => image.imageSha256 === sha256)) continue;
        const filename = `${product.sku.toLowerCase()}-${images.length + 1}.${extension}`;
        await writeFile(resolve(target, filename), bytes);
        images.push({ imageUrl: `/api/catalog/media/${filename}`, imageSha256: sha256, imageBytes: bytes.length, imageSourceUrl: candidate.src });
      }
      return { sku: product.sku, images };
    }));
    products.push(...rows);
    if (products.length % 20 === 0) console.log(`Source galleries: ${products.length}/200`);
  }
  const activeFiles = new Set(products.flatMap((product) => product.images.map((image) => image.imageUrl.split('/').at(-1))));
  for (const filename of await readdir(target)) {
    const path = resolve(target, filename);
    if (/^demo-(keychron|ugreen|baseus|satechi)-\d+-[2-8]\.(jpg|png|webp|avif)$/.test(filename) && !activeFiles.has(filename) && dirname(path) === target)
      await unlink(path);
  }
  const galleries = { version: 'tech-demo-gallery-v1', baseVersion: manifest.version, preparedAtUtc: new Date().toISOString(), products };
  await writeFile(resolve(root, 'demo/galleries.json'), JSON.stringify(galleries, null, 2) + '\n');
  console.log(JSON.stringify({ models: products.length, multiplePhotoModels: products.filter((product) => product.images.length > 1).length,
    photos: products.reduce((total, product) => total + product.images.length, 0) }));
}

main().catch((error) => { console.error(error.message); process.exitCode = 1; });

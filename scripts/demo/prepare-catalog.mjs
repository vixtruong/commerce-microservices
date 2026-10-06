import { readFile, writeFile, mkdir, readdir, unlink } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const sources = [
  { key: 'keychron', brand: 'Keychron', origin: 'https://www.keychron.com', quota: 50 },
  { key: 'ugreen', brand: 'UGREEN', origin: 'https://us.ugreen.com', quota: 60 },
  { key: 'baseus', brand: 'Baseus', origin: 'https://www.baseus.com', quota: 55 },
  { key: 'satechi', brand: 'Satechi', origin: 'https://satechi.net', quota: 35 },
];
const groups = {
  keyboards: ['Keyboards', 'Bàn phím cơ và bàn phím không dây cho góc làm việc.'],
  mice: ['Mice & input', 'Chuột, bút và thiết bị điều khiển cho công việc hằng ngày.'],
  audio: ['Audio', 'Tai nghe và loa phục vụ nghe nhạc, gọi điện và làm việc.'],
  chargers: ['Chargers', 'Sạc có dây, sạc không dây và bộ sạc để bàn.'],
  'power-banks': ['Power banks', 'Pin dự phòng cho điện thoại, máy tính bảng và thiết bị di động.'],
  'hubs-docks': ['Hubs & docks', 'Mở rộng kết nối màn hình, thiết bị USB và mạng tại bàn làm việc.'],
  cables: ['Cables & adapters', 'Cáp sạc, cáp dữ liệu và đầu chuyển kết nối thiết bị.'],
  storage: ['Storage & networking', 'Thiết bị lưu trữ, đọc thẻ và kết nối mạng.'],
  workspace: ['Workspace accessories', 'Giá đỡ, phụ kiện bàn làm việc và hỗ trợ mang theo thiết bị.'],
};

/** Classify the actual model name; never add capabilities absent from the source title. */
export function classify(title, type = '') {
  const name = title.toLowerCase();
  if (/power.?bank|portable charger|battery pack/.test(name)) return 'power-banks';
  if (/keyboard/.test(name)) return 'keyboards';
  if (/grip tape|mouse pad|mousepad/.test(name)) return 'workspace';
  if (/mouse|mice|trackpad|stylus|presenter|number pad|macro pad/.test(name)) return 'mice';
  if (/audio adapter|headphone.*adapter/.test(name)) return 'cables';
  if (/earbud|headphone|headset|speaker|earphone|audio|ear clip/.test(name)) return 'audio';
  if (/dock|\bhub\b|multiport|multi-port|revodok/.test(name)) return 'hubs-docks';
  if (/ssd|nvme|enclosure|card reader|ethernet|network|nas\b|hard drive/.test(name)) return 'storage';
  if (/cable|adapter|converter|hdmi switch|displayport/.test(name)) return 'cables';
  if (/charg|power strip|gan|power station/.test(name)) return 'chargers';
  if (/stand|holder|sleeve|desk|pad|mount|findall|tracker|light/.test(name)) return 'workspace';
  if (type && type !== title) return classify(type);
  return null;
}

/** Interleave groups so a source's newest items do not crowd out its smaller departments. */
function balancedSelection(candidates, count) {
  const queues = Object.keys(groups).map((slug) => candidates.filter((item) => item.categorySlug === slug));
  const chosen = [];
  while (chosen.length < count && queues.some((queue) => queue.length)) {
    for (const queue of queues) {
      if (queue.length && chosen.length < count) chosen.push(queue.shift());
    }
  }
  if (chosen.length !== count) throw new Error(`Only ${chosen.length} eligible products for quota ${count}.`);
  return chosen;
}

/** Download matching product assets locally so the customer demo needs no third-party image requests. */
async function downloadImage(product, target) {
  const url = new URL(product.imageSourceUrl);
  url.searchParams.set('width', '960');
  const response = await fetch(url, { signal: AbortSignal.timeout(45_000) });
  if (!response.ok) throw new Error(`Image HTTP ${response.status}: ${product.sku}`);
  const contentType = response.headers.get('content-type')?.split(';')[0];
  const extension = { 'image/jpeg': 'jpg', 'image/png': 'png', 'image/webp': 'webp', 'image/avif': 'avif' }[contentType];
  if (!extension) throw new Error(`Unexpected image type ${contentType}: ${product.sku}`);
  const bytes = Buffer.from(await response.arrayBuffer());
  if (bytes.length < 1000 || bytes.length > 8_000_000) throw new Error(`Unexpected image size: ${product.sku}`);
  const filename = `${product.sku.toLowerCase()}.${extension}`;
  await writeFile(resolve(target, filename), bytes);
  product.imageUrl = `/api/catalog/media/${filename}`;
  product.imageSha256 = createHash('sha256').update(bytes).digest('hex');
  product.imageBytes = bytes.length;
}

async function main() {
  const products = [];
  for (const source of sources) {
    const catalog = JSON.parse(await readFile(resolve(root, `artifacts/demo-sources/${source.key}.json`), 'utf8'));
    const candidates = catalog.products.flatMap((item) => {
      const variant = item.variants.find((entry) => Number(entry.price) > 0 && entry.available);
      const categorySlug = classify(item.title, item.product_type);
      if (!variant || !categorySlug || !item.images.length ||
        /gift card|gift set|replacement|barebone|switch tester|refurbish|open.box|mystery|bundle|keycap|switch set|switches|palm rest|charger.*[+&].*cable|charger.*&.*power bank|power bank.*&.*charger/.test(item.title.toLowerCase())) return [];
      const name = `${item.title}${variant.title && variant.title !== 'Default Title' ? ` — ${variant.title}` : ''}`.replace(/\s+/g, ' ').trim();
      if (name.length > 200) return [];
      // A real, available variant gives a coherent price/image pair rather than a fabricated configuration.
      const image = item.images.find((entry) => entry.variant_ids?.includes(variant.id)) ?? item.images[0];
      const sku = `DEMO-${source.key.toUpperCase()}-${item.id}`;
      return [{ sku, name, brand: source.brand, categorySlug, priceAmount: Number(variant.price), priceCurrency: 'USD',
        description: `${name}. ${groups[categorySlug][1]} Chọn đúng chuẩn kết nối và thiết bị tương thích theo thông tin của hãng trước khi mua. Giá USD tham khảo tại thời điểm chuẩn bị demo.`,
        sourceUrl: `${source.origin}/products/${item.handle}?variant=${variant.id}`,
        sourceProductId: String(item.id), sourceVariantId: String(variant.id), sourceSku: variant.sku,
        imageSourceUrl: image.src, sourceVendor: source.key,
        initialStock: 25 + (Number(item.id) % 76) }];
    });
    const selected = balancedSelection(candidates, source.quota);
    products.push(...selected);
    console.log(`${source.brand}: ${selected.length} / ${candidates.length} eligible models`);
  }
  // Small, visible low-stock and sold-out slices support truthful operational queues and failed-checkout demos.
  products.slice(-3).forEach((product) => { product.initialStock = 0; });
  products.slice(-11, -3).forEach((product, index) => { product.initialStock = 3 + index % 4; });
  const target = resolve(root, 'src/Services/Catalog/Catalog.Api/wwwroot/demo-products');
  await mkdir(target, { recursive: true });
  // Bound downloads to four concurrent requests; a failed asset aborts the manifest before database import.
  let completed = 0;
  for (let index = 0; index < products.length; index += 4) {
    await Promise.all(products.slice(index, index + 4).map(async (product) => {
      for (let attempt = 0; ; attempt++) {
        try { await downloadImage(product, target); break; }
        catch (error) { if (attempt === 2) throw error; }
      }
      completed++;
    }));
    if (completed % 20 === 0) console.log(`Verified images: ${completed}/200`);
  }
  if (new Set(products.map((product) => product.imageSha256)).size !== 200)
    throw new Error('The chosen models must have 200 distinct verified product photos.');
  const activeFiles = new Set(products.map((product) => product.imageUrl.split('/').at(-1)));
  for (const filename of await readdir(target)) {
    const asset = resolve(target, filename);
    // Only remove assets this preparer owns, with an absolute-parent check before deleting a file.
    if (/^demo-[a-z0-9-]+\.(jpg|png|webp|avif)$/.test(filename) && !activeFiles.has(filename) && dirname(asset) === target)
      await unlink(asset);
  }
  const categories = Object.entries(groups).map(([slug, [name, description]]) => ({ slug, name, description,
    productCount: products.filter((product) => product.categorySlug === slug).length })).filter((group) => group.productCount > 0);
  const manifest = { version: 'tech-demo-v1', capturedAtUtc: new Date().toISOString(), currency: 'USD',
    note: 'Real vendor models and photographs; prices are a dated reference. Accounts, stock and orders are demo scenarios.',
    categories, products };
  await mkdir(resolve(root, 'demo'), { recursive: true });
  await writeFile(resolve(root, 'demo/catalog.json'), JSON.stringify(manifest, null, 2) + '\n');
  console.log(JSON.stringify({ products: products.length, categories, imageMegabytes: products.reduce((sum, product) => sum + product.imageBytes, 0) / 1_000_000 }, null, 2));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => { console.error(error.message); process.exitCode = 1; });
}

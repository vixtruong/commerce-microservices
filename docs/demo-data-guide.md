# Hướng dẫn bộ demo công nghệ và phụ kiện

## Mở và trình bày

- Cửa hàng: http://localhost:8088
- Danh mục: http://localhost:8088/products
- Nhóm sản phẩm: http://localhost:8088/admin/collections
- Quản trị và số liệu vận hành: http://localhost:8088/admin

Đăng nhập quản trị bằng `ADMIN_EMAIL` / `ADMIN_PASSWORD` trong `.env` local. Những giá trị này không được đưa vào tài liệu, Postman hoặc manifest.

Bộ dữ liệu bổ sung gồm **200 model/phụ kiện**, **9 nhóm**, **52 khách hàng demo + 8 nhân viên demo** và **120 đơn checkout**. Sản phẩm cũ, tài khoản cũ và đơn hàng cũ được giữ lại, nên tổng số của toàn hệ thống có thể lớn hơn các số này.

Có **589 ảnh nguồn hãng**: 195 model có nhiều ảnh, 5 model chỉ có một ảnh phù hợp với variant được chọn. `demo/galleries.json` lưu thứ tự ảnh, nguồn và SHA-256; ảnh đầu tiên là ảnh chính.

Nguồn tên/model, cấu hình được chọn, ảnh và giá USD tham khảo là danh mục công khai của [Keychron](https://www.keychron.com), [UGREEN US](https://us.ugreen.com), [Baseus](https://www.baseus.com) và [Satechi](https://satechi.net). Manifest `demo/catalog.json` lưu nguồn từng sản phẩm, variant, SKU gốc, thời điểm lấy dữ liệu và SHA-256 của ảnh. Không dùng review, discount, stock hoặc mức bán chạy từ nguồn để tạo số liệu giả trong ứng dụng.

Đây là **demo**, không phải dữ liệu giao dịch hay khách hàng thật ngoài đời. Tồn kho được khởi tạo để phục vụ kịch bản; tên và địa chỉ được tạo riêng cho demo, email dùng miền `.example.test`. Giá USD là ảnh chụp tham khảo tại thời điểm chuẩn bị, không phải báo giá bán tại Việt Nam. Payment và Shipping vẫn dùng provider development hiện có. Ảnh giữ đúng model từ nguồn hãng; rà soát quyền sử dụng trước khi chuyển bộ tài sản demo sang cửa hàng bán hàng công khai.

## Nhóm sản phẩm (collection)

Collection trên website là **Category của Catalog**, không phải collection Postman. Mỗi sản phẩm được gán tối đa một nhóm, dùng slug ổn định để URL lọc có thể chia sẻ.

| Nhóm | SKU trong bộ dữ liệu |
| --- | ---: |
| Keyboards | 38 |
| Mice & input | 19 |
| Audio | 17 |
| Chargers | 24 |
| Power banks | 27 |
| Hubs & docks | 29 |
| Cables & adapters | 26 |
| Storage & networking | 12 |
| Workspace accessories | 8 |

1. Vào **Admin → Collections**. Tạo nhóm với tên và slug như `keyboards`; slug chỉ dùng chữ thường, số và dấu gạch nối.
2. Vào **Products → Edit → Collection**, chọn nhóm rồi lưu. Hãng có thể được chỉnh cùng form. Ảnh và nguồn tham khảo đã nhập được giữ nguyên nếu chỉ sửa các trường hiện có.
3. Trang chủ hiển thị nhóm có sản phẩm active; Catalog có bộ lọc nhóm, tìm kiếm, giá, sort và phân trang. Số lượng do Catalog tính trong SQL.
4. Đổi tên nhóm không đổi slug. **Disable** ẩn nhóm khỏi điều hướng cửa hàng và chặn gán mới; sản phẩm, đơn hàng và các liên kết lọc đã có vẫn được giữ. **Enable** mở lại nhóm.

Quyền dùng các policy Catalog hiện có: `catalog.products.create` để tạo nhóm, `catalog.products.update` để chỉnh nhóm/xem cả nhóm disabled. Role `CatalogManager` có cả hai quyền. Backend kiểm tra quyền và trạng thái nhóm, không chỉ dựa vào menu UI.

## Thêm, đổi và quản lý ảnh

Trong **Products → Create/Edit → Product photos**, chọn nhiều file JPEG/PNG/WebP (tối đa 8 ảnh, 5 MiB mỗi ảnh). Xem trước, chọn **Make primary** hoặc **Remove**, rồi **Save product**. Chỉ khi lưu mới upload và gán ảnh; form giữ cảnh báo thay đổi chưa lưu. Ảnh chính xuất hiện trong catalog/cart, trang chi tiết có thumbnail để chuyển ảnh. Cart đọc metadata Catalog hiện tại nên giỏ đã lưu cũng nhận ảnh mới sau khi tải lại.

API `POST /api/catalog/images` nhận multipart `file`, kiểm tra quyền create/update, kích thước khai báo/thực tế và chữ ký định dạng. Storage ghi stream với buffer 64 KiB, checksum SHA-256 và publish bằng rename nguyên tử; file tạm được dọn nếu bị hủy hoặc lỗi. Tên file do server tạo. Đường dẫn public cùng origin, không cho SVG/HTML hoặc ảnh remote tùy ý. `imageUrls` có thứ tự, null/omitted giữ gallery, [] xóa gán; `imageUrl` vẫn tương thích client cũ.

Ảnh upload lưu trong named volume **catalog-images**, dùng chung cho cả hai Catalog; recreate container giữ ảnh. Khi chạy ngoài Compose có thể cấu hình `ProductImages__Directory` thành thư mục dùng chung có quyền ghi. Cần backup volume cùng database. Bỏ ảnh khỏi gallery chỉ bỏ gán, không xóa tài sản có thể đang được sản phẩm khác dùng chung.

## Tài khoản và kịch bản đơn hàng

Tài khoản demo dùng mật khẩu từ `DEMO_CUSTOMER_PASSWORD`, hoặc `CUSTOMER_PASSWORD` trong `.env` local khi không có override. ASP.NET Core Identity thực hiện hashing; script không lưu mật khẩu hoặc token vào checkpoint.

- Khách hàng ví dụ: `minh.nguyen.01@demo.example.test`, `linh.nguyen.02@demo.example.test`.
- Nhân viên: `catalogmanager.1@demo.example.test`, `warehousemanager.1@demo.example.test`, `ordermanager.1@demo.example.test`, `supportagent.1@demo.example.test`. Mỗi role có hai tài khoản.
- 40 khách hàng có lịch sử mua; 12 khách hàng còn lại để demo tài khoản chưa có đơn. Giỏ hàng được dọn sau từng checkout; tài khoản có thể mua tiếp từ UI.
- 108 đơn thanh toán thành công, có snapshot giá/tên/SKU và shipment thật trong database. Shipping được tiến từng bước bằng API hiện có để có Created, ReadyForPickup, InTransit và Delivered.
- 12 đơn yêu cầu sản phẩm hết hàng, được Inventory/Saga hủy đúng luồng; không chèn trực tiếp trạng thái vào database. Ba SKU hết hàng có stock row 0 qua receipt và adjustment có audit. Tám SKU có tồn thấp để trình bày hàng đợi bổ sung kho.
- Không backdate lịch sử: timestamp phản ánh lúc dữ liệu được tạo và sự kiện thực sự xử lý.

Gợi ý trình bày: mở Home → chọn nhóm → tìm/sort/phân trang → xem ảnh/model/stock → đăng nhập khách hàng → xem lịch sử → đăng nhập Admin → xem dashboard, stock, payments và shipments → sửa collection/gán nhóm trong product editor. Có thể mở tài khoản nhân viên để minh họa các menu theo quyền.

## Nhập lại trên máy khác hoặc tiếp tục sau gián đoạn

Trong thư mục backend, với `.env` local đã cấu hình và frontend sibling sẵn sàng:

```powershell
docker compose build catalog-api-1 catalog-api-2 commerce-web
docker compose up -d catalog-api-1 catalog-api-2 commerce-web
node scripts/demo/import-demo.mjs all
node scripts/demo/import-demo.mjs verify
```

Migration `AddProductPresentation` và `AddProductImageGallery` được Catalog Development áp dụng khi container khởi động. Gallery migration giữ ảnh chính của dữ liệu cũ. Production cần áp dụng migration bằng quy trình triển khai của dự án. Không cần reset volumes.

Importer chỉ gọi Gateway local. Có các mode `catalog`, `orders`, `all`, `verify`. Có thể truyền `DEMO_BASE_URL`, `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD`, `DEMO_CUSTOMER_PASSWORD` qua environment; mặc định đọc cấu hình local, không dùng credential hard-code.

Checkpoint nằm ở `artifacts/demo-state/checkpoint.local.json` và report ở `artifacts/demo-state/verification.json`, được Git ignore. Giữ checkpoint sau gián đoạn: SKU được đối chiếu, stock row ngăn nhận kho lần hai, email được tìm lại, và mỗi checkout giữ owner/key/payload đã lưu trước khi submit. Những đơn đã xác minh được bỏ qua khi chạy lại. Không tự retry mutations; lỗi mạng dừng để người vận hành chạy lại có kiểm soát. Chỉ chạy một importer cho cùng môi trường tại một thời điểm.

Checkpoint khóa vào manifest và Gateway đã dùng. Khi đổi manifest hoặc thay database, dùng môi trường demo riêng với checkpoint riêng; không xóa checkpoint chỉ để né báo lỗi dữ liệu. Nếu có hoạt động mua/điều chỉnh kho thêm sau bộ nhập, bước verify tồn kho sẽ yêu cầu rà soát thay vì âm thầm ghi đè số lượng.

Ảnh hãng nằm trong `Catalog.Api/wwwroot/demo-products`; ảnh upload nằm trong volume/thư mục cấu hình. Cả hai được phục vụ qua `/api/catalog/media/<file>` bởi YARP. Preview file dùng blob URL chỉ trong browser; CSP cho phép blob tại img-src. `prepare-catalog.mjs` và `prepare-galleries.mjs` chuẩn bị bộ mới từ snapshot nguồn; không cần chạy để nhập lại bộ đã kèm repository. `verify` đối chiếu 589 ảnh, 200 stock row, 60 role/identity, 120 order/payment/shipment và replay 3 khóa checkout để bảo đảm trả về đúng đơn đã có.

## Kiểm chứng và Postman

```powershell
node scripts/demo/import-demo.test.mjs
dotnet test Commerce.slnx --no-restore -m:1 -nr:false
```

Frontend chạy `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`, `pnpm build-storybook`; Playwright demo chạy với `DEMO_E2E=true` và `WEB_BASE_URL=http://localhost:8088`, dùng credential từ `.env.e2e` ignored hoặc `E2E_ENV_FILE`.

Bản [Postman import](postman/commerce-microservices-api.postman_collection.json) có 51 request, bổ sung category filter, DTO ảnh/gallery/nhóm, API collection, multipart upload và đọc ảnh qua Gateway. Credential/token để trống. Postman MCP không khả dụng trong phiên này, nên bản collection remote chưa được đồng bộ; import bản này vào workspace khi cần.

Kết quả thực tế đã được ghi ở `docs/demo-data-plan.md` và `../Commerce.Web/docs/frontend-redesign-plan.md`. Lượt cuối xác minh đủ 60 tài khoản, 120 đơn, 200 stock row, 589 ảnh và 3 replay checkout đúng đơn cũ; 56 backend, 48 frontend, 6 dataset và 11 browser tests đều qua. Hai migration đã áp dụng local. Reload trang web sau cập nhật để dùng bundle mới.

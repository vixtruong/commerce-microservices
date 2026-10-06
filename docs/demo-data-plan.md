# Bộ dữ liệu demo công nghệ và phụ kiện

## Mục tiêu và nguồn dữ liệu

Tạo 200 SKU công nghệ/phụ kiện có tên model thật, ảnh đúng sản phẩm, giá tham khảo USD và nguồn đối chiếu. Ưu tiên danh mục công khai của Keychron, UGREEN, Baseus và Satechi; không nhân bản sản phẩm bằng cách bịa cấu hình. Giá được chụp tại thời điểm nhập, không phải cam kết giá bán ở Việt Nam.

Người dùng, địa chỉ, tồn kho và giao dịch là dữ liệu demo được lưu thật trong các dịch vụ. Không dùng thông tin cá nhân của khách hàng ngoài đời. Đơn hàng chạy qua Gateway, Cart, Ordering, Inventory, Payment và Shipping hiện có; không chèn trực tiếp những trạng thái Saga chưa xảy ra.

## Triển khai

1. Chuẩn bị manifest nguồn, phân nhóm sản phẩm và ảnh lưu tại Catalog, phục vụ qua YARP cùng origin để demo ổn định.
2. Bổ sung metadata ảnh, hãng và nhóm sản phẩm trong aggregate/DTO Catalog; migration tương thích với sản phẩm cũ. Bổ sung API nhóm sản phẩm có kiểm tra quyền và gán nhóm trong form quản trị.
3. Hiển thị nhóm có số lượng thật ở Home và bộ lọc Catalog. URL giữ nhóm cùng search/sort/price/page; thay nhóm hoặc reset phải về trang đầu. Ảnh lỗi có fallback rõ ràng.
4. Tạo công cụ nhập qua Gateway có kiểm tra manifest, lưu checkpoint và idempotency đơn hàng. Không xóa dữ liệu hiện có, không tăng tồn kho lại khi chạy lại, không ghi mật khẩu/token vào repository.
5. Nhập 200 sản phẩm, khoảng 60 tài khoản demo gồm khách hàng và nhân viên theo role hiện có, 120 đơn hàng với nhiều giỏ hàng, thành phố và trạng thái shipping được hỗ trợ.
6. Đồng bộ collection Postman từ source. Nếu MCP không khả dụng, cập nhật bản import trong repository và ghi rõ hạn chế đồng bộ remote.
7. Chạy build/tests, kiểm tra ảnh và phân nhóm, tổng tiền/ownership/idempotency/tồn kho; kiểm tra giao diện 375/768/1440px và ghi kết quả thực tế.

## Tiêu chí nghiệm thu

- Đúng 200 SKU trong manifest, không trùng nguồn/model hoặc ảnh hỏng; tất cả sản phẩm có nhóm, hãng, nguồn, ảnh và giá hợp lệ.
- Số lượng nhóm và dashboard lấy từ API; người dùng demo đăng nhập được; đơn hàng có payment/shipment và stock nhất quán.
- Chạy lại công cụ không nhân đôi sản phẩm, tài khoản, đơn hàng hoặc lượng stock nhận ban đầu.
- Giữ dữ liệu và thay đổi đang có ở hai repository. Không reset database/container volumes.
- Lưu hướng dẫn tiếng Việt để trình bày demo, quản lý nhóm và nhập lại trên máy khác.

## Kết quả

Hoàn thành nhập dữ liệu và triển khai local ngày 06/10/2026; phần đối chiếu cuối được lưu trong `artifacts/demo-state/verification.json` (ignored).

Lượt `verify` cuối lúc **2026-10-06 04:50:41 UTC** xác minh trực tiếp **60 user/role, 120 order/payment/shipment, 200 stock row, 589 ảnh** và **3 durable checkout replays** trả đúng order ID. Toàn hệ thống có 228 Catalog records và 147 orders vì giữ dữ liệu cũ cùng các fixture kiểm tra; 200/60/120 là số của bộ demo bổ sung. Summary không còn order processing.

| Hạng mục | Kết quả |
| --- | --- |
| Sản phẩm | 200 model nguồn hãng, 50 Keychron / 60 UGREEN / 55 Baseus / 35 Satechi; giữ sản phẩm cũ |
| Ảnh | 589 ảnh có source/checksum; 195 model có nhiều ảnh, 5 model một ảnh phù hợp variant |
| Collection | 9 nhóm, số lượng SQL thật; Home/filter URL/admin tạo/đổi tên/bật/tắt/gán nhóm |
| Tài khoản | 52 khách hàng + 8 nhân viên, role Identity, email reserved, password từ env |
| Đơn hàng | 120 checkout qua các dịch vụ; 108 payment succeeded, 12 stock-rejected cancellations |
| Fulfilment | Created 30 / ReadyForPickup 24 / InTransit 30 / Delivered 24 / Cancelled 12 |
| Nhập lặp | Chạy lại `all`: vẫn 200/60/120; không nhận kho thêm, không tạo đơn lại |
| Tồn kho và ảnh | 200 dòng stock khớp receipt trừ lượng đã bán, reserved=0; 589 ảnh qua Gateway khớp SHA-256 |
| Upload và gallery | Tối đa 8 JPEG/PNG/WebP, 5 MiB/ảnh, preview/primary/remove; storage stream 64 KiB, atomically publish, shared volume |
| Cart | Đọc metadata Catalog hiện tại để cả giỏ đã lưu nhận ảnh mới, giữ snapshot tiền/quantity |
| Persistence ảnh upload | Recreate Catalog đầu tiên; file/checksum vẫn còn trong volume, ảnh đang gán vẫn đọc đúng qua Gateway |
| Backend | Build qua; 56 tests domain/application/architecture/integration qua |
| Frontend | Lint/typecheck/build/Storybook qua; 48 tests trong 20 file qua |
| Dataset | 6 tests kiểm tra model, gallery/hash, phân nhóm, account, cents/stock bounds qua |
| Browser | 11 Playwright tests qua nginx/Gateway; review 375/768/1440px, 95 screenshot local ignored |

Bổ sung `CategoryService`, `ProductImageService`, `IProductImageStore` và file store Infrastructure; Domain giữ quy tắc gallery/nhóm. Hai migration `AddProductPresentation` và `AddProductImageGallery` đã áp dụng ở local, gallery migration bảo toàn ảnh cũ. REST/DTO Catalog mở rộng tương thích; gRPC, integration events, RabbitMQ routing/queues không đổi. Browser vẫn qua YARP.

Postman source copy có 51 request qua Gateway, gồm group/filter/gallery/upload; credential/token trống. Postman MCP không có trong phiên, nên chưa đồng bộ remote. Build chỉ có warning annotation Zod/size chunk Storybook hiện có. Không thêm dependency runtime.

Lượt browser đầu thiếu `E2E_ENV_FILE`; lượt tiếp theo phát hiện ảnh hưởng danh mục lớn đến tìm kiếm seed và một lỗi select nhóm tải chậm. Đã sửa test tìm theo SKU ổn định và form controlled selection, thêm regression test. Unit test multipart dùng mock fetch do môi trường jsdom/MSW không truyền File stream ổn định; luồng multipart thật, permissions, size/type rejection, create/edit/photo replacement và Cart được kiểm chứng bằng Playwright.

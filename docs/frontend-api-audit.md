# Frontend API audit

Source reviewed before implementation: all service controllers, application handlers/ports,
aggregate lifecycles, EF mappings/migrations, seeds, integration contracts, Saga consumers,
health/security middleware, Compose, smoke scripts and architecture/domain tests.

All paths below are public YARP routes. No browser request uses an internal service.

| UI requirement | Gateway route | Owner / input / output | Authorization | Audit result |
| --- | --- | --- | --- | --- |
| Login / register | POST /api/auth/login, /register | Identity credentials → TokenResponse | public | Exists |
| Refresh / logout | POST /api/auth/refresh, /logout | opaque refresh token → rotated tokens / 204 | token possession | Exists |
| Account / users | GET /api/auth/me, /users, /users/{id} | Identity → safe user DTOs | subject / users.read | Added |
| Role administration | PUT /api/auth/users/{id}/roles | role array → 204 | users.roles.manage | Added, disallow self change; revoke refresh sessions |
| Permission bundles / audit | GET /api/auth/roles, /permissions, /access/audit; PUT /roles/{id}/permissions | native role claims → safe bundles/audit | users.roles.manage | Added; protected system and actor bundles |
| Catalog | GET /api/catalog/products | query → ProductPageResponse (existing `total`) | public | Pagination/search exist; sort/status/price added |
| Product | GET /api/catalog/products/{id} | ProductResponse | public | Exists |
| Product editing | POST /api/catalog/products; PUT /{id}; POST /{id}/activate, /deactivate | validated product requests | catalog.products.create/update/deactivate | Existing mutations; permission policies added |
| Cart | GET /api/cart; PUT /items/{id}; DELETE /items/{id}; DELETE /api/cart | quantity → CustomerCart | authenticated subject | Exists |
| Checkout | POST /api/orders/checkout | address + Idempotency-Key → CheckoutResponse, 202 | authenticated subject | Exists; durable replay before cart validation |
| Order | GET /api/orders/{id} | OrderResponse | owner OR orders.read | Resource handler; address/workflow/shipment snapshot added |
| History / operations | GET /api/orders; /admin; /admin/{id}; /summary | bounded query → page / summary | subject / orders.read | Added |
| Stock | GET /api/inventory/{id}; POST /{id}/receipts | StockItemResponse | inventory.read / inventory.adjust | Exists |
| Availability | GET /api/inventory/{id}/availability | product ID → available units | public | Added, no reservation metadata |
| Inventory operations | GET /api/inventory; /summary; /{id}/reservations; POST /{id}/adjustments | query / signed delta+reason+version | inventory.read / inventory.reservations.read / inventory.adjust | Added |
| Payments | GET /api/payments/orders/{id} | existing payment projection | payments.read | Exists |
| Payment operations | GET /api/payments; /{id}; /summary | bounded query → page / summary | payments.read | Added |
| Shipping | GET /api/shipping/{id}; /orders/{id}; POST /{id}/advance | persisted shipment | shipments.read / shipments.update | Reads restricted; customers use owner-authorized Ordering snapshot |
| Shipment operations | GET /api/shipping; /summary | bounded query → page / summary | shipments.read | Added |
| System | GET /health/ready | Gateway readiness | public | Exists |

Existing `Customer` and `Admin` role names remain, plus four editable staff presets. Roles bundle permissions.
JWT claims are `sub`, `email`, `role` and `permission` (one or many). Policies use the centralized permission catalog.
Access tokens last 15 minutes (30 seconds validation tolerance); refresh tokens rotate atomically, reject replay and expire after seven days.
Refresh resolves current permissions; user membership edits revoke refresh sessions. See authorization.md for the complete matrix.
Ordering persists AwaitingInventory/AwaitingPayment/Paid/Compensating/Cancelled/ShipmentCreated/Completed Saga states.
Order Shipped currently means ShipmentCreated (Shipping can still be Created), so the customer wording is “Shipment created”.
Cancellation reasons include insufficient-stock, inventory-reservation-expired and fake provider failure/timeout.
Compensation must finish before displaying “stock released”. No timestamps for unpersisted intermediate transitions are fabricated.

No image upload, carrier integration, card collection, refund operation, password change or editable profile exists.
Notification is an internal durable fake email worker, with no public customer endpoint.
Categories exist as independent entities but products are not assigned to them.
Dashboard summaries stay inside each database; currencies are grouped instead of summed together.

Postman MCP is not available in this session. An importable collection is maintained under docs/postman;
remote collection synchronization must be completed when the connector is available.

# Full-stack implementation and validation

Validated locally on 2026-10-02 against the existing checkout and persisted development data.

1. **Architecture.** One feature-oriented application under `../Commerce.Web` provides separate Storefront and backoffice layouts. React calls YARP through Vite/nginx only. Application read ports and service-owned EF projections supply bounded pages and SQL summaries. Gateway, internal gRPC, RabbitMQ, Outbox/Inbox and service database boundaries are preserved. See [frontend architecture](frontend-architecture.md).

2. **Frontend stack.** React 19, strict TypeScript, Vite, React Router, TanStack Query/Table, Zustand for UI state, React Hook Form, Zod, Tailwind CSS, Radix Dialog, Lucide and Recharts. Vitest, Testing Library, MSW, Playwright and Storybook support verification. ESLint and Prettier provide separate lint and formatting responsibilities. pnpm 10.17.1 and Node 22 are configured.

3. **Routes.** Storefront: `/`, `/products`, `/products/:id`, `/login`, `/register`, `/cart`, `/checkout`, `/checkout/processing/:orderId`, `/checkout/success/:orderId`, `/orders`, `/orders/:orderId`, `/account`, `/account/profile`. Backoffice: `/admin`, product list/create/edit, inventory overview/detail, order/payment/shipment list/detail, user access, role bundles, access audit and system readiness. The complete route/permission table is in [frontend-architecture.md](frontend-architecture.md).

4. **Backend use cases.** Added current-user/user administration, role permission management and access audit; catalog status/sort/price filtering; customer/admin order history, summary and shipment snapshots; public availability, stock pages/summaries/reservations and audited version-checked adjustments; payment/shipment pages and summaries. Existing product, cart and checkout use cases are reused. Input DTO validation is strengthened. The [API audit](frontend-api-audit.md) maps exact Gateway routes and ownership. New migrations are `20261002083833_AddShipmentSnapshot` (Ordering), `20261002084133_AddStockAdjustmentAudit` (Inventory), and `20261002084134_AddAccessChangeAudit` (Identity). Development Compose applied them; production needs a controlled migration release step.

5. **Authentication.** Access JWT stays in memory; the opaque refresh credential is tab-scoped in sessionStorage. Reload restores the session. Concurrent 401s share one renewal; stale responses cannot repopulate a signed-out account. Identity atomically rotates refresh tokens with one concurrent winner. Replay returns 401. Login/refresh resolve current roles and permissions. Explicit logout clears private queries and checkout attempts.

6. **Authorization.** Existing Admin/Customer names are preserved. CatalogManager, WarehouseManager, OrderManager and SupportAgent are editable permission presets stored in native Identity role claims. Effective permissions are a distinct union. ASP.NET Core policies enforce permissions; an order resource handler permits the owner or `orders.read`. Unrelated customer reads return 404. React guards, navigation and actions use the same permission names. Protected system/actor bundles and self membership changes are rejected. Access changes are audited; membership edits revoke refresh sessions. Already-issued access claims expire after 15 minutes with 30 seconds of clock tolerance. See [authorization.md](authorization.md).

7. **Checkout and Saga.** An owner-scoped idempotency key and immutable address are saved before POST. Double submission, network retry, remount and session expiry retain the same attempt. The accepted order enters a processing page; two-second polling follows persisted Saga/order state and stops at terminal state, error or the two-minute limit. Confirmation waits for shipment creation. Payment cancellation waits for compensation before claiming stock release. The timeline displays creation and latest recorded facts without inventing intermediate timestamps.

8. **Admin capabilities.** Permission-scoped dashboards, product create/edit/publication, live product stock status, stock adjustment with reason/version/confirmation, reservation history, order/payment/shipment inspection, shipment advancement, user role assignment, grouped permission editing, access audit, command navigation and local observability links. Tables use bounded server pages, URL filters, column visibility and mobile row layouts. High-impact mutations have confirmation and inline errors.

9. **Tests created.** Backend permission/ownership and stock-invariant tests; frontend auth races, session generation, permission guards/navigation, validation, optimistic cart rollback, idempotency, status/formatting, filter reset and chart boundaries. Five real-service browser scenarios cover customer checkout/access denial, admin product/stock management, permission changes/refresh replay/concurrent refresh/ownership, insufficient stock with no payment, and responsive screens. Test products are deactivated and retained for audit; isolated customer accounts preserve existing carts.

10. **Docker.** Added a multi-stage frontend image and healthy `commerce-web` service on port 8088. Unprivileged nginx serves the SPA, proxies only YARP, supplies a content security policy and caches hashed assets. Existing volumes were preserved. Final state: all 18 containers running; the 14 services with healthchecks are healthy. Grafana, Prometheus and Jaeger endpoints return 200; the collector is running with no recent error/fatal entries. Payment is restored to `Success`.

11. **CI.** `.github/workflows/validate.yml` checks .NET restore/build/tests, frozen frontend installation, lint/types/tests/build/Storybook, Compose smoke and real Playwright flows, failed-payment compensation and browser artifact upload. GitHub-hosted execution was not triggered from this session; equivalent local checks were executed.

12. **Commands executed.** Principal checks are listed below. Targeted image rebuilds followed later Identity/frontend changes; payment-only recreation switched failure mode and restored Success without deleting volumes.

    ```powershell
    dotnet restore Commerce.slnx
    dotnet build Commerce.slnx --no-restore
    dotnet test Commerce.slnx --no-build --no-restore -m:1
    pnpm --dir ../Commerce.Web install --frozen-lockfile
    pnpm --dir ../Commerce.Web lint
    pnpm --dir ../Commerce.Web typecheck
    pnpm --dir ../Commerce.Web test
    pnpm --dir ../Commerce.Web build
    pnpm --dir ../Commerce.Web build-storybook
    docker compose config --quiet
    docker compose up --build -d --wait --wait-timeout 300
    ./scripts/smoke-test.ps1
    ./scripts/smoke-test-payment-failure.ps1
    $env:WEB_BASE_URL = 'http://localhost:8088'
    pnpm --dir ../Commerce.Web test:e2e
    docker compose ps
    git diff --check
    ```

13. **Results.** .NET build succeeds with zero errors/warnings; all 40 backend tests pass. Frontend lint/types/build and Storybook build pass; all 32 frontend unit/component tests pass. All five production browser scenarios passed in Success mode, and all five passed in Failure mode (final run: 44.6 seconds). Happy and failed-payment smoke scripts passed. Failure checks confirm cancelled order, completed compensation, zero reserved units and unchanged physical stock. Insufficient stock starts no payment. Gateway readiness, frontend health/page and nginx-to-YARP catalog requests return 200. All 12 required screens were visually reviewed at 375, 768 and 1440 pixels; screenshots are under ignored `artifacts/frontend-visual`. Responsive browser assertions pass. A tablet price wrap was corrected. Source review excludes secrets and generated outputs. Nonfatal Rollup warnings originate in third-party Zod annotations/Storybook vendor size. Log inspection identified the existing logger's 500 entries for browser-aborted Identity database reads as `OperationCanceledException`; these were cancelled navigation requests, while active reads and browser assertions passed.

14. **Deliberate boundaries.** No unsupported refund, admin order cancellation, image upload, editable profile, password change, carrier integration or category assignment controls are shown. Product media uses explicit local sample illustrations; payment remains the development provider. Customer shipment information is the persisted Ordering creation snapshot, not a carrier feed. Direct user permission overrides/deny rules and an ABAC engine were optional and are absent. English is delivered with shared copy/status catalogs and Intl formatting; Vietnamese translation and an offline PWA shell are not added. Authenticated API responses are not cached offline. The importable [Postman collection](postman/commerce-microservices-api.postman_collection.json) contains 45 Gateway-only requests with blank credentials; remote collection inspection/synchronization could not run because no Postman MCP tool is available.

15. **Next improvements.** For production deployment, introduce a secure HttpOnly-cookie BFF, real payment/media/carrier capabilities with their own contracts and tests, a persisted transition history if a complete timestamped timeline is needed, complete translations, and refine aborted-request logging. Synchronize the source Postman collection when the connector is available, and review the first hosted CI run.

Open the [Storefront](http://localhost:8088) or [backoffice](http://localhost:8088/admin). Development credentials and startup/failure verification instructions remain in the root [README](../README.md).

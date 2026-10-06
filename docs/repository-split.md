# Separate backend and frontend repositories

| Repository | Responsibility | Local checkout |
| --- | --- | --- |
| [commerce-microservices](https://github.com/vixtruong/commerce-microservices) | .NET services, contracts, authorization, persistence, migrations, full-stack Compose and integration CI | `D:\vixtruong\Commerce` |
| [commerce-web](https://github.com/vixtruong/commerce-web) | React Storefront/backoffice, frontend tests, Storybook, nginx image and frontend CI | `D:\vixtruong\Commerce.Web` |

The frontend repository is public and starts on `main`. Backend work is grouped on `codex/full-stack-commerce` for review before merging. The frontend was previously untracked, so extraction preserves its source as a new independent history without rewriting backend history.

## Business commit boundaries

| Capability | Backend changes | Frontend changes |
| --- | --- | --- |
| Shared application foundation | Bounded page/summary contracts | React toolchain, shared controls, Query state and Gateway client |
| Authentication/authorization | Permission policies, ownership handlers, Identity role claims, refresh rotation and access audit | Sign-in/session restore, permission helpers/guards, user and role management |
| Catalog | Product query filters/sort and permission-protected mutations | Storefront catalog/details and admin product forms/publication |
| Cart | Existing service contracts preserved | Server cart, optimistic changes and rollback |
| Ordering | Owner/admin history, summaries and shipment snapshots | Stable checkout attempts, Saga processing, history and order details |
| Inventory | Audited version-checked adjustment, queries, summaries and reservations | Availability, stock overview/detail and adjustment confirmation |
| Payment | Permission-protected bounded payment queries/summaries | Development-provider payment records/details |
| Shipping | Permission-protected shipment queries/summaries and advancement | Shipment overview/detail and status advancement |
| Operations | Gateway response metadata/CORS and health integration | Permission-scoped dashboard, readiness and local tooling links |
| Delivery/testing | Compose source path, smoke scripts, backend/cross-repository CI | nginx image, browser tests, frontend CI and manual backend-revision E2E |
| Documentation | API/authorization/migration/Postman documentation | Standalone setup, configuration and frontend agent rules |

Commits use Conventional Commits and include tests alongside the related capability. No unrelated source changes are combined into a single catch-all commit.

## Run and verify

Clone both repositories beside each other. Compose resolves `FRONTEND_SOURCE_PATH` relative to the backend directory; the default is `../Commerce.Web`. The frontend only sends requests to YARP. CI supplies an explicit frontend checkout path and does not add a submodule or copy frontend source into backend history.

Frontend E2E credentials come from ignored `.env.e2e`, environment variables or `E2E_ENV_FILE`. Screenshots stay under the frontend's ignored `artifacts/frontend-visual`. The original local frontend, including its generated dependencies, was preserved under backend `artifacts/frontend-before-repository-split`; it is not committed or published.

Validation after extraction includes frozen dependency installation, frontend lint/types/32 tests/production build, a frontend-only Compose rebuild and all five real Gateway browser scenarios. The previous backend build and all 40 tests remain documented in [implementation-validation.md](implementation-validation.md). Local `.env`, tokens, generated builds, dependency folders and browser artifacts are excluded from both histories.

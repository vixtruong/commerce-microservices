# ADR 0007: Redis has bounded responsibilities

Status: Accepted

Redis is Catalog's cache-aside store, Cart's expiring primary store, and a short-lived coordinator for checkout idempotency and token-safe distributed locks. Keys are purpose-prefixed and versioned. Redis is not a load balancer and is not the primary correctness mechanism for Inventory; PostgreSQL transactions, unique constraints, and optimistic concurrency protect stock.

# ADR 0008: PostgreSQL is the relational provider

Status: Accepted

PostgreSQL and Npgsql provide one portable relational stack for every service-owned database. Migrations, unique idempotency constraints, indexes, JSON Outbox payloads, and optimistic concurrency mappings remain local to each service. Docker creates the logical databases automatically while production can provision separate servers without code changes.

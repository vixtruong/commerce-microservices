# ADR 0004: Database per service

Status: Accepted

Each relational bounded context owns a PostgreSQL logical database, DbContext, and migrations. Local Docker uses one PostgreSQL server for convenience, but services never query another service's schema and no cross-service foreign keys exist. Cross-context data is represented by identifiers and immutable snapshots.

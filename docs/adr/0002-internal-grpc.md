# ADR 0002: gRPC is internal synchronous RPC only

Status: Accepted

Use gRPC only when a service needs an immediate authoritative answer: Cart reads Catalog product snapshots, and Ordering reads Cart and Catalog checkout snapshots. Public clients never see protobuf contracts. Calls have deadlines, bounded retries only for idempotent reads, and circuit breakers. Integration events do not use gRPC.

# ADR 0005: Transactional Outbox and Inbox

Status: Accepted

Relational business changes and outgoing Outbox rows commit together. A multi-instance-safe processor claims and publishes rows, then records confirmation. Consumers write a unique Inbox marker in the same transaction as their mutation. This provides reliable at-least-once messaging without making an incorrect exactly-once claim.

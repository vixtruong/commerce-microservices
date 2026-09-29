# ADR 0003: RabbitMQ carries transactional integration events

Status: Accepted

Business facts cross service boundaries through versioned primitive contracts on the durable `commerce.events` topic exchange. Messages are persistent and publisher-confirmed. Consumers use manual acknowledgements, bounded retry queues, and dead-letter queues. RabbitMQ delivery is at least once, so every state-changing handler must be idempotent.

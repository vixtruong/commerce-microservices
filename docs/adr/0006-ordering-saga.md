# ADR 0006: Ordering orchestrates checkout

Status: Accepted

Ordering owns an explicit persisted process manager because checkout state is easiest to teach and inspect in one place. Inventory, Payment, and Shipping remain autonomous event-driven services. Failures never use a distributed transaction: Payment failure cancels the Order and requests Inventory release; Inventory failure cancels without requesting payment.

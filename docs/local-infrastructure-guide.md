# Local infrastructure and observability guide

This guide covers PostgreSQL GUI access and the Docker-provisioned Grafana, Prometheus, Jaeger, RabbitMQ, OpenTelemetry, Redis, health, and log workflows.

## Start or update the stack

Create `.env` once if it does not exist:

```powershell
Copy-Item .env.example .env
```

After changing a published port, apply the Compose configuration:

```powershell
docker compose up -d
docker compose ps
```

The normal URLs are:

| Component | Address | Authentication |
| --- | --- | --- |
| Commerce Gateway | <http://localhost:8080> | JWT for protected APIs |
| PostgreSQL | `127.0.0.1:${POSTGRES_PORT:-5432}` | `POSTGRES_USER` / `POSTGRES_PASSWORD` from `.env` |
| Redis | `127.0.0.1:${REDIS_PORT:-6379}` | None in local development |
| RabbitMQ Management | <http://localhost:15672> | `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS` from `.env` |
| Jaeger | <http://localhost:16686> | None in local development |
| Prometheus | <http://localhost:9090> | None in local development |
| Grafana | <http://localhost:3000> | `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` from `.env` |

`POSTGRES_PORT` defaults to `5432` and `REDIS_PORT` defaults to `6379`. If either port is occupied, set a different host port in `.env`, then recreate the corresponding container.

## Connect a database management application

The same PostgreSQL server hosts seven isolated logical databases:

- `commerce_identity`
- `commerce_catalog`
- `commerce_inventory`
- `commerce_ordering`
- `commerce_payment`
- `commerce_shipping`
- `commerce_notification`

Use these settings in DBeaver, pgAdmin, JetBrains DataGrip, or another PostgreSQL client:

| Setting | Value |
| --- | --- |
| Driver/type | PostgreSQL |
| Host | `127.0.0.1` |
| Port | Value of `POSTGRES_PORT` in `.env`; default `5432` |
| Database | Start with `postgres` or one of the `commerce_*` databases |
| Username | Value of `POSTGRES_USER` in `.env` |
| Password | Value of `POSTGRES_PASSWORD` in `.env` |
| SSL | Disable for this local Docker connection |

For DBeaver, enable **Show all databases** in the PostgreSQL connection settings if one connection should display all seven databases. Otherwise, duplicate the connection and select one `commerce_*` database per bounded context.

Validate the published port from PowerShell:

```powershell
docker compose port postgres 5432
Test-NetConnection 127.0.0.1 -Port 5432
```

If `POSTGRES_PORT` is not `5432`, use that value in `Test-NetConnection`.

List databases without installing a local PostgreSQL client:

```powershell
docker compose exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d postgres -c "\l"'
```

Useful read-only starting points:

```sql
-- Execute in commerce_ordering.
SELECT "Id", "OrderNumber", "Status", "CreatedAtUtc"
FROM ordering.orders
ORDER BY "CreatedAtUtc" DESC
LIMIT 20;

SELECT "Id", "Type", "OccurredOnUtc", "ProcessedOnUtc", "RetryCount"
FROM ordering.outbox_messages
ORDER BY "OccurredOnUtc" DESC
LIMIT 20;

SELECT "MessageId", "Consumer", "ProcessedOnUtc"
FROM ordering.inbox_messages
ORDER BY "ProcessedOnUtc" DESC
LIMIT 20;
```

Table and column names are quoted because EF Core uses case-sensitive PostgreSQL identifiers. Do not edit another service's database to simulate an API operation; use the Gateway or Postman so domain rules and integration events still run.

## Generate useful telemetry

Dashboards and traces are empty until the application receives traffic. Run the end-to-end smoke test:

```powershell
./scripts/smoke-test.ps1
```

For continuous sample traffic, repeat safe reads in another terminal:

```powershell
1..30 | ForEach-Object {
    Invoke-RestMethod http://localhost:8080/api/catalog/products | Out-Null
    Start-Sleep -Milliseconds 500
}
```

## Grafana

1. Open <http://localhost:3000>.
2. Sign in with `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` from `.env`.
3. Open **Dashboards → Commerce → Commerce System Overview**.
4. Use the **Service**, **Route**, and **Queue** filters to select the component under investigation.
5. Run the smoke test or sample traffic and refresh the dashboard.

The provisioned dashboard contains:

- system-wide traffic, p95 latency, HTTP error rate, service count, and Prometheus scrape health;
- Gateway inbound routes and outbound calls to internal services;
- per-service traffic, route latency, service-to-service calls, and exceptions;
- .NET process CPU, working-set memory, GC pressure, thread-pool queue, and replica count;
- OpenTelemetry Collector throughput, failed/refused signals, exporter queue, and resource usage;
- RabbitMQ queue-level ready/unacknowledged messages, consumers, retry/dead-letter backlog, message flow, and broker resources.

The Prometheus data source is provisioned automatically as the default and points to `http://prometheus:9090` inside Docker. No manual data-source URL is required.

To investigate a metric directly:

1. Open **Explore**.
2. Select the **Prometheus** data source.
3. Enter one of the PromQL queries from the next section.
4. Switch between table and time-series views and adjust the time range.

## Prometheus

Open <http://localhost:9090>.

### Check scrape health

Open **Status → Target health** (or `/targets`). All four targets should be `UP`:

- `commerce-otel` scrapes the collector's Prometheus exporter.
- `otel-collector-internal` scrapes the collector's own pipeline and process telemetry.
- `rabbitmq` scrapes aggregate RabbitMQ broker metrics.
- `rabbitmq-per-object` scrapes queue-level metrics from `/metrics/per-object`.

Start troubleshooting with:

```promql
up
```

### Useful Commerce queries

HTTP request rate by service:

```promql
sum by (exported_job) (
  rate(http_server_request_duration_seconds_count[5m])
)
```

HTTP p95 latency by service:

```promql
histogram_quantile(
  0.95,
  sum by (le, exported_job) (
    rate(http_server_request_duration_seconds_bucket[5m])
  )
)
```

RabbitMQ ready messages by queue:

```promql
sum by (queue) (rabbitmq_queue_messages_ready{job="rabbitmq-per-object"})
```

RabbitMQ unacknowledged messages by queue:

```promql
sum by (queue) (rabbitmq_queue_messages_unacked{job="rabbitmq-per-object"})
```

RabbitMQ consumers by queue:

```promql
sum by (queue) (rabbitmq_queue_consumers{job="rabbitmq-per-object"})
```

Prometheus is the metric query and storage layer; Grafana is the preferred dashboard and visualization layer.

## Jaeger

Open <http://localhost:16686>.

The current all-in-one local container uses transient in-memory trace storage. Restarting or recreating the Jaeger container clears existing traces, so use it for live debugging rather than audit retention.

### Inspect a normal API request

1. Generate traffic through the Gateway.
2. In **Search**, choose service `Commerce.Gateway`.
3. Choose a recent lookback such as **Last 15 minutes**.
4. Click **Find Traces** and open a trace.
5. Expand spans to inspect Gateway time, downstream service time, status, HTTP route, and errors.

### Inspect the asynchronous checkout

1. Run `./scripts/smoke-test.ps1`.
2. Search `Commerce.Gateway` or `Ordering.Api` for the same time window.
3. Open the checkout trace around `POST /api/orders/checkout`.
4. Follow the `Commerce.Messaging` producer and consumer spans through Ordering, Inventory, Payment, Shipping, and Notification.
5. Compare span duration and tags to determine whether time was spent in HTTP handling, gRPC reads, database work, or asynchronous consumers.

Because W3C trace context is propagated in RabbitMQ headers, related asynchronous work can remain in the same distributed trace. A missing downstream span usually indicates that the consumer did not receive the message, telemetry export failed, or the selected time window is too narrow.

## RabbitMQ Management

Open <http://localhost:15672> and sign in with the RabbitMQ credentials from `.env`.

Recommended views:

- **Overview**: connection, channel, publish, delivery, acknowledgement, and queue rates.
- **Exchanges**: inspect `commerce.events` and `commerce.events.dlx` bindings.
- **Queues and Streams**: inspect ready/unacknowledged counts, consumers, and message rates.
- **Connections/Channels**: verify application containers are connected.

Every business subscription has a main queue, a retry queue, and a dead-letter queue. Names ending in `.retry` hold delayed retries; names ending in `.dead` require investigation. Avoid purging or publishing messages manually unless deliberately testing recovery behavior because those actions change workflow state.

## OpenTelemetry Collector

The collector has no user interface. It receives application telemetry on private Docker ports `4317` (gRPC) and `4318` (HTTP), batches it, then:

- exports traces to Jaeger on private port `4317`;
- exposes collected metrics to Prometheus on private port `8889`.

Inspect collector behavior with:

```powershell
docker compose logs --tail=100 otel-collector
```

Common failures are an invalid collector configuration, an unavailable Jaeger container, or applications using an incorrect `OTEL_EXPORTER_OTLP_ENDPOINT`.

## Redis

Redis is published only on the local loopback interface so RedisInsight, Navicat, or another local management client can inspect development data without exposing Redis to the LAN.

| Setting | Value |
| --- | --- |
| Host | `127.0.0.1` |
| Port | Value of `REDIS_PORT` in `.env`; default `6379` |
| Authentication | None in local development |

Validate the host connection or inspect Redis inside the container:

```powershell
Test-NetConnection 127.0.0.1 -Port 6379
docker compose exec redis redis-cli PING
docker compose exec redis redis-cli --scan --pattern 'catalog:product:v1:*'
docker compose exec redis redis-cli --scan --pattern 'cart:v1:*'
docker compose exec redis redis-cli --scan --pattern 'idempotency:v1:*'
```

Avoid `KEYS *` on realistic datasets because it blocks Redis. Use `SCAN` through `--scan` as shown above.

## Health checks and logs

Gateway health endpoints:

```powershell
Invoke-WebRequest http://localhost:8080/health/live
Invoke-WebRequest http://localhost:8080/health/ready
```

Container and structured application logs:

```powershell
docker compose ps
docker compose logs --tail=100 postgres prometheus grafana jaeger otel-collector
docker compose logs -f commerce-gateway ordering-api inventory-api payment-api shipping-api
```

Use the signals together:

1. **Health/readiness** answers whether a process can serve traffic.
2. **Logs** explain discrete errors and business events.
3. **Prometheus/Grafana** show rates, latency, saturation, and trends.
4. **Jaeger** explains one request or workflow across process boundaries.
5. **RabbitMQ Management** explains queue backlogs, retries, dead letters, and consumers.
6. **PostgreSQL/Redis inspection** confirms durable or cached state after the higher-level signals identify the relevant service.

## Troubleshooting checklist

### Database client cannot connect

```powershell
docker compose ps postgres
docker compose port postgres 5432
docker compose logs --tail=100 postgres
Test-NetConnection 127.0.0.1 -Port 5432
```

- Confirm the GUI uses the host port, not Docker's internal service name `postgres`.
- Confirm the username and password come from the active `.env` file.
- If another local server owns port `5432`, change `POSTGRES_PORT` and recreate PostgreSQL with `docker compose up -d postgres`.
- Do not run `docker compose down -v` unless deleting all local data is intended.

### Grafana has no data

- Generate requests first.
- Set the dashboard time range to include those requests.
- Check Prometheus `/targets` and confirm `commerce-otel` is `UP`.
- Query `up` and then the raw request-duration metric in Prometheus.
- Inspect `otel-collector` and application logs for export errors.

### Jaeger has no traces

- Generate a new Gateway request and search the correct time window.
- Check that the service selector contains the expected .NET application name.
- Inspect `otel-collector` and `jaeger` logs.
- Confirm application containers have `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317`.

### RabbitMQ queue is growing

- Verify the queue has a consumer.
- Inspect `.retry` and `.dead` queues.
- Inspect the owning consumer's logs and Jaeger spans.
- Check its PostgreSQL Inbox/Outbox rows before replaying or deleting messages.

# Postman

Import `commerce-microservices-api.postman_collection.json` into Postman. Set `baseUrl` to the YARP Gateway (default `http://localhost:8080`). Credentials and tokens are deliberately blank. Use development credentials from the root README locally; do not synchronize production credentials or tokens.

Login/registration/refresh scripts update the collection's local bearer and refresh variables. Current profile, product creation and checkout capture returned IDs. Choose an appropriate administrator or customer login for each request; permissions and ownership are described on every request. A Customer cannot read administrative payment or shipment endpoints.

Checkout creates `idempotencyKey` once. Repeat the exact request to verify replay. Clear that variable explicitly to start a new attempt after reviewing the previous order. Stock adjustment must use the latest `version` from Stock details; a stale version returns 409. Role edits replace the entire bundle and are audited.

The Postman MCP server is unavailable in this session, so the remote **Commerce Microservices API** collection could not be inspected or synchronized. This importable source copy covers the implemented public controllers and contains no internal gRPC or RabbitMQ operations.

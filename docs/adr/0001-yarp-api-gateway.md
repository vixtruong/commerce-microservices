# ADR 0001: YARP is the REST edge

Status: Accepted

Public clients use REST through a thin YARP reverse proxy. The Gateway owns authentication, routing, rate limiting, CORS, correlation, health-aware load balancing, and request logging. It references no service contracts or layers and performs no business orchestration. This replaces the former GraphQL/gRPC aggregation model and lets services remain independently deployable.

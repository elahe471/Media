# Media as a Service

A standalone media service for an e-commerce system, designed to separate media traffic from the main application traffic.

The service is responsible for validating, storing, and delivering media files independently from business services such as Catalog.

## Architecture

```text
Client
  |
  v
Media.Api
  |
  +----> MinIO Object Storage
  |
  +----> RabbitMQ
             |
             v
        Catalog Service
```

The Media Service acts as the **publisher** and the Catalog Service acts as the **consumer**.

After a media file is uploaded successfully, the Media Service publishes a `MediaUploadedEvent`. The Catalog Service consumes this event and attaches the media reference to the related Catalog Item.

## Features

- ASP.NET Core Minimal API
- Media upload endpoint
- FluentValidation for uploaded files
- File extension validation
- MIME type validation
- File size validation
- Image width and height validation
- Image integrity validation with ImageSharp
- MinIO Object Storage
- Nginx reverse proxy and load balancing
- Public media URLs
- Private media access using temporary tokens
- Expiration and access limitation for private media
- MassTransit + RabbitMQ integration
- Structured logging with Serilog
- Elasticsearch + Kibana logging
- Error handling and compensating actions

## Object Storage

The project currently uses **MinIO** as the local Object Storage provider.

```text
Media.Api
   |
   v
Nginx :9000
   |
   v
MinIO Cluster
```

MinIO Console:

```text
http://localhost:9001
```

MinIO API:

```text
http://localhost:9000
```

## Public and Private Media

### Public Media

Public media can be accessed directly using a permanent URL.

```text
Client
  |
  v
Public Media URL
  |
  v
MinIO
```

This approach is suitable for product images and other public assets.

### Private Media

Private media is accessed through the Media API using a temporary token.

```text
Client
  |
  v
Media Token
  |
  v
Media.Api
  |
  v
MinIO
```

Private media tokens can support:

- Expiration
- Access limits
- Access counters

## Media Validation

Uploaded files are validated before being stored.

Current validations include:

- File existence
- Allowed extensions
- Allowed content types
- Maximum file size
- File name length
- Valid image content
- Minimum and maximum dimensions
- Maximum pixel count

## Event-Driven Integration

After a successful upload:

```text
Media.Api
   |
   v
MediaUploadedEvent
   |
   v
RabbitMQ
   |
   v
Catalog Consumer
```

The Catalog Service then updates the Product-to-Media relationship.

## Logging

The service uses:

```text
ILogger
   |
   v
Serilog
   |
   +----> Console
   |
   +----> Elasticsearch
               |
               v
             Kibana
```

Elasticsearch is treated as an observability dependency, so the Media API can continue working even if centralized logging is unavailable.

## Technology Stack

- .NET
- ASP.NET Core Minimal APIs
- FluentValidation
- ImageSharp
- MinIO
- Nginx
- Docker Compose
- MassTransit
- RabbitMQ
- Entity Framework Core
- Serilog
- Elasticsearch
- Kibana

## Roadmap

- Azure Blob Storage provider
- `IObjectStorage` abstraction
- Redis for temporary media tokens
- Transactional Outbox
- Image resizing and thumbnails
- CDN integration
- OpenTelemetry distributed tracing

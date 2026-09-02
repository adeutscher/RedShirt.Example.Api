# Upload feature — design decisions

This document records settled design choices for the upload template in
`RedShirt.Example.Api.Upload.*` and related infrastructure.

## Presigned download URLs instead of API streaming

Large files should not be streamed back through the API on every download. The
GET `/uploads/{id}/download-link` endpoint returns a time-limited URL that
allows the client (or a background worker) to fetch bytes directly from blob
storage.

Presigned URLs are a common capability among object stores:

| Provider | Notes |
|----------|-------|
| AWS S3 | `GetPreSignedUrl` (used by `S3FileStorageService`) |
| Azure Blob Storage | Shared access signatures (SAS) |
| Google Cloud Storage | Signed URLs |
| MinIO / LocalStack | S3-compatible presigned URLs |

Locally, the Compose stack uses LocalStack (ministack) with path-style S3
access. Workers and tests should download via the presigned URL returned by
the API rather than adding a dedicated “download bytes” API route.

## Workers and direct blob storage access

Some background processes intentionally bypass the API for bulk data movement
to keep upload-related endpoints short-lived and to follow least privilege.

In this template:

1. **Validator worker** — polls the API for `NotValidated` uploads, downloads
   via presigned URL, validates content, and POSTs to
   `/uploads/{id}/verdicts`. It does not need bucket credentials.
2. **Mover worker** — polls for `Verified` uploads, copies the object from
   the unverified bucket to the verified bucket **using S3 APIs directly**
   (see `upload-move-worker.py`), then POSTs to `/uploads/{id}/move-reports`.
   This worker assumes knowledge of the backing storage layout (bucket names
   and object keys from event details).
3. **Rejected cleanup worker** — polls for `Rejected` uploads and DELETEs via
   the API, which removes the blob and records an `UploadDeleted` event.

In a production deployment, mover duties could be combined with validation in
one worker service, or split across services with queue messages carrying
upload ids and storage coordinates. The mock Python scripts in
`test/local/upload-workers/` stand in for those workers during local testing.

The Keycloak **`upload-validator`** realm role is scoped to the worker-facing
API surface only:

- GET `/uploads/{id}`
- POST `/uploads/{id}/verdicts`
- POST `/uploads/{id}/move-reports`

Poll/search, download-link, delete, and end-user upload POST require broader
permissions (`upload:read`, `upload:write`). Local mock scripts default to an
admin token via `API_JWT_TOKEN`; use the validator token when exercising
least-privilege paths.

## Event sourcing and aggregate projection

Upload metadata uses a lightweight event-sourcing style:

- **`UploadEvent`** — append-only JSON events (`UploadCreated`,
  `UploadCompleted`, `UploadValidated`, …).
- **`UploadAggregate`** — cached projection updated on each append using a
  Marten-style `Apply` pattern in `UploadAggregate`.

Streams are short for this example; the aggregate table mirrors the pattern
planned for Accounts ([#42](https://github.com/adeutscher/RedShirt.Example.Api/issues/42))
while keeping rehydration cheap for `/uploads/{id}/details`.

## Platform-agnostic boundaries

| Concern | Abstraction |
|---------|-------------|
| Blob I/O | `IFileStorageService` (`Common.FileStorage`) |
| S3 implementation | `S3FileStorageService` (`Common.Aws.S3FileStorage`) |
| Lifecycle signals | `IUploadEventBroadcaster` (stub; intended for EventBridge / Event Grid / queues) |
| Validation / move / delete processing | External workers (JobWorker template), not this API |

The API streams uploads into storage as early as possible, records events, and
broadcasts state changes. It does **not** implement queue consumers — that
belongs in the JobWorker template.

## Configuration

| Setting | Environment variable |
|---------|---------------------|
| Max request body size (nullable) | `UPLOADS__MAX_UPLOAD_SIZE_BYTES` |
| Unverified bucket | `UPLOADS__BUCKET_UNVERIFIED_ITEMS` |
| Verified bucket | `UPLOADS__BUCKET_VERIFIED_ITEMS` |
| Presigned URL lifetime (minutes) | `UPLOADS__PRESIGNED_URL_LIFETIME_MINUTES` |

## Validation rule (example)

A valid document is a **plaintext file containing the word `potato`**
(case-insensitive). The API does not enforce this on upload; the validator
worker applies the rule and submits a verdict.

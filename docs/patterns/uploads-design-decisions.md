# Upload Feature Description

This document describes the standards and decisions made around the upload feature in `RedShirt.Example.Api.Upload.*` and related infrastructure.

The short version is:

* Uploads are streamed directly into file storage.
* Uploads are verified before they come to their final resting place.
* Uploads assume that the file storage back-end can provide a presigned download URL for downloads.
* Uploads use event sourcing to track events.

For information on testing uploads, refer to the TOMATO folder.

# Decisions

## Streaming

In order to avoid overloading the API, the upload system streams information directly into file storage. 

## Verification

As described above, uploads are streamed directly into storage without immediate verification. Instead, that duty is delegated to downstream workers. The intent is that items should be verified before any further execution is performed on them.

In an applied version of this template, it is expected that the back-end worker be notified of upload events through an implementation of the `IUploadEventBroadcaster` interface (defined in the `RedShirt.Example.Upload.Core` project). The baseline version of this template performs these duties using mock scripts. This decision was made for the following reasons:

* The general template does not want to commit to a specific messaging technology.
* Handling of queued messages is more in the domain of the [RedShirt.Example.JobWorker Template](https://github.com/adeutscher/RedShirt.Example.JobWorker).

For information on testing uploads, refer to the TOMATO folder.

## Presigned Download URLs

Large files should not be streamed back through the API on every download. Instead, the
GET `/uploads/{id}/download-link` endpoint returns a time-limited URL that
allows the API client to fetch bytes directly from blob storage.

Presigned URLs are a common capability among object stores:

| Provider | Notes |
|----------|-------|
| AWS S3 | `GetPreSignedUrl` (used by `S3FileStorageService`) |
| Azure Blob Storage | Shared access signatures (SAS) |
| Google Cloud Storage | Signed URLs |
| MinIO | S3-compatible presigned URLs |

Download links include a `Content-Disposition` header suggesting the original
`fileName` supplied at upload time.

The Compose stack for local testing uses MiniStack with path-style S3
access. Workers and tests should download via the presigned URL returned by
the API rather than adding a dedicated “download bytes” API route.

## Workers

### Direct Blob Storage Access

In a perfect world, the API would completely mask the infrastructure decision of which blob storage technology was in use.
However, some background processes intentionally bypass the API in order keep upload-related endpoint requests short-lived.

In this template:

* The mock mover worker processes a `Verified` upload by id, copies the object from the unverified bucket to the verified bucket **using S3 APIs directly** (see `upload-move-worker.py`), then POSTs to `/uploads/{id}/move-reports`. This worker assumes knowledge of the backing storage layout (bucket names are assumed to be known and object keys are retrieved from the upload's details as provided by the API).

### Shared Responsibilities

In this template:

* The `upload-validator` user has access to perform duties related to validating uploads withing the API.
* The various background worker duties are separated into individual mock worker scripts. In a production deployment, these duties could be combined into one worker service, or split across services with queue messages carrying upload ids and storage coordinates. The mock Python scripts in `test/local/scripts/upload/` stand in for those workers during local testing.

```bash
export API_JWT_TOKEN="$(./test/local/get-bearer-token.py)"
python3 test/local/scripts/upload/upload-file.py path/to/document.txt
```

Use `list-upload-jobs.py` to list in-flight uploads (`Uploading`, `NotValidated`, `Verified`):

```bash
python3 test/local/scripts/upload/list-upload-jobs.py
```

Run a worker against a specific upload id from that list:

```bash
python3 test/local/scripts/upload/upload-validate-worker.py <upload-id>
python3 test/local/scripts/upload/upload-move-worker.py <upload-id>
python3 test/local/scripts/upload/upload-cleanup-rejected-files-worker.py <upload-id>
```

The Keycloak **`upload-validator`** realm role is scoped to worker-facing write endpoints:

- POST `/uploads/{id}/verdicts`
- POST `/uploads/{id}/move-reports`

Poll/search, download-link, delete, and end-user upload POST require broader
permissions (`upload:read`, `upload:write`) and owner scoping (see below).
Local mock scripts default to an admin token via `API_JWT_TOKEN`; use the
validator token when exercising least-privilege worker paths.

## Upload owner scoping

Mirroring order customer scope:

- **Search** — non-admin callers only see uploads where `UploadedByUserId`
  matches their JWT `sub`.
- **GET / DELETE / details** — uploader or admin only.
- **Download link** — state-dependent:
  - Non-`Stored`: admin or `upload:validator` (validators fetch content for
    validation; uploaders cannot download until stored).
  - `Stored`: uploader or admin.

## Event sourcing and aggregate projection

Upload metadata uses a lightweight event-sourcing style:

- **`UploadEvent`** — append-only JSON events (`Created`, `Completed`, `Validated`, …).
  `EventType` is stored as an integer; API consumers see string enum names when exposed.
- **`UploadAggregate`** — cached projection updated on each append using a
  Marten-style `Apply` pattern in `UploadAggregate`. `State` is stored as an integer;
  API responses serialize `UploadState` as strings (for example `"NotValidated"`).
- **`UploadDetailsModel`** — flattened response with nullable fields per event
  type (not raw event JSON).

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

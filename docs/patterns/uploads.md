# Upload Feature Description

This document describes the standards and decisions made around the upload feature in `RedShirt.Example.Api.Upload.*`
and related infrastructure.

The short version is:

* Uploads are streamed directly into file storage.
* Uploads are verified before they come to their final resting place.
* Uploads assume that the file storage back-end can provide a presigned download URL for downloads.
* Uploads use event sourcing to track events.

For information on testing uploads, see the [test/local README](../../test/local/README.md#uploads).

# Configuration

Configuration environment variables for the upload system.

| Setting                          | Environment variable                      |
|----------------------------------|-------------------------------------------|
| Max request body size (nullable) | `UPLOADS__MAX_UPLOAD_SIZE_BYTES`          |
| Unverified bucket                | `UPLOADS__BUCKET_UNVERIFIED_ITEMS`        |
| Verified bucket                  | `UPLOADS__BUCKET_VERIFIED_ITEMS`          |
| Presigned URL lifetime (minutes) | `UPLOADS__PRESIGNED_URL_LIFETIME_MINUTES` |

# Decisions

## Internal Endpoints

The current implementation of the upload system exposes several endpoints and parameters intended for internal use.
Though access to these items is restricted, exposing the existence of these endpoints at all to end users is unnecessary
at best and a liability at worst.

A future branch is planned in which the following shall be implemented:

* Mark certain internal endpoints and parameters as internal.
* Maintain separate OpenAPI documents that exclude internal endpoints and parameters.
* Consider separate handling in which trying to access internal endpoints despite not being documented returns an HTTP
  404 rather than an HTTP 403.
    * Security by obscurity is no good, but an extra layer of obscurity over existing security couldn't hurt.

## Streaming

In order to avoid overloading the API, the upload system streams information directly into file storage.

## Verification

As described above, uploads are streamed directly into storage without immediate verification. Instead, that duty is
delegated to downstream workers. The intent is that items should be verified before any further execution is performed
on them.

In an applied version of this template, it is expected that the back-end worker be notified of upload events through an
implementation of the `IUploadEventBroadcaster` interface (defined in the `RedShirt.Example.Upload.Core` project). The
baseline version of this template performs these duties using mock scripts. This decision was made for the following
reasons:

* The general template does not want to commit to a specific messaging technology.
* Handling of queued messages is more in the domain of
  the [RedShirt.Example.JobWorker Template](https://github.com/adeutscher/RedShirt.Example.JobWorker).

For information on testing uploads, see the [test/local README](../../test/local/README.md#uploads).

## Presigned Download URLs

Large files should not be streamed back through the API on every download. Instead, the GET
`/uploads/{id}/download-link` endpoint returns a time-limited URL that allows the API client to fetch bytes directly
from blob storage.

Presigned URLs are a common capability among object stores:

| Provider             | Notes                                              |
|----------------------|----------------------------------------------------|
| AWS S3               | `GetPreSignedUrl` (used by `S3FileStorageService`) |
| Azure Blob Storage   | Shared access signatures (SAS)                     |
| Google Cloud Storage | Signed URLs                                        |
| MinIO                | S3-compatible presigned URLs                       |

Download links include a `Content-Disposition` header suggesting the original
`fileName` supplied at upload time.

The Compose stack for local testing uses MiniStack with path-style S3 access. Workers and tests should download via the
presigned URL returned by the API rather than adding a dedicated “download bytes” API route.

## Workers

### Direct Blob Storage Access

In a perfect world, the API would completely mask the infrastructure decision of which blob storage technology was in
use. However, some background processes intentionally bypass the API in order keep upload-related endpoint requests
short-lived.

In this template:

* The mock mover worker processes a `Verified` upload by id, copies the object from the unverified bucket to the
  verified bucket **using S3 APIs directly** (see `upload-move-worker.py`), then POSTs to `/uploads/{id}/move-reports`.
  This worker assumes knowledge of the backing storage layout (bucket names are assumed to be known and object keys are
  retrieved from the upload's details as provided by the API).

### Shared Responsibilities

In this template:

* The `upload-validator` user has access to perform duties related to validating uploads within the API.
* The various background worker duties are separated into individual mock worker scripts. In a production deployment,
  these duties could be combined into one worker service, or split across services with queue messages carrying upload
  ids and storage coordinates. The mock Python scripts in `test/local/scripts/upload/` stand in for those workers during
  local testing (see the [test/local README](../../test/local/README.md#uploads) for usage).

The Keycloak **`upload-validator`** realm role is scoped to worker-facing write endpoints:

* POST `/uploads/{id}/verdicts`
* POST `/uploads/{id}/move-reports`

## Upload Owner Scoping

The upload feature uses scoped permissions to ensure that users cannot meddle in the permissions :

* **Search**: non-admin callers only see uploads where `UploadedByUserId`
  matches their JWT `sub`.
* **GET / DELETE / details**: uploader or admin only.
* **Download link** (state-dependent):
    * Non-`Stored`: admin or `upload:validator` (validators fetch content for validation; uploaders cannot download
      until stored).
    * `Stored`: uploader or admin.

Expanding on these permissions such as granting download access to another internal process is an exercise for the
reader.

## Database Tables and Event Sourcing

The upload system uses an event-sourcing style, in part just for an opportunity to practice with the design pattern.

Database Table Details:

* **`UploadEvent`**: append-only JSON events (`Created`, `Completed`, `Validated`, etc).
* **`UploadAggregate`**: this cached projection is updated on each append using an
  `Apply` pattern in the style of the Marten Event Store.

## Platform-agnostic boundaries

| Concern                               | Abstraction                                                                      |
|---------------------------------------|----------------------------------------------------------------------------------|
| Blob I/O                              | `IFileStorageService` (`Common.FileStorage`)                                     |
| S3 implementation                     | `S3FileStorageService` (`Common.Aws.S3FileStorage`)                              |
| Lifecycle signals                     | `IUploadEventBroadcaster` (stub; intended for EventBridge / Event Grid / queues) |
| Validation / move / delete processing | External workers (JobWorker template), not this API                              |

The API streams uploads into storage as early as possible, records events, and broadcasts state changes. It does **not**
implement queue consumers: that belongs in
the [JobWorker template](https://github.com/adeutscher/RedShirt.Example.JobWorker).

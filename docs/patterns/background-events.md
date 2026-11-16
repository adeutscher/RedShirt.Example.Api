
# Background Tasks

.NET applications can be set up with background threads using the phrasing of 'Hosted Services'.

This page describes:

* [How that could be useful](#use-cases)
* [How to set up background threads in general](#setup-general)
* [This template's BackgroundJob implementation](#)

## Use Cases

Doing this could be useful for:

* Periodic internal maintenance within the scope of the API instance.
* When there is a single instance of the API, it could be used for broader app maintenance such as periodically invoking
  CQRS handlers.
    * This case is an infrastructure shortcut to avoid the overhead of creating/maintaining a separate worker
      application for simple tasks. This option should not be explored or extracted out into a separate worker
      application the moment that the environment plan involves multiple instances of the API running in an environment.

This a standard feature of a hosted .NET application and not a special feature of this template, but I figured that it
was worth documenting here.

### Scope Limits

The background services in this template are made to do quick and/or periodic tasks on a single API instance.

This line of thinking starts to fall apart if you want to scale up this template, such as if you need:

* Multiple API instances
* More complex job handling

If you need to scale up, I would strongly encourage considering migrating background services to another process. If you specifically need to migrate the background job handling featured in this API template, the logic was structured to make for a (relatively) simple migration an application of the [JobWorker template](https://github.com/adeutscher/RedShirt.Example.JobWorker).

## Setup (General)

To set up a general background task, you can do the following:

1. Declare class that inherits from `BackgroundService`

    ```csharp
    public sealed class ExampleHostedService(IStuffDoerService stuffDoerService) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Perform action. It's assumed that the implementation runs continually.
                await stuffDoerService.DoStuffAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    ```

2. Add a hosted service when setting up your service collection:

    ```csharp
    services.AddHostedService<ExampleHostedService>()
    ```

## Background Jobs

Based on another project, this template features a background job system for handling deferred tasks. This template uses it for upload validation and placement.

### Cleanup

An API can be configured to clean up old completed tasks:

* `BACKGROUND_JOBS__CLEANUP_ENABLED`: When set to `true`, enables cleanup.
* `BACKGROUND_JOBS__CLEANUP_RETENTION_DAYS`: Must have a value if cleanup is enabled, dictates how long a completed background job is kept before it is cleaned.

Cleanup runs once every hardcoded hour.

### Implementation Limitations and Migration

The implementation of background jobs included in this general template is meant for low-impact minor jobs. It was implemented in this general template to be a light version of the short-polling behaviour found in the [JobWorker template](https://github.com/adeutscher/RedShirt.Example.JobWorker). Being the author of both, I would strongly advise pivoting to use an applied version of the JobWorker template.

#### Reasons to Migrate

You may wish to switch to an applied version of the JobWorker template if:

* You are running into problems with multiple instances of the API handling the background job table polling for tasks.
* Job lengths and/or workload sizes are beyond what the API can offer in a reasonable timeframe.

#### Migration Actions

If you wish to migrate, then I would advise the following steps:

1. Select a queue technology that the JobWorker template supports.
    * As a deliberate choice, this API template does not support polling/claiming jobs from outside sources to avoid making it a message broker in its own right. If you feel that this is something that you would want to do, then the only missing step should be the API endpoints pointing at the established CQRS handler. That said, the CQRS handler was not made with remote access in mind and should be thoroughly tested.
2. Edit the `BackgroundJobSubmitter` class in the API to submit to the queue after writing record to the chosen messaging service with the existing `backgroundJobService.CreateAsync` call.
    * I would strongly advise doing the submitting through a simplified abstraction to maintain a good structure.
3. The job handlers in this API template access the background jobs datastore using CQRS handlers. You will need to add endpoints that access handlers for marking a started job, a failed job, and a finished job. The CQRS handlers already exist, they just need the last-mile connection.
    * Remember to add appropriate required permissions for a worker.
4. Publish the updated Interop project as a NuGet or otherwise export the Swagger/OpenAPI document for use by the JobWorker.
5. Create an applied version of the JobWorker template according to its documentation.
6. In your applied JobWorker project:
    * Copy the content of the API's `BackgroundJobLogicRunner` projects into `Core.Logic` projects in the applied JobWorker.
    * Copy handlers.
    * Update dependency injection.
    * Replace the CQRS calls from the API version of job handler with calls to an API connector. I strongly encourage using the pattern set out by the API template's BarConnector.
7. Clean up copied content from the API.

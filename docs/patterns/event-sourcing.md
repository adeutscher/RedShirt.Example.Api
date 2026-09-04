# Event Sourcing

My notes on the Event Sourcing pattern.

## Overview

Event sourcing is based on the principle of domain objects being derived from a series of events that update the state
of the domain object.

## Key Terms

Some key terms:

* **Aggregate**/ **Projection**: User-facing domain object that expresses an accumulation of events.
* **Rehydration**/ **Rebuilding**: The practice of rebuilding an aggregate from the events on a record. Also known as
  "Replay".
* **Eventual Consistency**: Accepting that some database operations may take a moment to be reflected in a GET
  operation.
* **Subscription**: Per Google, "a listener tool that reacts instantly when a new event is saved to update read models".
  My read: able to be deferred through a job worker?
* **Snapshot**: Saved copy of a record up to a certain point in time.
* **Event Stream**: Connected sequence of events for a particular record.

## On Stream Length And The Importance of Short Event Streams

To repeat the definition, event sourcing is based on summarizing a series of events to reach a domain object's current
state. While building a system that uses this pattern, having event streams be as short as possible is advantageous for
performance. Fundamentally, fewer events to compile means less I/O on the application.

It is important to balance the specific needs of the application versus performance.

### Summaries, Truncation, Data Lifecycle

Developers should strongly consider their long-term requirements when planning their stream lifecycle and storage
logistics.

For example: In Canadian financial law, financial records are only required to be kept for 6 years (with a 7-year
retention being a common rule of thumb to be on the safe side). Depending on the business case, data kept past this
point could just be extra storage that needs to be paid for.

[Oskar Dudycz](https://github.com/oskardudycz), a vocal expert on event sourcing, brings up the possibility of using
summaries that allow for streams to be truncated. The information recorded in the summary could, on a case-by-case
basis, be deleted entirely or copied to a separate data store. Injecting a summary at a point would technically violate
the tenent of an event sourcing implementation being an append-only store of events. Therefore, it should be done
carefully and keep data consistency as a top priority in the face of practical realities such as I/O performance and
storage costs.

### Closing The Books Pattern

"Closing The Books" is a sub-pattern within event sourcing that draws from the world of accounting.

Consider the example of a cash register in a grocery store:

* On a day-to-day basis, the cashier at the start of their shift is only concerned with the amount of money in the till.
* On a larger scale, if we consider the entire business to be an event stream then it isn't essential to the business to
  immediately know about the details of a specific transaction. The big-picture stream doesn't want to count every
  receit in order to know its state.

An implementation of the "Closing the Books" pattern could see a particular shift at a register as one event stream.
When the shift ends, part of the end-of-shift handling could be to send a summary event to a parent event stream. This
would prevent having to load every individual transaction in order to rebuild the state of the parent object. The event
stream for a particular shift would still be retained and referenced as needed, it's just that the number of events in
an individual stream are constrainted.

## Idempotency Keys

As with other types of message handling, event-driving messages should be idempotent. Resubmitting the same event (such
as if a human accidentally hit a submit button twice) should not an action a second time again.

It is strongly encouraged that an event sourcing implementation use some sort of idempotency support in order to avoid
repeats, especially where human input could asynchronously stack repeat requests.

## Potential Pitfalls

Some general pitfalls that I became aware during my research:

* Be wary of potential disconnects between the submission of an event and the updating of the aggregate state that could
  affect separate requests.
    * An example of this would be a hypothetical bank account that accepts withdrawal attempts but does not immediately
      update the aggregate, which could allow for multiple withdrawals without sufficient funds.
* Long event streams could cause problems.
* Increased risk of events being played/handled out of order.
    * The partitioning of a Kafka topic was highlighted as an example of this.
* Separation of business-case expertise and developer expertise during development. A good implementation of event
  sourcing should be done with a good understanding of the use case.

### REST Naming Conflict

This template tries to be RESTful, but event sourcing phrasings and the CQRS layer in beteen doesn't always agree with
RESTful conventions.

The different goals:

* A purely RESTful API refers to the use of nouns in HTTP paths. The only verbs in the picture should be HTTP verbs in
  the operation type (e.g. `GET`, `POST`, `DELETE`, etc).
* CQRS is intent-oriented with its Commands and Queries (e.g. `DoThingCommands` or `GetRecordQuery`).
* Event Sourcing is historical, and favours past-tense verbs (e.g. `RecordCreated`, `RecordValidated`)

So far, my solution to this has been to rephrase operations as nouns, even if it's sometimes a bit strange. For example,
the upload system uses the `POST` endpoint at `/uploads/{id}/verdicts` to accept verdicts on the validity of an upload.

## Event Sourcing In This Template

When adding example content to this template, I strongly considered adding a new data type such as `Account`
specifically to showcase event sourcing. However, I decided against it and instead settled on writing this document. The
key reasons for this is that a good event sourcing system encourages strong integration with business knowledge.
Creating a dedicated system solely to demonstrate event sourcing suggested a series of case-specific decisions.

All that being said, this template does make use of event sourcing in its upload system. This doesn't contradict my
previous decision because the actual upload operation had a functional use that was reusable enough to justify the
effort.

### Upload System

The upload system of this API template utilizes the event sourcing pattern:

* Events are stored in the `UploadEvent` table.
    * I considered storing details unique to a particular event in separate tables for searchability. However, I decided
      that this was unnecessary for this case. The information is instead stored in a JSON document with the
      understanding that the event table will never need to be filtered by its content.
        * Off-topic, but I **_strongly_** discourage the idea of querying a JSON column unless it's to prove a point
          about why the storage schema needs to be changed so that one no longer has to consider querying the JSON
          column. Please don't do it.
* Uploads have the following states: `Uploading`, `NotVerified`, `Verified`, `Rejected`, `Deleted`, and `Stored`.
* The aggregate state of uploads is stored in the `UploadAggregate` table to allow for searching and rapid retrieval.
* A more detailed description of an upload can be requested, and is based on the rehydration of events in the stream.
* Events are submitted to the stream to drive state changes.
* For this particular case, the aggregate record is written in the same database transaction that inserts to the event
  stream. This prevents drift between event and state for this case.
* The initiating POST request to submit an upload accepts an idempotency key header (`Idempotency-Key`). If an aggregate
  exists under this idempotency key, then the follow-up attempt will be rejected with an HTTP 409 (Conflict) status
  code.

For more information on the upload system, refer to [`uploads.md`](uploads.md).

## Other Resources

This document is only my compiled notes on event sourcing. The following resources may also be useful:

* [Oskar Dudycz](https://github.com/oskardudycz) is a vocal proponent of Event Sourcing. He has compiled code examples
  links to his various videos and blog posts on Event Sourcing in
  his [EventSourcing.NetCore](https://github.com/oskardudycz/EventSourcing.NetCore) repository. It should absolutely be
  required reading for learning about event sourcing, it's an absolute gold mine.
* [Microsoft Documentation on Event Sourcing](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
* This document was created in response to [this](https://github.com/adeutscher/RedShirt.Example.Api/issues/42) GitHub
  issue. I have distilled my comments from that issue into this document, but it may still be useful in some way.

### Popular Event Store Projects

The following are some potential general event store solutions:

* [KurrentDB](https://www.kurrent.io/) (formerly known as EventStoreDB): Comes recommended, but has a fee for commercial
  use
* [Marten](https://martendb.io/events/): Builds on a Postgres back-end
# Client Events / Server-Sent Events

This document talks a bit about this template's usage of Server-Sent Events to send live updates to connected clients.

# Overview

Server-Sent Events (SSE) send a stream of event messages to a client as opposed to having them poll for new information.

This template uses the MQTT to coordinate messages between clients. Using an external service is necessary for a couple
of reasons:

* There could be more than one instance of an API running in an environment. An in-process coordination system would not
  have awareness of other instances.
* There could be other sources writing events directly to the external service (though this is not recommended for
  single-responsibility reasons).

## Template Example

This template has a simple messaging example:

* The POST endpoint at `/messages` can be used by a client to send events that will be read by any instance of the
  client's user connected to the event-stream.
* The GET endpoint at `/messages/event-stream` can be used by a client to receive events related to their user. Users
  will not receive messages intended for other users.

### Remove the Send Endpoint Unless You Need It

Unless you are building some kind of messaging application, remove `POST /messages` (the `Post` action on
`MessageController`, with its `SendExampleMessageCommand` and handler). It was added mostly as a convenient way to
trigger events for an SSE demonstration, not as a pattern to copy.

In a typical API, events come from the server's own work: an upload finished validating, an order changed state, a
background job completed. The code doing that work publishes the event with `IApiClientEventSender<T>` (or a domain
service like `IExampleMessageSendService`). Letting clients publish arbitrary text to other connections opens a channel
you then have to police for size, rate, and content, and most applications have no use for it.

Keep the event stream endpoint and the receive side. Those are the parts most applications want.

# Configuration Options

The MQTT client settings are listed in the [test/local README](../../test/local/README.md#messages-server-sent-events).

## Payload Size From the Broker

`ClientEvents:Mqtt:MaxPayloadBytes` (environment variable `CLIENT_EVENTS__MQTT__MAX_PAYLOAD_BYTES`) is the largest MQTT
payload, in bytes, that the API passes on to event streams.

* It is checked before the payload is turned into a string or deserialized. A larger payload is dropped with a
  warning that names the topic and the size. Later messages on the same stream still arrive.
* When unset, the limit is 131072 bytes (128 KiB). That matches the largest message AWS IoT Core accepts, so anything
  such a broker delivers still fits, while one oversized message cannot take much memory on each open stream.
* Zero or negative turns the check off, which is logged as a warning the first time a stream opens.

The check is on the receive side because the API is not the only thing that can publish to the broker. Other services,
or anything that has broker credentials, can publish to the same topics, so what the send endpoint accepts says nothing
about what arrives. If your broker has its own limit (for example Mosquitto's `message_size_limit`), keep this setting
at or below it.

## Slow Clients

Each open stream has a queue between the MQTT client, which pushes events as the broker delivers them, and the HTTP
response, which can only send as fast as the client reads. A client on a poor connection, or a browser tab that has
stalled, stops reading. Once the response buffer fills, the queue is all that is left to absorb new events.

`ClientEvents:Mqtt:MaxQueuedEventsPerStream` (environment variable `CLIENT_EVENTS__MQTT__MAX_QUEUED_EVENTS_PER_STREAM`)
bounds that queue.

* When unset, a stream can fall 256 events behind.
* When a stream falls further behind, the API closes it. The events already queued are still sent, then the stream
  ends normally and a warning names the topic. The client reconnects.
* Zero or negative removes the bound, which is logged as a warning the first time a stream opens.

Closing the stream is deliberate. The alternatives are worse:

* **Waiting for the client** would hold up the MQTT client's message handling, which delays QoS 1 acknowledgements and
  can lead the broker to disconnect it.
* **Dropping events** would keep the stream open with gaps the client cannot see, because this stream has no event IDs
  or replay.

A closed stream tells the client something may have been missed. Clients should reconnect with a delay (see
[Backing Off With Jitter](#backing-off-with-jitter)) and reload current state after reconnecting.

The queue bound and the payload limit together set the worst-case memory for one stream: roughly
`MaxQueuedEventsPerStream` times `MaxPayloadBytes`, or about 32 MiB at the defaults. Typical events are a few hundred
bytes, so a real queue is far smaller. If you raise the payload limit, consider lowering the queue bound to match.

## Connections Per User

`ServerSentEvents:MaxConnectionsPerUser` (environment variable `SERVER_SENT_EVENTS__MAX_CONNECTIONS_PER_USER`) caps how
many event streams one user can hold open at the same time.

* Users are identified by the JWT subject, the same value that picks their MQTT topic.
* A request over the cap gets `429 Too Many Requests` with a ProblemDetails body, before any MQTT connection is made.
* A slot is freed when the request ends, whether the stream finished, the client disconnected, or the request failed.
* Zero, a negative value, or leaving it unset means unlimited. The API logs a warning at startup in that case, in every
  environment.
* The local Compose stack sets it to `3`.

The count is kept in memory on each API instance. It is not shared between instances. If a load balancer spreads one
user's streams across several instances, that user can hold up to the cap times the number of instances. Sticky
sessions bring this closer to the configured value. A cross-instance count (for example in Redis) would be exact, but it
adds a dependency to every stream open and needs cleanup for instances that die while holding slots. That trade was not
worth it for this template.

### Choosing a Limit

This section is opinion, not a rule. The right number depends on the application: who the clients are, how many devices
and tabs they use, how the clients are written, and what the broker and hosting plan allow. Treat the numbers below as
a place to start and adjust them from what you see in production.

First, what one stream costs in this template:

* An HTTP connection that Kestrel holds open for as long as the client listens.
* Its own MQTT client connection to the broker. Streams do not share a broker connection, so the broker sees one
  connection per open stream. Managed brokers usually have connection quotas, and some (AWS IoT Core among them) bill
  by connection time.
* An in-process channel that buffers messages until the client reads them.

So the cap is less about one user hogging the API and more about one buggy or hostile client turning into hundreds of
broker connections. A client that reconnects in a tight loop without closing its old streams is the common accident.

| Client type                         | Suggested starting point | Why                                                 |
|-------------------------------------|--------------------------|-----------------------------------------------------|
| Interactive users (browser, mobile) | 5                        | A phone, a laptop, and a few tabs                   |
| Internal staff tools and dashboards | 10                       | Power users keep many tabs open                     |
| Service accounts and integrations   | 1 or 2                   | One consumer stream, plus one for a restart overlap |

Some reasoning behind those numbers:

* **Browsers already limit themselves over HTTP/1.1.** A browser allows about 6 connections per origin, and every
  open stream uses one of them, so a user with many tabs breaks their own page loads long before reaching a high cap.
  HTTP/2 removes that limit, so the server-side cap matters more when the API is served over HTTP/2.
* **A cap far below real use only produces support tickets.** If legitimate users hit `429`, raise the cap or change the
  client, rather than leaving it.
* **A cap far above real use protects nothing.** 50 streams per user is close to unlimited for a single bad client.
* **Service accounts should not need many.** A backend consumer should hold one stream and fan messages out
  internally. Allow a second slot so a restarting consumer can connect before the old connection times out.
* **One cap covers every client type.** This template has a single setting. If interactive users and service accounts
  need very different caps, a per-role cap (read from the permission claims) is the next step.

Things to do alongside the cap:

* Have clients treat `429` as "back off and retry later with jitter", not "retry now". See
  [Backing Off With Jitter](#backing-off-with-jitter).
* Share one stream between browser tabs where possible. See
  [Sharing One Stream Between Tabs](#sharing-one-stream-between-tabs).
* Size the broker for the worst case: users times cap times API instances, unless sticky sessions are in place.
* Keep an instance-wide backstop. Kestrel's `Limits.MaxConcurrentConnections` limits all connections on an instance, no
  matter which user opened them. The rate limiter limits how often streams are opened, not how many stay open.

# Special Cases

## Broker Transport Security

The broker URL's scheme decides whether the connection is encrypted:

| Scheme                         | Transport                    | Default port | Encrypted |
|--------------------------------|------------------------------|--------------|-----------|
| `mqtts://`                     | MQTT over TLS                | 8883         | Yes       |
| `wss://`                       | MQTT over WebSocket over TLS | 443          | Yes       |
| `mqtt://`, `tcp://`, bare host | MQTT over TCP                | 1883         | No        |
| `ws://`                        | MQTT over WebSocket          | 80           | No        |

`mqtts://` validates the broker certificate against the system trust store, including the host name. There is no
setting to skip validation.

Outside the `Development` environment, the API logs a warning (once per broker URL) when it connects over a plaintext
scheme. The warning says whether broker credentials are being sent in the clear.

### Known Gap: AWS IoT Resolution Builds `ws://`

When `ResolveBrokerAddressExternally` is on, `AwsIotMqttBrokerUrlResolver` always builds a `ws://` URL. That is what
the local MiniStack stack needs, but it is not usable against real AWS IoT Core. IoT Core accepts MQTT over WebSocket
only as `wss://` with a SigV4 signed upgrade, and MQTT over TLS only with an X.509 client certificate (or a custom
authorizer). Neither is implemented, so connecting to AWS IoT Core is not supported yet. Other brokers work through a
directly configured `BrokerUrl`. Outside Development, the plaintext warning flags the resolver's `ws://` URL.

## AWS IoT Endpoint Resolution

During local testing, it was discovered that pointing the Broker URL didn't cut it for connecting to MiniStack in the
local Docker Compose stack.

This issue is in part a product of my choice to use MiniStack's IoT emulator for local testing. However, I am choosing
to keep this feature in the template because it's possible (though not required) to resolve a broker URL using the
AWSSDK.IoT client if one is working in an AWS environment. It will return an IoT data-plane hostname that is stable for
a given account and region. That said, I'm also acknowledging that AWS deployments are not everyone's end goal. In the
same vein as the rest of this template, the AWSSDK.IoT operations have been quarantined in a separate
`RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws` project in order to limit the complexity of the removal.

If you want to remove the use of the `AWSSDK.IoT` altogether, then the following steps are advised:

1. Delete the `RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws` project. Off a cliff, it goes.
2. Remove the dependency injection setup issue that pops up in the root `RedShirt.Example.Api` project.
3. Do one of the following in the `RedShirt.Example.Api.ClientEvents.Library.Mqtt` project to address the need for an
   implementation of `IMqttBrokerUrlResolver` in `MqttClientFactory`:
    * Delete the `IMqttBrokerUrlResolver` altogether, and resolve any build errors that come up from it. I would
      recommend this path, as apparently there's no other system outside of AWS that uses a resolution endpoint (except
      perhaps if you insisted on also storing the broker URL in a secret manager?).
        * If you decide to take this path, then in `` you could probably just fold `ResolveBrokerTargetInnerAsync` into
          ResolveBrokerTargetAsync.
    * Add in a quick stub implementation of the `IMqttBrokerUrlResolver` interface.
        * The implementation can just return an object with a null URL.
        * Be sure to set configuration so that this path is never accessed.
4. The `AWSSDK.IoT` library was required to locally test MQTT using MiniStack, so you will need to adjust your local
   compose stack to use a different server. The following are some potential options (these haven't been explored in
   detail, MiniStack was deemed good enough for the time being):
    * Mosquitto: `eclipse-mosquitto` image
        * According to Google, this has the lowest resource footprint of the immediate options.
    * EMQX: `emqx/emqx` image
    * HiveMQ (Community Edition): `hivemq/hivemq-ce` image
    * RabbitMQ (not enabled by default, needs to be explicitly enabled)
5. Update configuration in local `test/local/docker-compose.yaml` file to point to the new server solution.
    * You may need to also set credentials in secret managers.

# Other Technologies

Server-Sent Events at the API level are a new feature in .NET 10. However, it was not the first iteration of the overall
pattern of streaming events.

## SignalR

SignalR is a historically-significant library for pushing event data to connected clients in real time.

I chose not to use SignalR in this template for the following reasons:

* SignalR supports bidirectional communication, which seemed like more complexity than I needed for my basic goal of
  reading events.
* SignalR apparently has more of a resource footprint.

With these reasons in mind, I would only pivot to SignalR if I had a specific case that required it.

# Client Examples

Reading from an event stream is a different challenge then a flat HTTP request, so here are several generated code
samples for interacting with them.

## C#

### Interop

A sample listener has been written for the `RedShirt.Example.Api.Interop` project, called `EventStreamListener`.

Example usage:

```csharp
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", accessToken);

var listener = new MessagesEventStreamListener("https://api.example.com", httpClient);

await listener.StreamEventsAsync(
    // Can use an Action<string> or Func<string, CancellationToken, Task> 
    message => Console.WriteLine($"[message] {message}"),
    cancellationToken);
```

### Standalone

Use this approach when you do not want a dependency on `RedShirt.Example.Api.Interop`. It uses `HttpClient` to open
`GET /messages/event-stream`, reads the response as a stream, and parses SSE blocks manually (same event name and plain
text `data` payload as the JavaScript example).

```csharp
using System.Net.Http.Headers;
using System.Text;

static async Task ListenToMessageStreamAsync(
    string apiBaseUrl,
    string accessToken,
    Action<string>? onEvent = null,
    CancellationToken cancellationToken = default)
{
    onEvent ??= message => Console.WriteLine($"[message] {message}");

    var baseUrl = apiBaseUrl.TrimEnd('/');
    using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/messages/event-stream");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

    using var httpClient = new HttpClient();
    using var response = await httpClient.SendAsync(
        request,
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"HTTP {(int)response.StatusCode}: {errorBody}",
            null,
            response.StatusCode);
    }

    if (response.Content == null)
    {
        return;
    }

    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    using var reader = new StreamReader(stream, Encoding.UTF8);

    string? eventName = null;
    var dataLines = new List<string>();

    while (!cancellationToken.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line == null)
        {
            break;
        }

        if (line.Length == 0)
        {
            if (dataLines.Count > 0)
            {
                var resolvedEventName = eventName ?? "message";
                if (string.Equals(resolvedEventName, "message", StringComparison.Ordinal))
                {
                    onEvent(string.Join('\n', dataLines));
                }
            }

            eventName = null;
            dataLines.Clear();
            continue;
        }

        if (line.StartsWith(':'))
        {
            continue;
        }

        if (line.StartsWith("event:", StringComparison.Ordinal))
        {
            eventName = line["event:".Length..].Trim();
            continue;
        }

        if (line.StartsWith("data:", StringComparison.Ordinal))
        {
            dataLines.Add(line["data:".Length..].TrimStart());
        }
    }

    cancellationToken.ThrowIfCancellationRequested();
}

// Example:
// await ListenToMessageStreamAsync("http://localhost:8080", accessToken);
```

## JavaScript

The example stream lives at `GET /messages/event-stream`. Each event uses the SSE event name `message`, and the
`data` field is the plain message text (not JSON). The endpoint requires a bearer token with the `api:read` scope.

Browsers cannot set custom headers on the built-in `EventSource` API, so the example below uses `fetch` and reads the
response body as a stream. This works in modern browsers and in Node.js 18+.

```javascript
async function listenToMessageStream(
  apiBaseUrl,
  accessToken,
  onEvent = (eventName, data) => {
    console.log(`[${eventName}] ${data}`);
  },
) {
  const url = `${apiBaseUrl.replace(/\/$/, "")}/messages/event-stream`;

  const response = await fetch(url, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      Accept: "text/event-stream",
    },
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${await response.text()}`);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  console.log(`Listening on ${url} (Ctrl+C to stop)...`);

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });

    let eventBoundary;
    while ((eventBoundary = buffer.indexOf("\n\n")) >= 0) {
      const block = buffer.slice(0, eventBoundary);
      buffer = buffer.slice(eventBoundary + 2);

      let eventName = "message";
      const dataLines = [];

      for (const line of block.split("\n")) {
        if (line.startsWith("event:")) {
          eventName = line.slice("event:".length).trim();
        } else if (line.startsWith("data:")) {
          dataLines.push(line.slice("data:".length).trimStart());
        }
      }

      if (dataLines.length > 0) {
        onEvent(eventName, dataLines.join("\n"));
      }
    }
  }
}

// Example:
// listenToMessageStream("http://localhost:8080", process.env.API_JWT_TOKEN);
```

To exercise the stream locally, open a terminal running the listener above, then publish a message with
`POST /messages` (requires `api:write`). The listener should print lines like `[message] hello from mqtt`.

### Backing Off With Jitter

When the API answers `429 Too Many Requests`, the user is already at the per-user stream cap (see
[Connections Per User](#connections-per-user)). Retrying at once only adds load and will keep failing until one of their
other streams closes. The same goes for `502`, `503`, `504`, and network errors, when the API or the broker is having
trouble: if every client retries on the same schedule, they all reconnect together and knock it over again.

The usual fix is exponential backoff with "full jitter": after each failure, double a ceiling (up to a maximum) and wait
a random time between zero and that ceiling. The randomness spreads out clients that failed at the same moment.

This builds on `listenToMessageStream` from the JavaScript example above, with two changes. It takes an `AbortSignal`,
and it throws an error that carries the HTTP status:

```javascript
class HttpStatusError extends Error {
  constructor(status, retryAfter, body) {
    super(`HTTP ${status}: ${body}`);
    this.status = status;
    this.retryAfter = retryAfter;
  }
}

async function listenToMessageStream(apiBaseUrl, accessToken, onEvent, signal) {
  const url = `${apiBaseUrl.replace(/\/$/, "")}/messages/event-stream`;

  const response = await fetch(url, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      Accept: "text/event-stream",
    },
    signal,
  });

  if (!response.ok) {
    throw new HttpStatusError(
      response.status,
      response.headers.get("Retry-After"),
      await response.text(),
    );
  }

  // ... the rest is unchanged: read response.body and call onEvent(eventName, data) for each event.
}
```

The retry loop:

```javascript
// 429: over the per-user stream cap. 502/503/504: the API or the broker is having trouble.
const RETRYABLE_STATUSES = new Set([429, 502, 503, 504]);

function sleep(ms, signal) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(signal.reason);
      return;
    }
    const onAbort = () => {
      clearTimeout(timer);
      reject(signal.reason);
    };
    const timer = setTimeout(() => {
      signal?.removeEventListener("abort", onAbort);
      resolve();
    }, ms);
    signal?.addEventListener("abort", onAbort, { once: true });
  });
}

async function streamWithBackoff({
  apiBaseUrl,
  getAccessToken,
  onEvent,
  signal,
  baseDelayMs = 1_000,
  maxDelayMs = 60_000,
  healthyAfterMs = 30_000,
}) {
  let attempt = 0;

  while (!signal?.aborted) {
    const startedAt = Date.now();
    let retryAfterMs = 0;

    try {
      // Ask for a token on every attempt, so a refreshed token is picked up after a long outage.
      await listenToMessageStream(apiBaseUrl, await getAccessToken(), onEvent, signal);
      // The server closed the stream normally (for example during a deployment). Reconnect, with a delay as well.
    } catch (error) {
      if (signal?.aborted) {
        return;
      }
      if (error instanceof HttpStatusError) {
        if (!RETRYABLE_STATUSES.has(error.status)) {
          // 400, 401, 403: retrying the same request will not help. Let the caller refresh the token or report it.
          throw error;
        }
        // The API does not send Retry-After today, but respect it if a proxy or a later version does.
        retryAfterMs = (Number(error.retryAfter) || 0) * 1_000;
      }
      // Anything else is a network error or a dropped connection, which is worth retrying.
    }

    // A stream that stayed up for a while was healthy, so the next failure starts again from a short delay.
    attempt = Date.now() - startedAt >= healthyAfterMs ? 1 : attempt + 1;

    const ceilingMs = Math.min(maxDelayMs, baseDelayMs * 2 ** (attempt - 1));
    const delayMs = Math.max(retryAfterMs, Math.random() * ceilingMs);

    try {
      await sleep(delayMs, signal);
    } catch {
      return; // Aborted while waiting.
    }
  }
}
```

With the defaults, the waits are random within 0 to 1 second, then 0 to 2, 0 to 4, and so on up to 0 to 60 seconds.

Usage:

```javascript
const stop = new AbortController();

streamWithBackoff({
  apiBaseUrl: "https://api.example.com",
  getAccessToken: () => auth.getAccessToken(), // Your OIDC client.
  onEvent: (eventName, data) => console.log(`[${eventName}] ${data}`),
  signal: stop.signal,
}).catch((error) => console.error("Event stream stopped:", error));

// Later, for example on sign-out or when the component unmounts:
stop.abort();
```

### Sharing One Stream Between Tabs

Every open tab with its own stream uses one of the user's slots. A user with four tabs open at a cap of 3 sees the
fourth tab get `429` and back off forever. Sharing one stream between tabs avoids that, and it also saves a broker
connection per tab.

The approach below uses two browser APIs:

* **Web Locks** (`navigator.locks`) pick a leader. Only one tab at a time holds the lock, and the browser releases it
  when that tab closes or crashes, so a waiting tab takes over without any heartbeat code.
* **`BroadcastChannel`** passes each event from the leader to the other tabs of the same origin.

```javascript
function shareMessageStream({ streamKey, apiBaseUrl, getAccessToken, onEvent, signal }) {
  const channel = new BroadcastChannel(streamKey);
  channel.onmessage = (message) => onEvent(message.data.eventName, message.data.data);
  signal?.addEventListener("abort", () => channel.close(), { once: true });

  // Every tab queues for the lock. The callback runs in one tab at a time, and the lock is held until it returns.
  return navigator.locks
    .request(`${streamKey}:leader`, { signal }, () =>
      streamWithBackoff({
        apiBaseUrl,
        getAccessToken,
        signal,
        onEvent: (eventName, data) => {
          // A BroadcastChannel does not deliver to the tab that posted, so the leader handles its own copy here.
          onEvent(eventName, data);
          channel.postMessage({ eventName, data });
        },
      }),
    )
    .catch((error) => {
      if (error.name !== "AbortError") {
        throw error;
      }
    });
}
```

Usage is the same as before, plus a key:

```javascript
const stop = new AbortController();

shareMessageStream({
  // Include the user, so two accounts signed in to the same browser (in different tabs) do not share a stream.
  streamKey: `example-messages:${userId}`,
  apiBaseUrl: "https://api.example.com",
  getAccessToken: () => auth.getAccessToken(),
  onEvent: (eventName, data) => console.log(`[${eventName}] ${data}`),
  signal: stop.signal,
});
```

Things to know before relying on it:

* **Sharing is per origin and per browser profile.** Other browsers, other devices, and private windows each elect
  their own leader and use their own slot. The cap still has to allow for those.
* **Events can be missed during a handover.** When the leader tab closes, there is a short gap before the next tab
  connects. This template's stream has no event IDs or replay, so anything published in that gap is lost. If that
  matters, have the new leader reload current state after it connects.
* **Mobile browsers may freeze background tabs.** A frozen leader can keep the lock without receiving anything. If
  that is a problem, abort the stream on `visibilitychange` when the page is hidden, and request the lock again when it
  becomes visible.
* **Stop it on sign-out.** Abort the signal in every tab (for example by broadcasting a sign-out message), or a
  leftover leader keeps streaming with the old user's token until that token expires.
* A `SharedWorker` that holds the only connection is another way to do this. Support for it has historically been
  missing on some mobile browsers, so check your audience's browsers before choosing it. Web Locks and
  `BroadcastChannel` keep everything in the page.

## Python

The local-testing scripts in the `test/local/scripts/messages/` directory has a Python-based example of listening for
events.

## TypeScript

The TypeScript example uses the same `fetch`-and-stream parsing approach as the JavaScript example above, with typed
parameters and a stricter null check on `response.body`. It runs in Node.js 18+ (with DOM lib types) or in a browser
build that provides `fetch`.

```typescript
async function listenToMessageStream(
  apiBaseUrl: string,
  accessToken: string,
  onEvent: (eventName: string, data: string) => void = (eventName, data) => {
    console.log(`[${eventName}] ${data}`);
  },
): Promise<void> {
  const url = `${apiBaseUrl.replace(/\/$/, "")}/messages/event-stream`;

  const response = await fetch(url, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      Accept: "text/event-stream",
    },
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${await response.text()}`);
  }

  if (!response.body) {
    throw new Error("Response body is null.");
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  console.log(`Listening on ${url} (Ctrl+C to stop)...`);

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });

    let eventBoundary: number;
    while ((eventBoundary = buffer.indexOf("\n\n")) >= 0) {
      const block = buffer.slice(0, eventBoundary);
      buffer = buffer.slice(eventBoundary + 2);

      let eventName = "message";
      const dataLines: string[] = [];

      for (const line of block.split("\n")) {
        if (line.startsWith("event:")) {
          eventName = line.slice("event:".length).trim();
        } else if (line.startsWith("data:")) {
          dataLines.push(line.slice("data:".length).trimStart());
        }
      }

      if (dataLines.length > 0) {
        onEvent(eventName, dataLines.join("\n"));
      }
    }
  }
}

// Example:
// listenToMessageStream("http://localhost:8080", process.env.API_JWT_TOKEN!);
```

# Other Resources

The following links have useful reading material relating to Server-Sent Events and MQTT:

* https://milanjovanovic.tech/blog/server-sent-events-in-aspnetcore-and-dotnet-10
* https://docs.aws.amazon.com/whitepapers/latest/designing-mqtt-topics-aws-iot-core/mqtt-design-best-practices.html
* https://github.com/dotnet/MQTTnet/wiki/Tips-and-Tricks
* https://www.hivemq.com/blog/mqtt-essentials-part-5-mqtt-topics-best-practices/
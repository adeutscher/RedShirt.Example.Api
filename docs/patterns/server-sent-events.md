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

# Special Cases

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

# Client Examples

## JavaScript

The example stream lives at `GET /messages/event-stream`. Each event uses the SSE event name `message`, and the
`data` field is the plain message text (not JSON). The endpoint requires a bearer token with the `api:read` scope.

Browsers cannot set custom headers on the built-in `EventSource` API, so the example below uses `fetch` and reads the
response body as a stream. This works in modern browsers and in Node.js 18+.

```javascript
async function listenToMessageStream(apiBaseUrl, accessToken) {
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
        console.log(`[${eventName}] ${dataLines.join("\n")}`);
      }
    }
  }
}

// Example:
// listenToMessageStream("http://localhost:8080", process.env.API_JWT_TOKEN);
```

To exercise the stream locally, open a terminal running the listener above, then publish a message with
`POST /messages` (requires `api:write`). The listener should print lines like `[message] hello from mqtt`.
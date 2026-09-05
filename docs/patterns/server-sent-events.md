# Client Events / Server-Sent Events

This document talks a bit about this template's usage of Server-Sent Events to send live updates to connected clients.

# Special Cases

## AWS IoT Endpoint Resolution

During local testing, it was discovered that pointing the Broker URL didn't cut it for connecting to MiniStack in the
local Docker Compose stack.

This problem is in part a product of our choices to use MiniStack's IoT emulator for local testing. However, I am
choosing to keep this feature in the template because it's possible (though not required) to resolve a broker URL using
the AWSSDK.IoT client. It will return an IoT data-plane hostname that is stable for a given account and region. That
said, I'm also acknowledging that AWS deployments are not everyone's end goal. In the same vein as the rest of this
template, the AWSSDK.IoT operations have been quarantined in a separate project,
`RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws` in order to limit the scope of the removal.

If you want to remove the use of the `AWSSDK.IoT` altogether, then {END OF SESSION, WILL FINISH LATER}
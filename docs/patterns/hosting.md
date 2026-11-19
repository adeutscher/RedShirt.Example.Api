# Hosting Hardening

This document covers how the API protects its HTTP responses and which settings a deployment should review.

## Response Headers

Every response gets these headers, including error responses from the exception handler and status code pages:

| Header                    | Value                                        | Purpose                                                |
| ------------------------- | -------------------------------------------- | ------------------------------------------------------ |
| `X-Content-Type-Options`  | `nosniff`                                    | Browsers use the declared content type, never a guess. |
| `X-Frame-Options`         | `DENY`                                       | Responses cannot be framed (older browsers).           |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'` | Nothing loads from or frames an API response.          |
| `Referrer-Policy`         | `no-referrer`                                | No referrer is sent on from an API response.           |
| `Cache-Control`           | `no-store`                                   | Shared caches do not keep customer or order data.      |

Notes:

* `Cache-Control` is only added when nothing else set a caching policy, so an endpoint can still opt into caching.
* Swagger UI (served under `/swagger` in the Development environment only) runs inline scripts, so it gets every header
  except the content security policy.
* Outside Development, HSTS and HTTPS redirection are also on.

The headers come from `UseSecurityHeaders` in `src/RedShirt.Example.Api/Extensions/SecurityHeadersExtensions.cs`.

## Allowed Hosts

ASP.NET Core host filtering rejects requests whose `Host` header is not in `AllowedHosts` with a 400. This stops links
and redirects built from the `Host` header from pointing at a host an attacker chose.

| Environment variable | Example                                | Notes                                     |
| -------------------- | -------------------------------------- | ----------------------------------------- |
| `ALLOWED_HOSTS`      | `api.example.com;api.internal.example` | `;`-separated. `ALLOWEDHOSTS` also works. |

* `appsettings.json` does not set `AllowedHosts`. Unset means any host is allowed.
* `appsettings.Development.json` sets `*`, so local runs accept any host without a warning.
* Outside Development, the API logs a startup warning while any host is allowed. It does not refuse to start: the
  template cannot know the ingress host names.

List every name clients use to reach the API. That includes health probes and load balancers that call the container
by IP address or an internal name, or their requests will get a 400.

## Behind a Reverse Proxy

`X-Forwarded-*` headers are ignored unless `FORWARDING_HEADERS__RESPECT_HEADERS` is `true`. They are then only accepted
from the networks listed in `FORWARDING_HEADERS__TRUSTED_NETWORKS__0`, `FORWARDING_HEADERS__TRUSTED_NETWORKS__1` and so
on (CIDR notation). See the `api` service in `test/local/docker-compose.yaml` for an example.

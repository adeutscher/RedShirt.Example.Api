# Authentication

This document describes authentication behaviour for this API.

## Authorization vs. Authentication

Small reminder that **authorization** is different from **authentication**:

* "**Authentication**" is centred around the question of "who are you?". In order to pass authentication, then you
  simply need to be somebody identifiable.
* "**Authorization**" is centred around the question of "what are you allowed to do?".

## Logic Flow

1. A user receives a JWT token from an identity provider such as Keycloak, Okta, 0Auth, or similar.
    * A JWT token contains a series of claims about the user.
    * A JWT token includes a signature so that a malicious user cannot meddle with the provided claims (the format of
      the JWT token itself is 3 sections of base64-encoded data).
    * The most significant claim used for authentication by this template is the user's list of roles.
2. The user's claim is enriched by a transformer.
    * In this template, roles are translated to a set of hardcoded permissions.
    * The transformer inherits from `IClaimsTransformation` which defines the async `TransformAsync` method, so making
      some sort of callout to resolve roles to permissions isn't out of the question. However, given that it runs with
      every request some per-instance caching is probably in order.
3. Authorization policies are created according to calling `AddAuthentication` (an extension on `IServiceCollection`).
    * Authorization falls back to a defined fallback policy if a specific policy is not named.
    * Authorization policies have requirements.
        * Requirements can check claims. In this template, that claim is most often a permission requirement set by the
          transformer.
        * Requirements can also be customized. For example, the read-only policies on this template have an additional
          requirement that sanity-checks that the request was an HTTP GET. It's more of a sanity-check for the developer
          applying the authorization policy, but it gets the point across that permission claims are not the only kind
          of requirement that one can set.
4. API controllers and/or endpoints can be assigned an authorization policy.
    * If no authorize policy is specified, and the `AllowAnonymous` attribute isn't used, then the configured fallback
      policy shall be used.
    * Many `Authorize` attributes in this template are centralized/shorthanded behind bespoke attributes that inherit
      from `AuthorizeAttribute`:

    ```csharp
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ApproveOrderReadOnlyAttribute : AuthorizeAttribute
    {
        public ApproveOrderReadOnlyAttribute()
        {
            Policy = BespokeAuthorizationPolicies.OrderReadApproved;
        }
    }
    ```

Recapped in reverse:

1. A controller/endpoint is assigned a policy, which is set up with requirements.
2. Requirements may be derived from request properties or the request's identity (as detailed by a JWT token).
3. The request's identity information may be enriched by a transformer.

## On Disabling Authentication

As described above, authentication is a separate concept from authorization and authorization can have requirements can
have requirements other than the claims provided by the JWT token that authentication demands. ASP.NET always checks for
authorization policies, even if the web application was not set up with `UseAuthentication`. A phrasing of this is
"failing closed".

This means that a developer using this template should be aware that, for the moment, requests to endpoints that require
authorization (which is to say all of them at time of writing) will fail if authentication is disabled. For the moment,
this is intentional.

At time of writing, I flat-out don't have tidy answer to this conundrum for this template that I'm entirely happy with.
I could have fallback copies of policies that are defined if authentication is disabled, but it feels like a lot of
irresponsible duplication to give such a critical security feature such a clear off switch. The off switch is much too
powerful just for the sake of local testing. For the moment, logging this message is good enough. 


# Impersonation: acting as another identity

Part of the [documentation](index.md).

**Contents**

- [Impersonation: acting as another identity](#impersonation-acting-as-another-identity)
  - [Requesting a token](#requesting-a-token)
  - [How it is built](#how-it-is-built)
  - [The token](#the-token)
  - [Configuration](#configuration)
  - [Recording attempts](#recording-attempts)

`POST /api/v1/impersonation/tokens` (also served on the transitional unversioned alias, see [API versioning](http-contract.md#api-versioning)) lets an `Administrator` or `Support` caller mint a short-lived
bearer token that acts as **another identity**, so a support engineer can reproduce what a user sees
without knowing their credentials. It is available in every environment, and it is a **controlled
authentication bypass**: whoever can call it can become anyone. Every safeguard below is a hard
requirement, not an extra.

| Safeguard | What it does | Why |
| --- | --- | --- |
| Role gate | The `Impersonator` policy (`AdministratorRequirement("Administrator", "Support")`, answered by the existing `AdministratorAuthorizationHandler`) is checked by `AuthorizationBehavior` before validation or the handler run | Only staff who already hold an elevated role may attempt it; never anonymous |
| Mandatory reason | `reason` is required (10 to 500 characters after trimming, no control characters) and is written into the token and the audit record | An impersonation without a stated purpose cannot be reviewed afterwards |
| Separate signing key | Tokens are signed with `Impersonation:SigningKey`, which must differ from `Authentication:Jwt:SigningKey` (checked on start) | The two kinds of token stay distinguishable by who can sign them; leaking one key does not leak the other |
| No chaining | A caller already using an impersonation token is refused (`403`), even one that carries `Administrator` or `Support` | A minted token cannot be used to renew or widen itself |
| Assignable roles | A requested role must be in `Impersonation:AssignableRoles` | A role outside the list can never be granted, whoever asks |
| No escalation | Unless the caller is an `Administrator`, every requested role must be one the caller holds | A `Support` user cannot mint an `Administrator` token |
| Lifetime cap | The lifetime defaults to `DefaultLifetimeMinutes` and a request above `MaxLifetimeMinutes` is a `400` | The window a stolen token is useful is bounded |
| Audited, fail closed | Every attempt (issued, refused by the handler, refused by the policy, refused by validation) is written to the [audit stream](audit.md#audit-stream-a-separate-record-of-security-relevant-actions) before the response leaves; if the record cannot be written, no token is returned (`500`) | A token is never delivered unrecorded, and every later request made with it ties back to its mint through the `jti` |
| Off switch | `Impersonation:Enabled=false` turns the endpoint into a `404` and stops the bearer scheme accepting impersonation-key tokens | A deployment that does not want the feature has none of it |

## Requesting a token

```bash
curl -i -X POST http://localhost:5233/api/v1/impersonation/tokens \
  -H "Authorization: Bearer $SUPPORT" -H "Content-Type: application/json" \
  -d '{"targetUserId":"alice","roles":["Support"],"reason":"Reproducing the checkout error alice reported","ticketReference":"SUP-1234","lifetimeMinutes":15}'
```

`targetUserId` and `reason` are required; `roles` (default none), `ticketReference` (at most 100
characters) and `lifetimeMinutes` (default `DefaultLifetimeMinutes`) are optional. Impersonating
yourself is a validation error (`targetUserId` must differ from the caller's own id). The `200`
response carries `Cache-Control: no-store` (so does every response of this endpoint) and:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-03-02T14:20:00+00:00",
  "userId": "alice",
  "roles": ["Support"],
  "actorId": "sam",
  "tokenType": "Bearer"
}
```

Present it like any other token (`Authorization: Bearer <token>`). The roles granted are exactly the
ones requested: there is no user directory, so the target's real roles are not looked up.

| Status | When |
| --- | --- |
| `200` | The token was issued |
| `400` | Per-field errors: missing or short `reason`, missing or self `targetUserId`, lifetime not between 1 and the maximum, over-long or control-character text, more than 10 roles |
| `401` | No or invalid token (the fallback policy, before anything else, and also when the feature is switched off) |
| `403` | The caller lacks both roles; is already using an impersonation token; asked for a role outside `AssignableRoles`; or (as a non-administrator) asked for a role they do not hold. The problem `detail` names which |
| `404` | `Enabled` is `false`, for every authenticated caller (an unqualified caller is not told the endpoint exists) |

## How it is built

It is a normal vertical slice, `Application/Features/Impersonation/IssueToken/`, following
[Adding a new command or query](adding-a-command.md#adding-a-new-command-or-query) and
[Configuring role-based authorization for a new command](authorization.md#configuring-role-based-authorization-for-a-new-command):

- `IssueImpersonationTokenCommand` is an `ICommand<IssueImpersonationTokenResult>` (not
  transactional), an `IRequiresAuthorization` request for the `Impersonator` policy and an
  `IAuditableRequest` (action `Impersonation.IssueToken`, fail closed).
- `IssueImpersonationTokenResult` is `union(ImpersonationToken, ValidationErrors, NotAuthorized, Error)`
  implementing `IValidatable` and `IAuthorizable`. `Error` (code `IMPERSONATION_DISABLED`, mapped to
  `404`) is the handler's own refusal when the switch is off; the controller checks the switch first
  so that an unqualified caller sees a `404` and not a `403`.
- `IssueImpersonationTokenValidator` holds the input rules; `IssueImpersonationTokenHandler` holds the
  decisions (no chaining, assignable roles, no escalation) and returns `NotAuthorized`, never throws.
- The Application layer references no JWT library (an architecture test enforces it). It calls
  `IImpersonationTokenIssuer`; `JwtImpersonationTokenIssuer` in `Api/Impersonation/` signs the token.
  The rules it needs (`IImpersonationSettings`) are implemented by `ImpersonationOptions`.

## The token

An HS256 JWT with the same issuer and audience as ordinary tokens, signed with the impersonation key.
The bearer scheme accepts tokens signed with either key (`TokenValidationParameters.IssuerSigningKeys`)
and judges both identically: HS256 only, issuer, audience, lifetime and signature all validated.

| Claim | Value |
| --- | --- |
| `sub` | The target user id; becomes `ClaimTypes.NameIdentifier`, so the token owns what it creates |
| `role` | One per granted role; becomes `ClaimTypes.Role` |
| `act` | The RFC 8693 actor claim: a JSON object naming the real caller, `{"sub":"<caller id>"}` |
| `impersonated` | The marker, `true` |
| `imp_reason`, `imp_ticket` | The recorded reason, and the ticket reference when one was given |
| `jti`, `iat`, `nbf`, `exp` | Unique id and whole-second times; `exp` is exactly the returned `expiresAt` |

`act` is written as a nested JSON object and validated by the same `JsonWebTokenHandler` the bearer
scheme uses; on the resulting `ClaimsPrincipal` it is a single claim of type `act` whose value is the
JSON text `{"sub":"..."}` (`impersonated`, `imp_reason` and `imp_ticket` arrive under their own names,
untouched by inbound claim mapping). The application reads them through `ImpersonationClaims`
(`Application/Common/Authorization/`): `principal.IsImpersonated()` (the marker or an `act` claim) and
`principal.GetActorId()` (the `sub` of `act`, or `null`). An integration test proves the round trip
from signing through validation to those helpers.

## Configuration

`Impersonation` section, `ImpersonationOptions`, validated on start (DataAnnotations by the
source-generated `ImpersonationOptionsValidator`, the cross-field rules by `ImpersonationOptionsRules`):

| Setting | Meaning |
| --- | --- |
| `Enabled` | Default `true`. When `false`: the endpoint is `404`, the impersonation key is not trusted, no key is required |
| `SigningKey` | Required while enabled: at least 32 characters, different from the ordinary key. Only `appsettings.Development.json` carries one, labelled development-only; any other environment supplies it through user secrets, `Impersonation__SigningKey` or a secret store, and an enabled host without one **refuses to start** |
| `DefaultLifetimeMinutes` | Default 15; 1 to 1440 and not above the maximum |
| `MaxLifetimeMinutes` | Default 60; 1 to 1440 |
| `AssignableRoles` | Roles a token may carry; `appsettings.json` lists `Support` and `Administrator`. An empty list means plain identities only |

## Recording attempts

The handler records nothing itself. The command opts into the audit stream, and `AuditBehavior`, which
wraps the authorization and validation behaviors, writes one `Impersonation.IssueToken` event for every
attempt: the token issued, a refusal by the handler's rules, a refusal by the `Impersonator` policy (with
the caller as actor) and a validation failure. Each event names the real caller, the target, the roles
granted or asked for, the reason, the ticket, the denial message, and for a mint the `jti` of the new
token; every later request made with that token is recorded too (`Impersonation.Request`, carrying the
same `tokenId`). The token itself is never recorded (`ImpersonationToken.ToString()` omits it too). The
endpoint answering `404` while switched off happens before the pipeline and is not audited: no attempt
can succeed. See [Audit stream](audit.md#audit-stream-a-separate-record-of-security-relevant-actions).

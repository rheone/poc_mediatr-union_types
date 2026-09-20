# Speculative shared case types for a larger API

Part of the [documentation](index.md).

Not implemented here, but worth having in your vocabulary for a real project — none of these map
1:1 onto an HTTP status, deliberately. Like the shared case types actually used in this repo, each
one below is meant to be a meaning-free record reused across many unions, not tied to any single
operation:

| Case                                    | Meaning                                                                 |
| ----------------------------------------- | -------------------------------------------------------------------------- |
| `Accepted(jobId)`                        | Work was queued/deferred, not completed synchronously                     |
| `Conflict(currentVersion)`               | [Optimistic-concurrency](glossary.md#cross-cutting-concepts) version mismatch on update *(the `Conflict` case actually used here is the simpler `Conflict(message)` for a duplicate product name; a version mismatch is `PreconditionFailed`)* |
| `Locked(heldBy)`                         | Resource is [pessimistically locked](glossary.md#cross-cutting-concepts) by another process |
| `RateLimited(retryAfter)`                | Caller hit a throttling limit                                             |
| `Timeout(dependency)`                    | A downstream dependency didn't respond in time                           |
| `QuotaExceeded(limit, current)`          | A business quota (not a rate limit) was exceeded                          |
| `PartialSuccess(succeeded, failed)`      | A batch operation partially completed                                     |
| `AlreadyProcessed(idempotencyKey)`       | A duplicate request was detected and safely [ignored](glossary.md#cross-cutting-concepts) — see **Idempotency** |
| `RequiresConfirmation(prompt)`           | The action needs an explicit second confirmation before proceeding        |
| `Stale(asOf)`                            | Data was served from a cache/read-replica and may be out of date          |
| `Deprecated(replacement)`                | The operation still works but callers should migrate                      |

A queue consumer, a scheduled job, and an HTTP controller could all share the exact same
`Conflict`/`Locked`/`AlreadyProcessed` cases and each map them to something completely different
in their own boundary code.

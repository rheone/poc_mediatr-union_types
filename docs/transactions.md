# Transactions: what rollback undoes, and why it matters

Part of the [documentation](index.md).

A database transaction groups a set of writes so they succeed or fail *together*. `TransactionBehavior`
opens one before a [transactional command's](glossary.md#mediatr-vocabulary) handler runs, and decides after the
handler returns whether to **commit** (make every write inside it permanent) or **rollback** (undo
every write inside it, as if none of them had ever happened).

**Where this matters**: a handler that touches more than one repository, or writes to an entity
across more than one step, would otherwise risk a *partial write* — succeeding at step one and
failing at step two, leaving the database in a state no business rule ever intended to exist. A
transaction turns "did some of this succeed?" into a question that never needs asking: either the
whole unit of work happened, or none of it did.

**What actually gets undone**: everything the underlying `DbContext`'s change tracker recorded
during the handler's execution but never reached the database via `SaveChangesAsync` — inserts,
updates, and deletes alike. Nothing about this repo's own handler code has to remember what to
undo; the database transaction (plus `EfCoreUnitOfWork`'s change-tracker detach — see
[Notes and gotchas](notes-and-gotchas.md#notes-and-gotchas)) does that bookkeeping.

**Why this is a benefit, not just a safety net**: it lets a handler write code that assumes success
and bail out cleanly on any unexpected outcome, without manually tracking "what have I already done
that I'd need to compensate for." Compare a hypothetical handler with no transaction: if it updated
one row, then failed validating a second write, undoing the first row's change would need to be
written by hand, remembered, and tested — a whole category of bugs a transaction eliminates by
construction.

This repo decides commit-vs-rollback by asking the union itself
(`ITransactionOutcome<TSelf>.ShouldCommit`, [above](case-types.md#shared-case-types-are-meaning-free-transactionbehavior-cant-assume-what-a-case-means))
rather than by catching an exception — so rolling back a transaction is just as available for an
*expected*, successfully-returned outcome (`NotFound`, `ValidationErrors`) as it is for a genuine
fault, with no `catch` block required to trigger it. See
[Commit vs. rollback, message by message](request-lifecycle.md#commit-vs-rollback-message-by-message) below for this
traced through a concrete request.

# Unit of Work: one session, every repository

[`IUnitOfWork`](../src/MediatrUnionPoc.Domain/IUnitOfWork.cs) is registered once per request (a scoped
[dependency injection](glossary.md#cross-cutting-concepts) lifetime) and handed to every repository and
handler that needs it during that request — which means every repository sharing that same
`IUnitOfWork` is also sharing the same underlying `DbContext`/change-tracking session. A handler
that needs `IProductRepository` *and* a hypothetical `IOrderRepository` in the same operation gets
both backed by the same session automatically, purely from how dependency injection resolves scoped
services — no extra wiring required to keep them consistent with each other.

This is what lets `IUnitOfWork.CommitAsync()`/`RollbackAsync()` cover *every* repository touched
during the request with one call, instead of each repository having to save (or undo) its own
changes independently. That's the specific gap a **standalone repository or domain service method**
doesn't close on its own: a repository's own save method only knows about *its* entities —
coordinating a write that spans two repositories would need either the repositories to reference
each other (leaking persistence details across an otherwise clean boundary) or some other component
to explicitly own "did both succeed." [Unit of Work](glossary.md#architectural-patterns) is that "other
component" — it doesn't replace repositories or domain services, and a simple operation touching
exactly one repository doesn't need to reach for it: a `Product`-only handler here still calls
`IProductRepository` directly, with `IUnitOfWork` only mattering for its transaction boundary, not
as a required intermediary for every read or write.

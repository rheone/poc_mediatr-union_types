# The interview

One question at a time, in this order. Each names what it decides and how to recommend. Every question opens with one plain sentence on what it decides, written for someone new to the pattern, and every option carries its consequence in one sentence.

## Questions

**1. What should the operation do, and is it new or a change to an existing one?** Free text, one sentence. Decides the operation name (`{Verb}{Entity}`), feature area and entity, or which existing slice is changing. Offer two or three names in the project's naming style. For a change, skip the questions the existing slice already answers.

**2. Does it only read, or does it change something?** Decides query versus command, and whether it writes (transactional). Recommend from question 1.

**3. Which verb, route and success status?** Recommend from the project's routes and [PATTERN.md](PATTERN.md#http-layer). Typical: create is `POST` on the collection (`201`, `Location`); read `GET` (`200`); replace `PUT`; partial change `PATCH`; remove `DELETE` (`204`); an action on one record `POST` on a sub-route.

**4. What does the caller send?** Each input with its source (route, query, body, header), type and whether required. Name each input as the nearest sibling names the same concept on the wire (`pageNumber`, not `page`); recommend the sibling's names and say so. Confirm the list. Fields the caller must not set (owner, id, version, timestamps) are left off; the handler assigns them. Say so when the caller might expect otherwise.

**5. What are the rules for each input?** One input at a time. Options come from its type: not empty, maximum length, range, format, allowlist. Recommend a finite bound for every string, number and list; an unbounded input is a storage and denial-of-service risk.

**6. Should this input be a value object?** Per input that is an identity, a measure or a constrained string. Options: reuse an existing one (named from the survey), create one, keep the primitive. Recommend reuse when one wraps the same concept (the command still carries the primitive; the handler converts it with the existing type); a new one when the value has an invariant or must not be swapped with another value of the same primitive type; the primitive for free text with no rule. See [PATTERN.md](PATTERN.md#value-objects).

**7. Must a business rule keep a value unique?** Only for a command that writes. Yes adds an up-front check, a unique index and a conflict outcome that also covers a lost race. Recommend from the entity's existing rules.

**8. Does it change an existing record, and must the caller prove which version they saw?** Only when a record is changed. Options: required, optional, none. Recommend required when two callers could overwrite each other. Required adds a stale-version outcome and a conditional-request header.

**9. Who may do this?** See [PATTERN.md](PATTERN.md#authorization). Options:
- any signed-in caller;
- callers holding certain roles or groups (decided from the caller alone, in the pipeline); list the roles and policies the survey found;
- callers related to the record: its owner, a member of a group it belongs to, a role that permits this action on this kind of record, the same tenant (decided in the handler after the load).

Recommend the least the operation needs. When the scope comes from the caller's identity ("mine", "my tenant"), also ask what happens when the caller has no value for that claim; recommend an empty result or a refusal, never an unscoped result. A read that returns records is not exempt: ask whether every caller may see every record returned. When a record's existence is itself sensitive, a refused caller gets not-found instead of not-authorized.

**10. Which outcomes can it produce?** Ask also whether a state exists that the operation cannot act on (already done, wrong status): options are an idempotent success returning the record unchanged (recommended: safe to retry, no extra outcome) or a conflict. The union as a checklist built from the answers so far, each case with its HTTP status: success, not found, validation errors (per-field, or folded into an error case for a single input), conflict, stale version, not authorized, unexpected error. Recommend exactly the cases the answers imply; every declared case must be reachable.

**10b. Does this change what other operations should return, hide or accept?** Reads, lists, DTOs and other consumers of the changed data. Each affected operation is its own slice. Recommend finishing this slice first and recording the others as named follow-up slices in the spec, so none is forgotten. Ask which, if any, belong in this piece.

**11. Does it need a schema change?** Table, column, index, constraint or value converter. Recommend from questions 4 to 7. Where the project uses migrations, a migration is part of it.

**12. Should it be audited?** Only when the survey found an audit mechanism. Recommend yes for a change to security-relevant state or a refusal of someone who tried, with a best-effort failure policy unless the audit record is a precondition of the action.

**13. Does anything happen after the change that cannot be rolled back?** Email, search index, message. Recommend none. Otherwise it runs after the commit through a notification and the user accepts eventual consistency.

**14. Anything unusual about load or duration?** Bulk work, slow work, an unusual request media type, a new response header a browser client must read. Recommend the project's default limits; ask only when question 1 suggests otherwise.

**15. Confirm the seams and the first test.** Propose: an HTTP tracer-bullet test for the happy path; a handler test per branch; a validator test per rule; a union test for commit classification; a value-object test per new value object; a persistence test when the schema changes. Ask the user to confirm or adjust.

## The spec

Write it after the last question and ask the user to confirm.

| Item | Decision |
| --- | --- |
| Name, area, entity | |
| Kind and marker | query, command, or transactional command |
| Host style | controller or minimal API, as the project uses |
| Verb, route, success status, headers | |
| Inputs and rules | source, type, required, bounds |
| Value objects | reused, new (primitive, invariant), or primitive kept |
| Union cases | each case, its meaning here, its HTTP status |
| Follow-up slices | operations affected by this change, deferred by name |
| Validation representation | per-field case, or folded into an error case |
| Authorization | none; role or group in the pipeline; relationship in the handler; policy names |
| Uniqueness | the rule, or none |
| Concurrency | required, optional, or none |
| Transaction | which cases commit; how each commit failure maps |
| Persistence | schema changes, or none |
| Audit | action name and failure policy, or absent |
| After-commit effects | none, or the notification |
| Files to create | path and role, from the concern map |
| Files to touch | registration, contract snapshot, doc tables, request file |
| Seams and slice order | the tracer bullet, then the slices |

# Demo account foundation (phase 1)

An existing personal account can own one separate demo account. `Account.CreateDemoAccount`
requires an active, persisted personal owner. Persist the returned account through
`IAccountRepository.AddAsync` and the unit of work. Provisioning and seed data are deferred.

Demo accounts have their own account ID, no Microsoft identity or email, and cannot record
an independent login. The unique owner index prevents duplicate demos even during concurrent
creation. The self-reference uses restricted deletion. Database checks enforce identity shape;
the domain factory and resolver additionally enforce that the owner is active and personal.
Existing rows retain personal mode and their identity indexes.

`IAccountContextResolver.ResolveAsync(AccountMode.Personal)` preserves the existing
`ICurrentUserService` sign-in/provisioning path. Demo resolution gets that authenticated owner,
looks up only their linked demo, and rejects missing, inactive or unauthorized accounts.
Unknown enum values throw before identity resolution. No arbitrary target account ID is accepted,
and no demo failure falls back to personal.

This phase registers the resolver but does not introduce an HTTP selection header or change
controllers. Current endpoints remain personal. In the integration phase, parse selection
strictly, map invalid selection to an HTTP error, and pass the resolved `AccountId` consistently
to holdings, portfolios, conversation threads, messages and memory services. Use
`PersonalAccountId` for owner/audit context only. Existing account ownership checks must still
reject thread/portfolio IDs from the other account. End-to-end demo isolation is not enabled
until those entry points use the resolver together.

Migration `AddDemoAccountOwnership` adds nullable identity fields, mode, ownership, the unique
owner index and constraints. It has not been applied to a database by this task. Rollback is
blocked while demo rows exist, to avoid converting demo data into identity-bearing accounts.
Remove demo accounts and dependent data deliberately before rollback.

No demo seed data, UI switch, new sign-in flow or account-provisioning endpoint is included.

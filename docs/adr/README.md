# Architecture Decision Records

One file per decision, named `NNNN-kebab-case-title.md`, numbered in the order they were
accepted (`0001-...`, `0002-...`). Copy [template.md](./template.md) to start one.

An ADR is a record, not a proposal that gets edited into shape: once accepted, it is not
rewritten. If the decision changes later, write a new ADR and set the old one's status to
*Superseded by ADR-NNNN*. The trail of superseded decisions is the point - it shows why the
codebase is the way it is, including the turns that were tried and abandoned.

Worth writing an ADR for anything that is expensive to reverse: persistence strategy, auth
model, deployment target, a dependency the whole codebase leans on. Not worth it for choices
a single pull request can undo.

## Decisions still to be recorded

The scaffold made several choices that deserve a full ADR once the team has settled on them.
They are summarised in the root [README](../../README.md#architecture-decisions) for now:

| Topic | Current choice |
| --- | --- |
| Clean Architecture layering | Four projects, references point inward only |
| Dapper over EF Core | Hand-written SQL, no change tracking |
| Hand-rolled dispatcher | Avoids MediatR's commercial licence from v13 |
| DbUp for migrations | Versioned `.sql`, embedded and journalled |
| Turborepo + npm workspaces | Two Next.js apps, three shared packages |
| JWT with a limited-scope token | `token_type` claim plus an authorization policy |
| Rate limiting | Built-in .NET limiter, in-memory partitions |

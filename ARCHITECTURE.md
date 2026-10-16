# Architecture

`frontend` is the Next.js application form and result view. `backend` exposes one thin submission endpoint, a rule engine, SQLite persistence, and a background worker. `mock-service` is a small HTTP server that records deliveries in a local JSON file.

The rule engine takes independent `IDenialRule` implementations. A rule returns a reason when it denies. Add a new rule by implementing that interface and registering it in `Program.cs`; existing rules do not need edits.

For approved submissions, `LoanStore` opens one SQLite transaction. It inserts or updates the customer by SSN, inserts or updates the customer's application, and writes an outbox row before commit. A failure before commit rolls back all three changes. Denied applications write nothing.

`OutboxWorker` polls committed outbox rows and sends them to the mock service over HTTP after the request finishes. It uses POST for a new customer and PUT for a returning customer. A failed call leaves the event pending for retry. The mock stores by customer ID, so repeated delivery does not create duplicate external records. External HTTP delivery itself cannot be part of the local database transaction; the outbox makes event publication durable and atomic with the database writes.

This version chooses SQLite to keep setup small and transactional without Docker. The API and mock are single-process demos. Concurrent submissions for one SSN rely on SQLite's unique constraint and transaction locking; polished conflict handling, migrations, authentication, real SSN protection, and production delivery controls are out of scope for this first pass.

# Architecture

`frontend` is the Next.js application form, approval result, and denial page. `backend` exposes one submission endpoint, an application service, a rule engine, SQLite persistence, and a background worker. `mock-service` is a small HTTP server that records deliveries in a local JSON file. `backend.tests` covers the main behavior.

The rule engine takes independent `IDenialRule` implementations. A rule returns a reason when it denies. Add a new rule by implementing that interface and registering it in `Program.cs`; existing rules do not need edits.

`SubmitApplication` validates and orchestrates the submission. The endpoint only maps its outcome to HTTP. It depends on `ILoanStore`; SQLite is the current implementation.

For approved submissions, `LoanStore` opens one SQLite transaction. It inserts or updates the customer by SSN, inserts or updates the customer's application, and writes an outbox row before commit. A failure before commit rolls back all three changes. Denied applications write nothing.

`OutboxWorker` polls committed outbox rows after the request finishes. It calls `IExternalCustomerClient`, whose HTTP implementation uses POST for a new customer and PUT for a returning customer. A failed call leaves the event pending for retry. The mock stores by customer ID, so repeated delivery does not create duplicate external records. External HTTP delivery itself cannot be part of the local database transaction; the outbox makes event publication durable and atomic with the database writes.

This version chooses SQLite to keep setup small and transactional. Docker Compose packages the three services and keeps SQLite and mock records in separate named volumes; the same services also run directly on the host. The browser calls the API through published localhost port 5050, while the API reaches the mock by its Compose service name. The API and mock are single-process demos. Concurrent submissions for one SSN rely on SQLite's unique constraint and transaction locking; polished conflict handling, migrations, authentication, real SSN protection, and production delivery controls are out of scope for this first pass.

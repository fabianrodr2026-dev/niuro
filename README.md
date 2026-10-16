# Fundo loan application demo

Video: not recorded yet.

This is an initial local implementation of the [Fundo full-stack challenge](https://fundo-llc.github.io/fundo-take-home/full-stack-engineer-challenge/). It includes a Next.js form, a .NET API with SQLite, and a small mock external service. Docker is not used.

## Requirements

- .NET 8 SDK
- Node.js 20 or newer

## Run locally

Open three terminals from the repository root:

```powershell
cd mock-service
npm run dev
```

```powershell
cd backend
dotnet restore
dotnet run
```

```powershell
cd frontend
npm install
npm run dev
```

Visit http://localhost:3000. The API listens on http://localhost:5050 and the mock service on http://localhost:5051. The SQLite database is created at `backend/data/fundo.db`. External records can be inspected at http://localhost:5051/customers.

## Sample flows

Use fictional details only. For each case, enter a name, address, company, and a positive requested amount.

| Result | State | SSN |
| --- | --- | --- |
| Approved | CA | 123-45-6789 |
| Denied by state | NY | 123-45-6789 |
| Denied by blacklist | CA | 000-00-0000 |
| Returning customer | CA | 123-45-6789 again, with a changed name or amount |

The mock service stores one record per customer ID. It may take a few seconds to reflect a submission. If it is offline, the API retains the event and retries while running.

## Current scope

This is an initial implementation. Automated tests, a recorded walkthrough, and a public repository link are still outstanding. SSNs are stored in plain text in this local demo and must never be real personal data. See [ARCHITECTURE.md](ARCHITECTURE.md) for decisions and trade-offs.

using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Fundo.Api;

public sealed record SaveResult(long ApplicationId, bool ReturningCustomer);
public sealed record PendingEvent(long Id, string Payload);

public sealed class LoanStore(IConfiguration configuration)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = configuration["DatabasePath"] ?? Path.Combine("data", "fundo.db")
    }.ToString();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Initialize()
    {
        var path = new SqliteConnectionStringBuilder(_connectionString).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY, ssn TEXT NOT NULL UNIQUE, first_name TEXT NOT NULL, last_name TEXT NOT NULL, address TEXT NOT NULL, state TEXT NOT NULL, company_name TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS applications (id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL UNIQUE REFERENCES customers(id), requested_amount TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS outbox (id INTEGER PRIMARY KEY, payload TEXT NOT NULL, delivered_at TEXT);
            """;
        command.ExecuteNonQuery();
    }

    public SaveResult SaveApproved(LoanRequest request)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        long? customerId;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM customers WHERE ssn = $ssn";
            command.Parameters.AddWithValue("$ssn", request.Ssn.Trim());
            customerId = command.ExecuteScalar() as long?;
        }

        var returning = customerId.HasValue;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = returning
                ? "UPDATE customers SET first_name=$first, last_name=$last, address=$address, state=$state, company_name=$company WHERE id=$id"
                : "INSERT INTO customers (ssn, first_name, last_name, address, state, company_name) VALUES ($ssn, $first, $last, $address, $state, $company)";
            command.Parameters.AddWithValue("$ssn", request.Ssn.Trim());
            command.Parameters.AddWithValue("$first", request.FirstName.Trim());
            command.Parameters.AddWithValue("$last", request.LastName.Trim());
            command.Parameters.AddWithValue("$address", request.Address.Trim());
            command.Parameters.AddWithValue("$state", request.State.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("$company", request.CompanyName.Trim());
            command.Parameters.AddWithValue("$id", customerId ?? 0);
            command.ExecuteNonQuery();
        }
        if (!customerId.HasValue)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT last_insert_rowid()";
            customerId = (long)command.ExecuteScalar()!;
        }

        long applicationId;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = returning
                ? "UPDATE applications SET requested_amount=$amount WHERE customer_id=$customer; SELECT id FROM applications WHERE customer_id=$customer"
                : "INSERT INTO applications (customer_id, requested_amount) VALUES ($customer, $amount); SELECT last_insert_rowid()";
            command.Parameters.AddWithValue("$customer", customerId.Value);
            command.Parameters.AddWithValue("$amount", request.RequestedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            applicationId = (long)command.ExecuteScalar()!;
        }

        var payload = JsonSerializer.Serialize(new
        {
            customerId,
            applicationId,
            returningCustomer = returning,
            request.FirstName,
            request.LastName,
            request.Address,
            request.State,
            request.CompanyName,
            request.RequestedAmount,
            request.Ssn
        });
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO outbox (payload) VALUES ($payload)";
            command.Parameters.AddWithValue("$payload", payload);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
        return new SaveResult(applicationId, returning);
    }

    public PendingEvent? NextEvent()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, payload FROM outbox WHERE delivered_at IS NULL ORDER BY id LIMIT 1";
        using var reader = command.ExecuteReader();
        return reader.Read() ? new PendingEvent(reader.GetInt64(0), reader.GetString(1)) : null;
    }

    public void MarkDelivered(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE outbox SET delivered_at = datetime('now') WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
}

namespace Fundo.Api;

public interface ILoanStore
{
    SaveResult SaveApproved(LoanRequest request);
}

public sealed record SubmissionResult(bool Approved, string? Reason = null, long? ApplicationId = null, bool ReturningCustomer = false);

public sealed class SubmitApplication(RuleEngine rules, ILoanStore store)
{
    public SubmissionResult Execute(LoanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.State) || string.IsNullOrWhiteSpace(request.Ssn) ||
            request.State.Trim().Length != 2 || request.RequestedAmount <= 0 ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Ssn.Trim(), @"^\d{3}-\d{2}-\d{4}$"))
            throw new ArgumentException("Complete every field, use a two-letter state and SSN in 123-45-6789 format, and enter a positive amount.");

        var decision = rules.Decide(request);
        if (!decision.Approved) return new SubmissionResult(false, decision.Reason);

        var saved = store.SaveApproved(request);
        return new SubmissionResult(true, ApplicationId: saved.ApplicationId, ReturningCustomer: saved.ReturningCustomer);
    }
}

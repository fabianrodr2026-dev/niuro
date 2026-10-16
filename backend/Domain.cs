namespace Fundo.Api;

public sealed record LoanRequest(string FirstName, string LastName, string Address, string State, string CompanyName, decimal RequestedAmount, string Ssn);
public sealed record Decision(bool Approved, string? Reason);
public interface IDenialRule
{
    string? Evaluate(LoanRequest request);
}

public sealed class NewYorkRule : IDenialRule
{
    public string? Evaluate(LoanRequest request) => string.Equals(request.State.Trim(), "NY", StringComparison.OrdinalIgnoreCase) ? "Applications from NY are not supported." : null;
}

public sealed class BlacklistRule(IReadOnlySet<string> blacklistedSsns) : IDenialRule
{
    public string? Evaluate(LoanRequest request) => blacklistedSsns.Contains(request.Ssn.Trim()) ? "This SSN cannot be approved." : null;
}

public sealed class RuleEngine(IEnumerable<IDenialRule> rules)
{
    public Decision Decide(LoanRequest request)
    {
        foreach (var rule in rules)
        {
            var reason = rule.Evaluate(request);
            if (reason is not null) return new Decision(false, reason);
        }
        return new Decision(true, null);
    }
}

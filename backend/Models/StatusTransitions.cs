namespace Requests.Models;

/// <summary>
/// </summary>
public static class StatusTransitions
{
    public const string New        = "New";
    public const string InProgress = "InProgress";
    public const string Waiting    = "Waiting";
    public const string Resolved   = "Resolved";
    public const string Closed     = "Closed";
    public const string Cancelled  = "Cancelled";

    public static readonly IReadOnlySet<string> AllStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            New, InProgress, Waiting, Resolved, Closed, Cancelled
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [New]        = Set(InProgress, Cancelled),
            [InProgress] = Set(Waiting, Resolved, Cancelled),
            [Waiting]    = Set(InProgress, Resolved, Cancelled),
            [Resolved]   = Set(InProgress, Closed),
            [Closed]     = Set(),          // סופי
            [Cancelled]  = Set(New)        // ניתן להחזיר לחדש
        };

    public static bool IsKnown(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AllStatuses.Contains(status);

    public static bool IsAllowed(string from, string to) =>
        Map.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlySet<string> TargetsFrom(string from) =>
        Map.TryGetValue(from, out var targets) ? targets : Set();

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}
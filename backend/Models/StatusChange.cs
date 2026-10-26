using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Models;

public class StatusChange
{
    public long Id { get; set; }
    public int RequestId { get; set; }
    public ServiceRequest? Request { get; set; }

    public string PreviousStatusRaw { get; set; } = nameof(Status.New);
    public string NewStatusRaw { get; set; } = nameof(Status.New);

    [NotMapped]
    public Status PreviousStatus
    {
        get => Enum.TryParse<Status>(PreviousStatusRaw, true, out var v) ? v : Status.New;
        set => PreviousStatusRaw = value.ToString();
    }

    [NotMapped]
    public Status NewStatus
    {
        get => Enum.TryParse<Status>(NewStatusRaw, true, out var v) ? v : Status.New;
        set => NewStatusRaw = value.ToString();
    }

    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = "system";
}
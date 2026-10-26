using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Models;

public class ServiceRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string OrganizationName { get; set; } = "";

    // The table stores these as nvarchar(20). These are the mapped columns;
    // the enums below are a [NotMapped] view over them.
    public string StatusRaw { get; set; } = nameof(Status.New);
    public string PriorityRaw { get; set; } = nameof(Priority.Medium);

    [NotMapped]
    public Status Status
    {
        get => Enum.TryParse<Status>(StatusRaw, true, out var v) ? v : Status.New;
        set => StatusRaw = value.ToString();
    }

    [NotMapped]
    public Priority Priority
    {
        get => Enum.TryParse<Priority>(PriorityRaw, true, out var v) ? v : Priority.Medium;
        set => PriorityRaw = value.ToString();
    }

    public string? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<StatusChange> StatusChanges { get; set; } = new();
}
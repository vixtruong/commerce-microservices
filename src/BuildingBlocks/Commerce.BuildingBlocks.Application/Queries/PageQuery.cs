using System.ComponentModel.DataAnnotations;

namespace Commerce.BuildingBlocks.Application.Queries;

/// <summary>Defines bounded filters shared by service-owned read use cases.</summary>
public sealed class PageQuery
{
    /// <summary>Gets or sets the one-based page.</summary>
    [Range(1, 100000)] public int Page { get; set; } = 1;
    /// <summary>Gets or sets the maximum number of records in a response.</summary>
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    /// <summary>Gets or sets a bounded text or identifier search.</summary>
    [MaxLength(200)] public string? Search { get; set; }
    /// <summary>Gets or sets the persisted status filter.</summary>
    [MaxLength(40)] public string? Status { get; set; }
    /// <summary>Gets or sets the inclusive UTC date cutoff.</summary>
    public DateTimeOffset? From { get; set; }
    /// <summary>Gets or sets the exclusive UTC date cutoff.</summary>
    public DateTimeOffset? To { get; set; }
}

/// <summary>Contains a bounded page without leaking persistence entities.</summary>
/// <typeparam name="T">Application response type.</typeparam>
/// <param name="Items">Matching page records.</param>
/// <param name="Page">One-based page.</param>
/// <param name="PageSize">Applied page size.</param>
/// <param name="TotalCount">Total matching rows.</param>
public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Gets the number of matching pages.</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>Groups monetary totals by their authoritative currency.</summary>
/// <param name="Currency">ISO currency.</param>
/// <param name="Amount">Server-computed total.</param>
public sealed record CurrencyTotalResponse(string Currency, decimal Amount);

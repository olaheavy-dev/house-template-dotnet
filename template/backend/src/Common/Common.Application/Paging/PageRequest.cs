using FluentValidation;

namespace Common.Application.Paging;

/// <summary>
/// Which page of a list a caller wants: up to <see cref="Limit"/> items after the page whose
/// <see cref="Cursor"/> they were given (none for the first page).
/// </summary>
public sealed record PageRequest(int Limit = PageRequest.DefaultLimit, string? Cursor = null)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int MaxCursorLength = 512;
}

/// <summary>Add it to a list query's validator: <c>RuleFor(query => query.Page).SetValidator(new PageRequestValidator())</c>.</summary>
public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(page => page.Limit).InclusiveBetween(1, PageRequest.MaxLimit);
        RuleFor(page => page.Cursor).MaximumLength(PageRequest.MaxCursorLength);
    }
}

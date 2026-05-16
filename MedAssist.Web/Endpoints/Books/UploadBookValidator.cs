using FastEndpoints;
using FluentValidation;
using MedAssist.Shared.Constants;

namespace MedAssist.Web.Endpoints.Books;

public sealed class UploadBookValidator : Validator<UploadBookRequest>
{
    public UploadBookValidator()
    {
        RuleFor(r => r.File)
            .NotNull()
            .Must(f => f is { Length: > 0 })
            .WithMessage("A PDF file is required.");

        RuleFor(r => r.BookId)
            .NotEmpty()
            .WithMessage("BookId is required.");

        RuleFor(r => r.Title)
            .NotEmpty()
            .WithMessage("Title is required.");

        RuleFor(r => r.Author)
            .NotEmpty()
            .WithMessage("Author is required.");

        RuleFor(r => r.Language)
            .Must(l => l == LanguageCodes.English || l == LanguageCodes.Bulgarian)
            .WithMessage($"Language must be '{LanguageCodes.English}' or '{LanguageCodes.Bulgarian}'.");
    }
}

using DocumentProcessor.Application.Common.Files;
using FluentValidation;

namespace DocumentProcessor.Application.Documents.Commands.ProcessDocument;

public sealed class ProcessDocumentCommandValidator : AbstractValidator<ProcessDocumentCommand>
{
    public ProcessDocumentCommandValidator()
    {
        RuleFor(command => command.FileName)
            .NotEmpty()
            .MaximumLength(260);

        RuleFor(command => command.MediaType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(SupportedDocumentMediaTypes.Contains)
            .WithMessage("Supported types: PDF, JPEG, PNG and WebP.");

        RuleFor(command => command.Content)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(content => content.Length > 0)
            .WithMessage("The document is empty.")
            .Must(content => content.Length <= SupportedDocumentMediaTypes.MaxFileSizeBytes)
            .WithMessage("Maximum document size is 20 MB.");

        RuleFor(command => command)
            .Must(command =>
                command.Content is not null &&
                command.Content.Length > 0 &&
                SupportedDocumentMediaTypes.Contains(command.MediaType) &&
                DocumentFileSignature.Matches(command.MediaType, command.Content))
            .WithMessage("The file signature does not match the declared media type.");
    }
}

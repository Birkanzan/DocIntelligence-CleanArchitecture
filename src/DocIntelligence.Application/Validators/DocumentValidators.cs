using FluentValidation;
using DocIntelligence.Application.UseCases.Documents;

namespace DocIntelligence.Application.Validators;

/// <summary>
/// Yükleme komutunun iş kurallarını doğrular.
/// </summary>
public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private static readonly string[] AllowedMimeTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/tiff",
        "image/bmp"
    ];

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("Dosya zorunludur.");

        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"Dosya boyutu en fazla {MaxFileSizeBytes / (1024 * 1024)} MB olabilir.");

        RuleFor(x => x.File.ContentType)
            .Must(ct => AllowedMimeTypes.Contains(ct))
            .WithMessage($"Desteklenen formatlar: {string.Join(", ", AllowedMimeTypes)}");

        RuleFor(x => x.File.FileName)
            .NotEmpty()
            .WithMessage("Dosya adı boş olamaz.")
            .MaximumLength(255)
            .WithMessage("Dosya adı en fazla 255 karakter olabilir.");
    }
}

using MediatR;
using Microsoft.AspNetCore.Mvc;
using DocIntelligence.Application.DTOs;
using DocIntelligence.Application.UseCases.Documents;
using DocIntelligence.Domain.Enums;

namespace DocIntelligence.WebAPI.Controllers;

/// <summary>
/// Belge yükleme, listeleme ve silme işlemlerini yönetir.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IMediator mediator, ILogger<DocumentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Yeni bir belge yükler. Dosya asenkron olarak işlenir.
    /// </summary>
    /// <response code="202">Belge alındı, arka planda işleniyor.</response>
    /// <response code="400">Geçersiz dosya formatı veya boyutu.</response>
    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Belge yükleme isteği: {FileName} ({Size} bytes)",
            file?.FileName, file?.Length);

        // UserId'yi JWT claim'inden al (şimdilik null)
        var userId = User.Identity?.Name;

        var command = new UploadDocumentCommand(file!, userId);
        var result = await _mediator.Send(command, cancellationToken);

        return Accepted(result);
    }

    /// <summary>
    /// Tüm belgeleri listeler. Filtreleme ve sayfalama desteklenir.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DocumentSearchFilter(
            UserId: null,
            Status: Enum.TryParse<DocumentStatus>(status, out var s) ? s : null,
            Category: Enum.TryParse<DocumentCategory>(category, out var c) ? c : null,
            TextQuery: search,
            Page: page,
            PageSize: pageSize
        );

        var result = await _mediator.Send(new GetAllDocumentsQuery(filter), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Belirli bir belgeyi ID ile getirir.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDocumentByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Belgeyi ve ilişkili dosyaları siler.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteDocumentCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
    /// <summary>
    /// Belgeyi indirir.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        Guid id,
        [FromQuery] bool optimized = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DownloadDocumentQuery(id, optimized), cancellationToken);
        if (result == null) return NotFound();

        return File(result.Stream, result.ContentType, result.FileName);
    }
}

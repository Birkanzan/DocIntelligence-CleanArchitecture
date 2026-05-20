using AutoMapper;
using MediatR;
using DocIntelligence.Application.DTOs;
using DocIntelligence.Domain.Entities;
using DocIntelligence.Domain.Enums;
using DocIntelligence.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DocIntelligence.Application.UseCases.Documents;

// =====================================================================
// COMMAND: Belge Yükleme
// =====================================================================

/// <summary>
/// Belge yükleme komutu. IFormFile ASP.NET Core'a bağımlı,
/// ancak Application katmanında Microsoft.AspNetCore.Http.Features
/// paketi üzerinden kullanılabilir.
/// </summary>
public record UploadDocumentCommand(
    IFormFile File,
    string? UserId
) : IRequest<DocumentDto>;

public class UploadDocumentCommandHandler
    : IRequestHandler<UploadDocumentCommand, DocumentDto>
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private readonly IMessageBroker _broker;
    private readonly IMapper _mapper;

    private static readonly Dictionary<string, FileType> _mimeToFileType = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/pdf",  FileType.Pdf  },
        { "image/jpeg",       FileType.Jpeg },
        { "image/png",        FileType.Png  },
        { "image/tiff",       FileType.Tiff },
        { "image/bmp",        FileType.Bmp  },
    };

    public UploadDocumentCommandHandler(
        IDocumentRepository repository,
        IStorageService storage,
        IMessageBroker broker,
        IMapper mapper)
    {
        _repository = repository;
        _storage = storage;
        _broker = broker;
        _mapper = mapper;
    }

    public async Task<DocumentDto> Handle(
        UploadDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var file = request.File;

        // 1. MIME → FileType
        var fileType = _mimeToFileType.TryGetValue(file.ContentType, out var ft)
            ? ft
            : FileType.Unknown;

        // 2. Dosyayı depola
        await using var stream = file.OpenReadStream();
        var storagePath = await _storage.SaveAsync(
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        // 3. Entity oluştur ve veritabanına kaydet
        var document = new Document(
            originalFileName: file.FileName,
            storagePath: storagePath,
            fileSizeBytes: file.Length,
            fileType: fileType,
            contentType: file.ContentType,
            uploadedByUserId: request.UserId
        );

        await _repository.AddAsync(document, cancellationToken);

        // 4. Mesaj kuyruğuna "işle" mesajı at (API kullanıcıyı bekletmez!)
        var message = new DocumentUploadedMessage(
            DocumentId: document.Id,
            StoragePath: storagePath,
            ContentType: file.ContentType,
            FileType: fileType.ToString()
        );

        await _broker.PublishAsync("document.processing", message, cancellationToken);

        return _mapper.Map<DocumentDto>(document);
    }
}

// =====================================================================
// QUERY: Tüm Belgeleri Getir
// =====================================================================

public record GetAllDocumentsQuery(DocumentSearchFilter Filter) : IRequest<PagedResult<DocumentDto>>;

public class GetAllDocumentsQueryHandler
    : IRequestHandler<GetAllDocumentsQuery, PagedResult<DocumentDto>>
{
    private readonly IDocumentRepository _repository;
    private readonly IMapper _mapper;

    public GetAllDocumentsQueryHandler(IDocumentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<DocumentDto>> Handle(
        GetAllDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var filter = request.Filter;

        IEnumerable<Document> documents = filter.UserId is not null
            ? await _repository.GetByUserIdAsync(filter.UserId, cancellationToken)
            : await _repository.GetAllAsync(cancellationToken);

        // Bellek içi filtreleme (production'da Expression<Func<T>> ile DB'de yapılmalı)
        if (filter.Status.HasValue)
            documents = documents.Where(d => d.Status == filter.Status.Value);

        if (filter.Category.HasValue)
            documents = documents.Where(d => d.Category == filter.Category.Value);

        if (!string.IsNullOrWhiteSpace(filter.TextQuery))
            documents = documents.Where(d =>
                d.OriginalFileName.Contains(filter.TextQuery, StringComparison.OrdinalIgnoreCase) ||
                (d.ExtractedText != null && d.ExtractedText.Contains(filter.TextQuery, StringComparison.OrdinalIgnoreCase)));

        var allDocs = documents.ToList();
        var totalCount = allDocs.Count;
        var paged = allDocs
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResult<DocumentDto>(
            Items: _mapper.Map<IEnumerable<DocumentDto>>(paged),
            TotalCount: totalCount,
            Page: filter.Page,
            PageSize: filter.PageSize
        );
    }
}

// =====================================================================
// QUERY: Tek Belge Getir
// =====================================================================

public record GetDocumentByIdQuery(Guid Id) : IRequest<DocumentDto?>;

public class GetDocumentByIdQueryHandler
    : IRequestHandler<GetDocumentByIdQuery, DocumentDto?>
{
    private readonly IDocumentRepository _repository;
    private readonly IMapper _mapper;

    public GetDocumentByIdQueryHandler(IDocumentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<DocumentDto?> Handle(
        GetDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return document is null ? null : _mapper.Map<DocumentDto>(document);
    }
}

// =====================================================================
// COMMAND: Belge Sil
// =====================================================================

public record DeleteDocumentCommand(Guid Id) : IRequest<bool>;

public class DeleteDocumentCommandHandler
    : IRequestHandler<DeleteDocumentCommand, bool>
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;

    public DeleteDocumentCommandHandler(IDocumentRepository repository, IStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<bool> Handle(
        DeleteDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (document is null) return false;

        // Depodan dosyaları sil
        if (await _storage.ExistsAsync(document.StoragePath, cancellationToken))
            await _storage.DeleteAsync(document.StoragePath, cancellationToken);

        if (document.OptimizedStoragePath is not null &&
            await _storage.ExistsAsync(document.OptimizedStoragePath, cancellationToken))
            await _storage.DeleteAsync(document.OptimizedStoragePath, cancellationToken);

        await _repository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}
// =====================================================================
// QUERY: Belge İndir
// =====================================================================

public record DownloadDocumentQuery(Guid Id, bool Optimized = false) : IRequest<FileDownloadResult?>;

public record FileDownloadResult(Stream Stream, string ContentType, string FileName);

public class DownloadDocumentQueryHandler
    : IRequestHandler<DownloadDocumentQuery, FileDownloadResult?>
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;

    public DownloadDocumentQueryHandler(IDocumentRepository repository, IStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<FileDownloadResult?> Handle(
        DownloadDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (document is null) return null;

        var path = request.Optimized && !string.IsNullOrEmpty(document.OptimizedStoragePath)
            ? document.OptimizedStoragePath
            : document.StoragePath;

        if (!await _storage.ExistsAsync(path, cancellationToken))
            return null;

        var stream = await _storage.GetAsync(path, cancellationToken);
        var fileName = request.Optimized ? $"optimized_{document.OriginalFileName}" : document.OriginalFileName;
        
        // PDF ise content type koru, değilse storage'dan gelen stream'i dön
        return new FileDownloadResult(stream, document.ContentType, fileName);
    }
}

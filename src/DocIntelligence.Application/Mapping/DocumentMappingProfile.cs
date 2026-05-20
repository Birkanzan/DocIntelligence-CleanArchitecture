using AutoMapper;
using DocIntelligence.Application.DTOs;
using DocIntelligence.Domain.Entities;

namespace DocIntelligence.Application.Mapping;

/// <summary>
/// AutoMapper profili: Domain Entity → DTO dönüşümleri burada tanımlanır.
/// </summary>
public class DocumentMappingProfile : Profile
{
    public DocumentMappingProfile()
    {
        CreateMap<Document, DocumentDto>()
            .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => src.FileType.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category.ToString()))
            .ForMember(dest => dest.UploadedAt, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.ClassificationConfidence, opt => opt.MapFrom(src => src.CategoryConfidence));
    }
}

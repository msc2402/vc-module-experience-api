using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.ExperienceApiModule.Core.Extensions;
using VirtoCommerce.ExperienceApiModule.Core.Infrastructure;
using VirtoCommerce.ExperienceApiModule.DigitalCatalog.Index;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.XDigitalCatalog.Queries
{
    public class LoadProductsQueryHandler : IQueryHandler<LoadProductQuery, LoadProductResponse>
    {
        private readonly IMapper _mapper;
        private readonly ISearchProvider _searchProvider;

        public LoadProductsQueryHandler(ISearchProvider searchProvider, IMapper mapper)
        {
            _searchProvider = searchProvider;
            _mapper = mapper;
        }

        public virtual async Task<LoadProductResponse> Handle(LoadProductQuery request, CancellationToken cancellationToken)
        {
            var result = new LoadProductResponse();
            var requestFields = new List<string>();

            var searchRequest = new SearchRequestBuilder()
                .WithPaging(0, request.Ids.Count())
                .WithIncludeFields(request.IncludeFields.Concat(new[] { "id" }).Select(x => "__object." + x).ToArray())
                .WithIncludeFields(request.IncludeFields.Where(x => x.StartsWith("prices.")).Concat(new[] { "id" }).Select(x => "__prices." + x.TrimStart("prices.")).ToArray())
                .WithIncludeFields(request.IncludeFields.Any(x => x.StartsWith("variations."))
                    ? new[] { "__variations" }
                    : Array.Empty<string>())
                .WithIncludeFields(request.IncludeFields.Any(x => x.StartsWith("category."))
                    ? new[] { "__object.categoryId" }
                    : Array.Empty<string>())
                // Add master variation fields
                .WithIncludeFields(request.IncludeFields
                    .Where(x => x.StartsWith("masterVariation."))
                    .Concat(new[] { "mainProductId" })
                    .Select(x => "__object." + x.TrimStart("masterVariation."))
                    .ToArray())
                // Add seoInfos
                .WithIncludeFields(request.IncludeFields.Any(x => x.Contains("slug", StringComparison.OrdinalIgnoreCase)
                                                                || x.Contains("meta", StringComparison.OrdinalIgnoreCase)) // for metaKeywords, metaTitle and metaDescription
                    ? new[] { "__object.seoInfos" }
                    : Array.Empty<string>())
                .WithIncludeFields(request.IncludeFields.Any(x => x.Contains("imgSrc", StringComparison.OrdinalIgnoreCase))
                    ? new[] { "__object.images" }
                    : Array.Empty<string>())
                .WithIncludeFields(request.IncludeFields.Any(x => x.Contains("brandName", StringComparison.OrdinalIgnoreCase))
                    ? new[] { "__object.properties" }
                    : Array.Empty<string>())
                .WithIncludeFields(request.IncludeFields.Any(x => x.Contains("descriptions", StringComparison.OrdinalIgnoreCase))
                    ? new[] { "__object.reviews" }
                    : Array.Empty<string>())
                .WithIncludeFields(request.IncludeFields.Any(x => x.Contains("availabilityData", StringComparison.OrdinalIgnoreCase))
                    ? new[] { "__object.isActive", "__object.isBuyable", "__object.trackInventory" }
                    : Array.Empty<string>())
                .AddObjectIds(request.Ids)
                .Build();

            var searchResult = await _searchProvider.SearchAsync(KnownDocumentTypes.Product, searchRequest);
            result.Products = searchResult.Documents.Select(x => _mapper.Map<ExpProduct>(x)).ToList();

            return result;
        }
    }
}

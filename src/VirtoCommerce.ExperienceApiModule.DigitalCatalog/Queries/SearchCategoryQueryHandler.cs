using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.ExperienceApiModule.Core.Infrastructure;
using VirtoCommerce.ExperienceApiModule.XDigitalCatalog.Index;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.XDigitalCatalog.Queries
{
    public class SearchCategoryQueryHandler :
        IQueryHandler<SearchCategoryQuery, SearchCategoryResponse>
        , IQueryHandler<LoadCategoryQuery, LoadCategoryResponse>
    {
        private readonly IMapper _mapper;
        private readonly ISearchProvider _searchProvider;
        private readonly ISearchPhraseParser _phraseParser;
        private readonly IStoreService _storeService;

        public SearchCategoryQueryHandler(
            ISearchProvider searchProvider
            , IMapper mapper
            , ISearchPhraseParser phraseParser
            , IStoreService storeService)
        {
            _searchProvider = searchProvider;
            _mapper = mapper;
            _phraseParser = phraseParser;
            _storeService = storeService;
        }

        public virtual async Task<SearchCategoryResponse> Handle(SearchCategoryQuery request, CancellationToken cancellationToken)
        {
            var store = await _storeService.GetByIdAsync(request.StoreId);

            var searchRequest = new IndexSearchRequestBuilder()
                                          .WithFuzzy(request.Fuzzy, request.FuzzyLevel)
                                          .ParseFilters(_phraseParser, request.Filter)
                                          .WithSearchPhrase(request.Query)
                                          .WithPaging(request.Skip, request.Take)
                                          .AddObjectIds(request.ObjectIds)
                                          .AddSorting(request.Sort)
                                          //Limit search result with store catalog
                                          .AddTerms(new[] { $"__outline:{store.Catalog}" })
                                          .WithIncludeFields(IndexFieldsMapper.MapToIndexIncludes(request.IncludeFields).ToArray())
                                          .Build();        

            var searchResult = await _searchProvider.SearchAsync(KnownDocumentTypes.Category, searchRequest);

            return new SearchCategoryResponse
            {
                Results = searchResult.Documents?.Select(x => _mapper.Map<ExpCategory>(x, options =>
                {
                    options.Items["store"] = store;
                    options.Items["language"] = request.CultureName;
                })).ToList(),
                TotalCount = (int)searchResult.TotalCount
            };
        }

        public virtual async Task<LoadCategoryResponse> Handle(LoadCategoryQuery request, CancellationToken cancellationToken)
        {
            var result = new LoadCategoryResponse();
            var searchRequest = _mapper.Map<SearchCategoryQuery>(request);

            result.Category = (await Handle(searchRequest, cancellationToken)).Results.FirstOrDefault();

            return result;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.ExperienceApiModule.Core.Helpers;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.XPurchase;
using VirtoCommerce.XPurchase.Validators;

namespace VirtoCommerce.ExperienceApiModule.XOrder.Commands
{
    public class CreateOrderFromCartCommandHandler : IRequestHandler<CreateOrderFromCartCommand, CustomerOrderAggregate>
    {
        private readonly IShoppingCartService _cartService;
        private readonly ICustomerOrderAggregateRepository _customerOrderAggregateRepository;
        private readonly ICartAggregateRepository _cartRepository;
        private readonly ICartValidationContextFactory _cartValidationContextFactory;

        public CreateOrderFromCartCommandHandler(
            IShoppingCartService cartService,
            ICustomerOrderAggregateRepository customerOrderAggregateRepository,
            ICartAggregateRepository cartRepository,
            ICartValidationContextFactory cartValidationContextFactory)
        {
            _cartService = cartService;
            _customerOrderAggregateRepository = customerOrderAggregateRepository;
            _cartRepository = cartRepository;
            _cartValidationContextFactory = cartValidationContextFactory;
        }

        public virtual async Task<CustomerOrderAggregate> Handle(CreateOrderFromCartCommand request, CancellationToken cancellationToken)
        {
            var cart = await _cartService.GetByIdAsync(request.CartId);

            await ValidateCart(cart);

            var result = await _customerOrderAggregateRepository.CreateOrderFromCart(cart);
            await _cartService.DeleteAsync(new List<string> { request.CartId }, softDelete: true);
            // Remark: There is potential issue, because there is no transaction thru two actions above. If a cart deletion fails, the order remains. That causes data inconsistency.
            // Unfortunately, current architecture does not allow us to support such scenarios in a transactional manner.
            return result;
        }

        protected virtual async Task ValidateCart(ShoppingCart cart)
        {
            var cartAggregate = await _cartRepository.GetCartForShoppingCartAsync(cart);
            var context = await _cartValidationContextFactory.CreateValidationContextAsync(cartAggregate);

            await cartAggregate.ValidateAsync(context, "*");

            var errors = cartAggregate.ValidationErrors;
            if (errors.Any())
            {
                var dictionary = errors.GroupBy(x => x.ErrorCode).ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage).FirstOrDefault());
                throw new ExecutionError("The cart has validation errors", dictionary) { Code = Constants.ValidationErrorCode };
            }
        }
    }
}

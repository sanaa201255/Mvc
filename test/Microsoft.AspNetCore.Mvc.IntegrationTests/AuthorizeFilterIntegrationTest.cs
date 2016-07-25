using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.IntegrationTests
{
    public class AuthorizeFilterIntegrationTest
    {
        [Fact]
        public async Task AuthorizeFilter_CalledTwiceWithNonDefaultProvider()
        {
            var applicationModelProviderContext = new ApplicationModelProviderContext(
                new[] { typeof(AuthorizeController).GetTypeInfo() });

            var policyProvider = new TestAuthorizationPolicyProvider();
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());

            defaultProvider.OnProvidersExecuting(applicationModelProviderContext);

            var controller = Assert.Single(applicationModelProviderContext.Result.Controllers);
            var action = Assert.Single(controller.Actions);
            var authorizeData = action.Attributes.OfType<AuthorizeAttribute>();
            var authorizeFilter = new AuthorizeFilter(policyProvider, authorizeData);

            var testContext = ModelBindingTestHelper.GetTestContext();
            var actionContext = new ActionContext(testContext.HttpContext, testContext.RouteData, testContext.ActionDescriptor);

            var authorizationFilterContext = new AuthorizationFilterContext(actionContext, action.Filters);
            await authorizeFilter.OnAuthorizationAsync(authorizationFilterContext);
            await authorizeFilter.OnAuthorizationAsync(authorizationFilterContext);

            Assert.Equal(2, policyProvider.GetPolicyCount);
        }

        public class TestAuthorizationPolicyProvider : IAuthorizationPolicyProvider
        {
            public int GetPolicyCount = 0;

            public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
            {
                throw new NotImplementedException();
            }

            public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
            {
                GetPolicyCount++;

                var requirements = new IAuthorizationRequirement[] {
                    new AssertionRequirement((con) => { return true; })
                };
                return Task.FromResult(new AuthorizationPolicy(requirements, new string[] { }));
            }
        }

        public class AuthorizeController
        {
            [Authorize(Policy = "Base")]
            public virtual void Authorize()
            { }
        }
    }
}

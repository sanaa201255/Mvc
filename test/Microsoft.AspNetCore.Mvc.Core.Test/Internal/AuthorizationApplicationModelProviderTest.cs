// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class AuthorizationApplicationModelProviderTest
    {
        [Fact]
        public void CreateControllerModel_AuthorizeAttributeAddsAuthorizeFilter()
        {
            // Arrange
            var provider = new AuthorizationApplicationModelProvider(new DefaultAuthorizationPolicyProvider(new TestOptionsManager<AuthorizationOptions>()));
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());

            var context = new ApplicationModelProviderContext(new[] { typeof(AccountController).GetTypeInfo() });
            defaultProvider.OnProvidersExecuting(context);

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            var controller = Assert.Single(context.Result.Controllers);
            Assert.Single(controller.Filters, f => f is AuthorizeFilter);
        }

        [Fact]
        public void BuildActionModels_BaseAuthorizeFiltersAreStillValidWhenOverriden()
        {
            // Arrange
            var options = new TestOptionsManager<AuthorizationOptions>();
            options.Value.AddPolicy("Base", policy => policy.RequireClaim("Basic").RequireClaim("Basic2"));
            options.Value.AddPolicy("Derived", policy => policy.RequireClaim("Derived"));

            var provider = new AuthorizationApplicationModelProvider(new DefaultAuthorizationPolicyProvider(options));
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());

            var context = new ApplicationModelProviderContext(new[] { typeof(DerivedController).GetTypeInfo() });
            defaultProvider.OnProvidersExecuting(context);

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            var controller = Assert.Single(context.Result.Controllers);
            var action = Assert.Single(controller.Actions);
            Assert.Equal("Authorize", action.ActionName);

            var attributeRoutes = action.Selectors.Where(sm => sm.AttributeRouteModel != null);
            Assert.Empty(attributeRoutes);
            var authorizeFilters = action.Filters.OfType<AuthorizeFilter>();
            Assert.Single(authorizeFilters);

            Assert.NotNull(authorizeFilters.First().Policy);
            Assert.Equal(3, authorizeFilters.First().Policy.Requirements.Count()); // Basic + Basic2 + Derived authorize
        }

        [Fact]
        public void CreateControllerModelAndActionModel_AllowAnonymousAttributeAddsAllowAnonymousFilter()
        {
            // Arrange
            var provider = new AuthorizationApplicationModelProvider(new DefaultAuthorizationPolicyProvider(new TestOptionsManager<AuthorizationOptions>()));
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());

            var context = new ApplicationModelProviderContext(new[] { typeof(AnonymousController).GetTypeInfo() });
            defaultProvider.OnProvidersExecuting(context);

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            var controller = Assert.Single(context.Result.Controllers);
            Assert.Single(controller.Filters, f => f is AllowAnonymousFilter);
            var action = Assert.Single(controller.Actions);
            Assert.Single(action.Filters, f => f is AllowAnonymousFilter);
        }

        [Fact]
        public void OnProvidersExecuting_DefaultPolicyProvider_NoAuthorizationData_NoFilterCreated()
        {
            // Arrange
            var requirements = new IAuthorizationRequirement[] {
                new AssertionRequirement((con) => { return true; })
            };
            var authorizationPolicy = new AuthorizationPolicy(requirements, new string[] { "dingos" });
            var authOptions = new TestOptionsManager<AuthorizationOptions>();
            authOptions.Value.AddPolicy("Base", authorizationPolicy);
            var policyProvider = new DefaultAuthorizationPolicyProvider(authOptions);

            var provider = new AuthorizationApplicationModelProvider(policyProvider);
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());

            // Act
            var action = GetBaseControllerActionModel(provider, defaultProvider);

            // Assert
            var authorizationFilter = Assert.IsType<AuthorizeFilter>(Assert.Single(action.Filters));
            Assert.NotNull(authorizationFilter.Policy);
            Assert.Null(authorizationFilter.AuthorizeData);
            Assert.Null(authorizationFilter.PolicyProvider);
        }

        [Fact]
        public async void OnProvidersExecuting_NonDefaultPolicyProvider_HasNoPolicy_HasPolicyProviderAndAuthorizeData()
        {
            // Arrange
            var requirements = new IAuthorizationRequirement[] {
                new AssertionRequirement((con) => { return true; })
            };
            var authorizationPolicy = new AuthorizationPolicy(requirements, new string[] { "dingos" });
            var authorizationPolicyProviderMock = new Mock<IAuthorizationPolicyProvider>();
            authorizationPolicyProviderMock
                .Setup(s => s.GetPolicyAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(authorizationPolicy))
                .Verifiable();

            var provider = new AuthorizationApplicationModelProvider(authorizationPolicyProviderMock.Object);
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());
            var action = GetBaseControllerActionModel(provider, defaultProvider);
            var actionFilter = Assert.IsType<AuthorizeFilter>(Assert.Single(action.Filters));

            var httpContext = GetHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());

            var authorizationFilterContext = new AuthorizationFilterContext(actionContext, action.Filters);

            // Act
            await actionFilter.OnAuthorizationAsync(authorizationFilterContext);
            await actionFilter.OnAuthorizationAsync(authorizationFilterContext);

            // Assert
            authorizationPolicyProviderMock.Verify(s => s.GetPolicyAsync("POLICY"), Times.Exactly(2));

            Assert.Null(actionFilter.Policy);
            Assert.NotNull(actionFilter.AuthorizeData);
            Assert.NotNull(actionFilter.PolicyProvider);
        }

        [Fact]
        public void CreateControllerModelAndActionModel_NoAuthNoFilter()
        {
            // Arrange
            var provider = new AuthorizationApplicationModelProvider(
                new DefaultAuthorizationPolicyProvider(
                    new TestOptionsManager<AuthorizationOptions>()
                ));
            var defaultProvider = new DefaultApplicationModelProvider(new TestOptionsManager<MvcOptions>());

            var context = new ApplicationModelProviderContext(new[] { typeof(NoAuthController).GetTypeInfo() });
            defaultProvider.OnProvidersExecuting(context);

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            var controller = Assert.Single(context.Result.Controllers);
            Assert.Empty(controller.Filters);
            var action = Assert.Single(controller.Actions);
            Assert.Empty(action.Filters);
        }

        private ActionModel GetBaseControllerActionModel(
            IApplicationModelProvider authorizationApplicationModelProvider,
            IApplicationModelProvider applicationModelProvider)
        {
            var context = new ApplicationModelProviderContext(new[] { typeof(BaseController).GetTypeInfo() });
            applicationModelProvider.OnProvidersExecuting(context);
            var authorizeData = new List<IAuthorizeData> {
                new AuthorizeAttribute("POLICY")
            };

            authorizationApplicationModelProvider.OnProvidersExecuting(context);

            var controller = Assert.Single(context.Result.Controllers);
            Assert.Empty(controller.Filters);
            var action = Assert.Single(controller.Actions);

            return action;
        }

        private static IServiceProvider GetServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddAuthorization();
            serviceCollection.AddMvc();
            serviceCollection
                .AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>()
                .AddTransient<ILoggerFactory, LoggerFactory>()
                .AddTransient<ILogger<DefaultAuthorizationService>, Logger<DefaultAuthorizationService>>();

            return serviceCollection.BuildServiceProvider();
        }

        private static HttpContext GetHttpContext()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.RequestServices = GetServices();
            return httpContext;
        }


        private class ChangingAuthorizationPolicyProvider : IAuthorizationPolicyProvider
        {
            public int CallCount = 0;

            public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
            {
                throw new NotImplementedException();
            }

            public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
            {
                CallCount++;

                var authorizationPolicyBuilder = new AuthorizationPolicyBuilder();
                for (var i = 0; i < CallCount; i++)
                {
                    authorizationPolicyBuilder.AddRequirements(new NameAuthorizationRequirement("require" + i));
                }

                return Task.FromResult(authorizationPolicyBuilder.Build());
            }
        }

        private class BaseController
        {
            [Authorize(Policy = "Base")]
            public virtual void Authorize()
            {
            }
        }

        private class DerivedController : BaseController
        {
            [Authorize(Policy = "Derived")]
            public override void Authorize()
            {
            }
        }

        [Authorize]
        public class AccountController
        {
        }

        public class NoAuthController
        {
            public void NoAuthAction()
            { }
        }

        [AllowAnonymous]
        public class AnonymousController
        {
            [AllowAnonymous]
            public void SomeAction()
            {
            }
        }
    }
}
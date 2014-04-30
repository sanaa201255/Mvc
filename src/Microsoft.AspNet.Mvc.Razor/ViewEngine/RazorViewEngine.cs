// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING
// WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF
// TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR
// NON-INFRINGEMENT.
// See the Apache 2 License for the specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNet.Mvc.Rendering;

namespace Microsoft.AspNet.Mvc.Razor
{
    public class RazorViewEngine : IViewEngine
    {
        private static readonly string _viewExtension = ".cshtml";

        private static readonly string[] _viewLocationFormats =
        {
            "/Views/{1}/{0}" + _viewExtension,
            "/Views/Shared/{0}" + _viewExtension,
        };

        private static readonly string[] _areaViewLocationFormats =
        {
            "/Areas/{2}/Views/{1}/{0}" + _viewExtension,
            "/Areas/{2}/Views/Shared/{0}" + _viewExtension,
            "/Views/Shared/{0}" + _viewExtension,
        };

        private readonly IVirtualPathViewFactory _virtualPathFactory;

        public RazorViewEngine(IVirtualPathViewFactory virtualPathFactory)
        {
            _virtualPathFactory = virtualPathFactory;
        }

        public IEnumerable<string> ViewLocationFormats
        {
            get { return _viewLocationFormats; }
        }

        public ViewEngineResult FindView([NotNull] IDictionary<string, object> context,
                                         [NotNull] string viewName)
        {
            var viewEngineResult = CreateViewEngineResult(context, viewName);
            return viewEngineResult;
        }

        public ViewEngineResult FindPartialView([NotNull] IDictionary<string, object> context,
                                                [NotNull] string partialViewName)
        {
            return FindView(context, partialViewName);
        }

        private ViewEngineResult CreateViewEngineResult([NotNull] IDictionary<string, object> context,
                                                        [NotNull] string viewName)
        {
            var nameRepresentsPath = IsSpecificPath(viewName);

            if (nameRepresentsPath)
            {
                EnsureFullPathViewExtension(viewName);

                var view = _virtualPathFactory.CreateInstance(viewName);
                return view != null ? ViewEngineResult.Found(viewName, view) :
                                      ViewEngineResult.NotFound(viewName, new[] { viewName });
            }
            else
            {
                var controllerName = context.GetValueOrDefault<string>("controller");
                var areaName = context.GetValueOrDefault<string>("area");
                var potentialPaths = GetViewSearchPaths(viewName, controllerName, areaName);

                foreach (var path in potentialPaths)
                {
                    var view = _virtualPathFactory.CreateInstance(path);
                    if (view != null)
                    {
                        return ViewEngineResult.Found(viewName, view);
                    }
                }

                return ViewEngineResult.NotFound(viewName, potentialPaths);
            }
        }

        private static void EnsureFullPathViewExtension(string viewName)
        {
            if(!viewName.EndsWith(_viewExtension))
            {
                throw new InvalidOperationException(
                    Resources.FormatViewMustEndInExtension(viewName, _viewExtension));
            }
        }

        private static bool IsSpecificPath(string name)
        {
            char c = name[0];
            return name[0] == '~' || name[0] == '/';
        }

        private IEnumerable<string> GetViewSearchPaths(string viewName, string controllerName, string areaName)
        {
            IEnumerable<string> unformattedPaths;

            if (string.IsNullOrEmpty(areaName))
            {
                // If no areas then no need to search area locations.
                unformattedPaths = _viewLocationFormats;
            }
            else
            {
                // If there's an area provided only search area view locations
                unformattedPaths = _areaViewLocationFormats;
            }

            var formattedPaths = unformattedPaths.Select(path =>
                string.Format(CultureInfo.InvariantCulture, path, viewName, controllerName, areaName)
            );

            return formattedPaths;
        }
    }
}

﻿using System;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.Mvc.Razor.Precompilation
{
    public class PrecompilationApplication : CommandLineApplication
    {
        private readonly Type _callingType;

        public PrecompilationApplication(Type callingType)
        {
            _callingType = callingType;

            Name = "razor-tooling";
            FullName = "Microsoft Razor Tooling Utility";
            Description = "Resolves Razor tooling specific information.";
            ShortVersionGetter = GetInformationalVersion;

            HelpOption("-?|-h|--help");

            OnExecute(() =>
            {
                ShowHelp();
                return 2;
            });
        }

        public new int Execute(params string[] args)
        {
            try
            {
                return base.Execute(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private string GetInformationalVersion()
        {
            var assembly = _callingType.GetTypeInfo().Assembly;
            var attributes = assembly.GetCustomAttributes(
                typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute[];

            var versionAttribute = attributes.Length == 0 ?
                assembly.GetName().Version.ToString() :
                attributes[0].InformationalVersion;

            return versionAttribute;
        }
    }
}

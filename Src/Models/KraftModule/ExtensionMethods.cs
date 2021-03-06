﻿using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ccf.Ck.Utilities.Profiling;
using Ccf.Ck.Libs.Web.Bundling;
using Ccf.Ck.Libs.Web.Bundling.Transformations;
using Ccf.Ck.Utilities.Web.BundleTransformations;

namespace Ccf.Ck.Models.KraftModule
{
    public static class ExtensionMethods
    {
        static IApplicationBuilder _Builder;
        static ILogger _Logger;
        static string _ModulesRootFolder;
        public static void Init(IApplicationBuilder builder, ILogger logger, string modulesRootFolder)
        {
            _Builder = builder;
            _Logger = logger;
            _ModulesRootFolder = modulesRootFolder;
        }
        #region Render Scripts and Css
        //Called from the Razor-Views or master page
        public static Scripts KraftScripts(this Profile profile)
        {
            if (!profile.HasScriptBundle(profile.Key + "-scripts"))
            {
                KraftModuleCollection modulesCollection = _Builder.ApplicationServices.GetService<KraftModuleCollection>();
                ScriptBundle scriptBundle = new ScriptBundle(profile.Key + "-scripts", new PhysicalFileProvider(modulesCollection.KraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath));
                StringBuilder contentTemplates = new StringBuilder(10000);
                bool appendDiv = false;

                //try to get the target module
                KraftModule profileTargetModule = modulesCollection.GetModule(profile.Key);
                if (profileTargetModule == null) throw new Exception($"No CoreKraft module found for bundle target \"{profile.Key}\"!");


                void Dive(KraftModule kmodule, HashSet<KraftModule> deps)
                {
                    foreach (var dep in kmodule.Dependencies)
                    {
                        Dive(dep.Value as KraftModule, deps);
                    }
                    deps.Add(kmodule);
                }

                HashSet<KraftModule> targetDeps = new HashSet<KraftModule>();
                Dive(profileTargetModule, targetDeps);
                List<KraftModule> targetDepsSorted = targetDeps.OrderBy(x => x.DependencyOrderIndex).ToList<KraftModule>();

                foreach (KraftModule kraftDepModule in targetDepsSorted)
                {
                    if (kraftDepModule.ScriptKraftBundle != null)
                    {
                        using (KraftProfiler.Current.Step($"Time loading {kraftDepModule.Key}: "))
                        {
                            scriptBundle.Include(new KraftRequireTransformation().Process(kraftDepModule.ScriptKraftBundle, _Logger));
                        } 
                    }

                    if (kraftDepModule.TemplateKraftBundle != null && kraftDepModule.TemplateKraftBundle.TemplateFiles.Count > 0)
                    {
                        if (appendDiv)
                        {
                            contentTemplates.Append(",");
                        }
                        else
                        {
                            appendDiv = true;
                        }
                        HtmlTransformation htmlTransformation = new HtmlTransformation();
                        Func<StringBuilder, ILogger, StringBuilder> minifyHtml = htmlTransformation.Process;
                        contentTemplates.Append(new KraftHtml2JsAssocArrayTransformation().Process(kraftDepModule.TemplateKraftBundle, minifyHtml, _Logger));
                    }
                }

                scriptBundle.IncludeContent("Registers.addRegister(new TemplateRegister(\"module_templates\")); Registers.getRegister(\"module_templates\").$collection= {" + contentTemplates.Append("}"));

                profile.Add(scriptBundle);
                return profile.Scripts;
            }
            else
            {
                return profile.Scripts;
            }
        }

        //Called from the Razor-Views or master page
        public static Styles KraftStyles(this Profile profile)
        {
            if (!profile.HasStyleBundle(profile.Key + "-css"))
            {
                KraftModuleCollection modulesCollection = _Builder.ApplicationServices.GetService<KraftModuleCollection>();
                StyleBundle styleBundle = new StyleBundle(profile.Key + "-css", new PhysicalFileProvider(modulesCollection.KraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath), delegate () { return _ModulesRootFolder; });
                styleBundle.RemoveTransformationType(typeof(LessTransformation));
                StringBuilder contentTemplates = new StringBuilder(10000);

                //try to get the target module
                KraftModule profileTargetModule = modulesCollection.GetModule(profile.Key);
                if (profileTargetModule == null) throw new Exception($"No CoreKraft module found for bundle target \"{profile.Key}\"!");

                void Dive(KraftModule kmodule, HashSet<KraftModule> deps)
                {
                    foreach (var dep in kmodule.Dependencies)
                    {
                        Dive(dep.Value as KraftModule, deps);
                    }
                    deps.Add(kmodule);
                }

                HashSet<KraftModule> targetDeps = new HashSet<KraftModule>();
                Dive(profileTargetModule, targetDeps);
                List<KraftModule> targetDepsSorted = targetDeps.OrderBy(x => x.DependencyOrderIndex).ToList<KraftModule>();

                foreach (KraftModule kraftDepModule in targetDepsSorted)
                {
                    if (kraftDepModule.StyleKraftBundle != null)
                    {
                        styleBundle.Include(new KraftRequireTransformation().Process(kraftDepModule.StyleKraftBundle, _Logger));
                    }
                }

                profile.Add(styleBundle);
                return profile.Styles;
            }
            else
            {
                return profile.Styles;
            }
        }
        #endregion Render Scripts and Css
    }
}

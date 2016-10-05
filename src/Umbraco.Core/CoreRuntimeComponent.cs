﻿using System;
using System.Collections.Generic;
using System.IO;
using AutoMapper;
using LightInject;
using Umbraco.Core.Cache;
using Umbraco.Core.Components;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.DependencyInjection;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Manifest;
using Umbraco.Core.Models.Mapping;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Plugins;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Core.Strings;
using Umbraco.Core.Sync;
using Umbraco.Core._Legacy.PackageActions;
using IntegerValidator = Umbraco.Core.PropertyEditors.IntegerValidator;

namespace Umbraco.Core
{
    public class CoreRuntimeComponent : UmbracoComponentBase, IRuntimeComponent
    {
        public override void Compose(ServiceContainer container)
        {
            base.Compose(container);

            // register from roots
            container.RegisterFrom<ConfigurationCompositionRoot>();
            container.RegisterFrom<RepositoryCompositionRoot>();
            container.RegisterFrom<ServicesCompositionRoot>();
            container.RegisterFrom<CoreModelMappersCompositionRoot>();












            //TODO: Don't think we'll need this when the resolvers are all container resolvers
            container.RegisterSingleton<IServiceProvider, ActivatorServiceProvider>();

            container.Register<MediaFileSystem>(factory => FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>());

            // fixme - should we capture Logger, etc here or use factory?

            // register manifest builder, will be injected in eg PropertyEditorCollectionBuilder
            container.RegisterSingleton(factory
                => new ManifestParser(factory.GetInstance<ILogger>(), new DirectoryInfo(IOHelper.MapPath("~/App_Plugins")), factory.GetInstance<IRuntimeCacheProvider>()));
            container.RegisterSingleton<ManifestBuilder>();

            PropertyEditorCollectionBuilder.Register(container)
                .Add(factory => factory.GetInstance<PluginManager>().ResolvePropertyEditors());

            ParameterEditorCollectionBuilder.Register(container)
                .Add(factory => factory.GetInstance<PluginManager>().ResolveParameterEditors());

            // register our predefined validators
            ValidatorCollectionBuilder.Register(container)
                .Add<RequiredManifestValueValidator>()
                .Add<RegexValidator>()
                .Add<DelimitedManifestValueValidator>()
                .Add<EmailValidator>()
                .Add<IntegerValidator>()
                .Add<DecimalValidator>();

            // register a server registrar, by default it's the db registrar unless the dev
            // has the legacy dist calls enabled - fixme - should obsolete the legacy thing
            container.RegisterSingleton(factory => UmbracoConfig.For.UmbracoSettings().DistributedCall.Enabled
                ? (IServerRegistrar)new ConfigServerRegistrar(UmbracoConfig.For.UmbracoSettings())
                : (IServerRegistrar)new DatabaseServerRegistrar(
                    new Lazy<IServerRegistrationService>(factory.GetInstance<IServerRegistrationService>),
                    new DatabaseServerRegistrarOptions()));

            // by default we'll use the database server messenger with default options (no callbacks),
            // this will be overridden in the web startup
            // fixme - painful, have to take care of lifetime! - we CANNOT ask users to remember!
            // fixme - same issue with PublishedContentModelFactory and many more, I guess!
            container.RegisterSingleton<IServerMessenger>(factory
                => new DatabaseServerMessenger(
                    factory.GetInstance<IRuntimeState>(),
                    factory.GetInstance<DatabaseContext>(),
                    factory.GetInstance<ILogger>(),
                    factory.GetInstance<ProfilingLogger>(),
                    true, new DatabaseServerMessengerOptions()));

            CacheRefresherCollectionBuilder.Register(container)
                .Add(factory => factory.GetInstance<PluginManager>().ResolveCacheRefreshers());

            PackageActionCollectionBuilder.Register(container)
                .Add(f => f.GetInstance<PluginManager>().ResolvePackageActions());

            MigrationCollectionBuilder.Register(container)
                .Add(factory => factory.GetInstance<PluginManager>().ResolveTypes<IMigration>());

            // need to filter out the ones we dont want!! fixme - what does that mean?
            PropertyValueConverterCollectionBuilder.Register(container)
                .Append(factory => factory.GetInstance<PluginManager>().ResolveTypes<IPropertyValueConverter>());

            container.RegisterSingleton<IShortStringHelper>(factory
                => new DefaultShortStringHelper(new DefaultShortStringHelperConfig().WithDefault(factory.GetInstance<IUmbracoSettingsSection>())));

            UrlSegmentProviderCollectionBuilder.Register(container)
                .Append<DefaultUrlSegmentProvider>();

            // by default, register a noop factory
            container.RegisterSingleton<IPublishedContentModelFactory, NoopPublishedContentModelFactory>();
        }

        internal void Initialize(
            IEnumerable<ModelMapperConfiguration> modelMapperConfigurations)
        {
            //TODO: Remove these for v8!
            LegacyPropertyEditorIdToAliasConverter.CreateMappingsForCoreEditors();
            LegacyParameterEditorAliasConverter.CreateMappingsForCoreEditors();

            // model mapper configurations have been registered & are created by the container
            Mapper.Initialize(configuration =>
            {
                foreach (var m in modelMapperConfigurations)
                    m.ConfigureMappings(configuration);
            });

            // ensure we have some essential directories
            // every other component can then initialize safely
            IOHelper.EnsurePathExists("~/App_Data");
            IOHelper.EnsurePathExists(SystemDirectories.Media);
            IOHelper.EnsurePathExists(SystemDirectories.MvcViews);
            IOHelper.EnsurePathExists(SystemDirectories.MvcViews + "/Partials");
            IOHelper.EnsurePathExists(SystemDirectories.MvcViews + "/MacroPartials");
        }
    }
}
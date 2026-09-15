using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using ValheimServerGUI.Core.Processes;
using ValheimServerGUI.Game;
using ValheimServerGUI.Infrastructure.DependencyInjection;
using ValheimServerGUI.Tests.Tools;
using ValheimServerGUI.Tools.Data;
using ValheimServerGUI.Tools.Http;

namespace ValheimServerGUI.Tests
{
    public partial class BaseTest
    {
        protected IServiceCollection ServiceCollection { get; }

        protected IServiceProvider ServiceProvider { get; }

        protected MockDataFileProvider MockDataFileProvider { get; }

        protected MockHttpClientProvider MockHttpClientProvider { get; }

        protected MockServerProcessFactory MockProcessFactory { get; }

        protected MockUserPreferencesProvider MockUserPreferencesProvider { get; }

        public BaseTest()
        {
            ServiceCollection = new ServiceCollection();
            ServiceCollection.AddValheimServerServices(Array.Empty<string>());

            MockDataFileProvider = new();
            MockProcessFactory = new();
            MockHttpClientProvider = new();
            MockUserPreferencesProvider = new();

            ServiceCollection.Replace(ServiceDescriptor.Singleton<IFileProvider>(MockDataFileProvider));
            ServiceCollection.Replace(ServiceDescriptor.Singleton<IServerProcessFactory>(MockProcessFactory));
            ServiceCollection.Replace(ServiceDescriptor.Singleton<IHttpClientProvider>(MockHttpClientProvider));
            ServiceCollection.Replace(ServiceDescriptor.Singleton<IUserPreferencesProvider>(MockUserPreferencesProvider));

            ServiceProvider = ServiceCollection.BuildServiceProvider();
        }

        protected TService GetService<TService>()
        {
            return ServiceProvider.GetRequiredService<TService>();
        }
    }
}

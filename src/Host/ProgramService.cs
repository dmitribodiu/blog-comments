﻿namespace Host
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Common;
    using Components.GitHub;
    using Microsoft.Extensions.Configuration;
    using NServiceBus;

    public class ProgramService : ServiceBase
    {
        private IEndpointInstance endpointInstance;

        public static void Main()
        {
            using (var service = new ProgramService())
            {
                if (ServiceHelper.IsService())
                {
                    Run(service);
                    return;
                }

                Console.Title = "Host";
                service.OnStart(null);

                Console.WriteLine("Endpoint started. Press any key to exit");
                Console.ReadKey();

                service.OnStop();
            }
        }

        protected override void OnStart(string[] args)
        {
            this.AsyncOnStart().GetAwaiter().GetResult();
        }

        protected async Task AsyncOnStart()
        {
            IConfiguration config = new ConfigurationBuilder()
                            .SetBasePath(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location))
                            .AddJsonFile("appsettings.host.dev.json", true, true)
                            .AddJsonFile("appsettings.host.test.json", true, true)
                            .AddJsonFile("appsettings.host.production.json", true, true)
                            .Build();

            var configurationManager = new ConfigurationManager(config);
            var endpointInitializer = new EndpointInitializer(configurationManager);
            var endpointConfiguration = new EndpointConfiguration(configurationManager.NsbEndpointName);

            // initialize endpoint
            endpointInitializer.Initialize(endpointConfiguration, false);

            // register components
            if (configurationManager.IsUseFakes)
            {
                endpointConfiguration.RegisterComponents(reg =>
                    reg.ConfigureComponent<GitHubApiForTests>(DependencyLifecycle.InstancePerCall));
            }
            else
            {
                endpointConfiguration.RegisterComponents(reg =>
                    reg.ConfigureComponent<GitHubApi>(DependencyLifecycle.InstancePerCall));
            }

            endpointConfiguration.RegisterComponents(reg =>
                    reg.ConfigureComponent(() => configurationManager, DependencyLifecycle.InstancePerCall));

            // start endpoint
            this.endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);
        }

        protected override void OnStop()
        {
            this.endpointInstance?.Stop().GetAwaiter().GetResult();
        }
    }
}

﻿using System;
using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;
using NUnit.Framework.Interfaces;

using Orleans;
using Orleans.Hosting;
using Orleans.Configuration;
using Orleans.Storage;

using Orleankka.Testing;
using Orleankka.Cluster;
using Orleankka.Features.Intercepting_requests;

[assembly: TeardownSilo]

namespace Orleankka.Testing
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RequiresSiloAttribute : TestActionAttribute
    {
        const string DemoClusterId = "localhost-demo";
        const string DemoServiceId = "localhost-demo-service";

        const int LocalhostSiloPort = 11111;
        const int LocalhostGatewayPort = 30000;
        static readonly IPAddress LocalhostSiloAddress = IPAddress.Loopback;

        public override void BeforeTest(ITest test)
        {
            if (!test.IsSuite)
                return;

            if (TestActorSystem.Instance != null)
                return;

            var sb = new SiloHostBuilder()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = DemoClusterId;
                    options.ServiceId = DemoServiceId;
                })
                .EnableDirectClient()
                .UseDevelopmentClustering(options => options.PrimarySiloEndpoint = new IPEndPoint(LocalhostSiloAddress, LocalhostSiloPort))
                .ConfigureEndpoints(LocalhostSiloAddress, LocalhostSiloPort, LocalhostGatewayPort)
                .AddMemoryGrainStorageAsDefault()
                .AddMemoryGrainStorage("PubSubStore")
                .AddSimpleMessageStreamProvider("sms")
                .UseInMemoryReminderService()
                .ConfigureServices(services =>
                {
                    services.Configure<GrainCollectionOptions>(options => options.CollectionAge = TimeSpan.FromMinutes(1));
                })
                .ConfigureApplicationParts(x => x
                    .AddApplicationPart(GetType().Assembly)
                    .AddApplicationPart(typeof(MemoryGrainStorage).Assembly)
                    .WithCodeGeneration())
                .UseOrleankka(x => x
                    .ActorMiddleware(typeof(TestActorBase), new TestActorMiddleware())
                    .DirectClientActorRefMiddleware(new TestActorRefMiddleware()));

            var host = sb.Build();
            host.StartAsync().Wait();

            TestActorSystem.Host = host;
            TestActorSystem.Client = host.Services.GetRequiredService<IClusterClient>();
            TestActorSystem.Instance = host.ActorSystem();
        }
    }

    public class TeardownSiloAttribute : TestActionAttribute
    {
        public override void AfterTest(ITest test)
        {
            if (!test.IsSuite)
                return;

            if (TestActorSystem.Instance == null)
                return;

            TestActorSystem.Client.Close().Wait();
            TestActorSystem.Client.Dispose();
            TestActorSystem.Host.StopAsync().Wait();
            TestActorSystem.Host.Dispose();

            TestActorSystem.Client = null;
            TestActorSystem.Host = null;
            TestActorSystem.Instance = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit;

namespace DockerDotnetPlaygroundTests
{
    [CollectionDefinition("LocalStack", DisableParallelization = false)]
    public class LocalStackTestsCollection : ICollectionFixture<LocalStackTestCollectionFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class LocalStackTestCollectionFixture : IAsyncLifetime
    {
        internal LocalStackContainer? LocalStackContainer;

        /// <summary>
        ///     SetUp for tests collection. Runs once, before the first test in collection.
        /// </summary>
        public async Task InitializeAsync()
        {
            LocalStackContainer = await LocalStackContainer.StartAsync();
        }

        /// <summary>
        ///     TearDown for tests collection. Runs once, after the last test in collection.
        /// </summary>
        public async Task DisposeAsync()
        {
            if (LocalStackContainer != null)
            {
                await LocalStackContainer.DisposeAsync();
                LocalStackContainer = null;
            }
        }
    }

    internal class LocalStackContainer : IAsyncDisposable, IDisposable
    {
        private readonly string _containerId;
        private DockerClient? _dockerClient;

        public LocalStackContainer(DockerClient dockerClient, string containerId, string serviceUrl)
        {
            _dockerClient = dockerClient;
            _containerId = containerId;
            ServiceUrl = serviceUrl;
        }

        public AWSCredentials AwsCredentials => new BasicAWSCredentials("test", "test");

        public string ServiceUrl { get; }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public static async Task<LocalStackContainer> StartAsync(CancellationToken cancellationToken = default)
        {
            const string imageName = "localstack/localstack:0.13";
            const string hostName = "localhost";
            var containerName = $"local-stack-{Guid.NewGuid():N}";
            var environmentVariables = new List<string> {"SERVICES=sns,sqs"};
            var portMapping = (ContainerPort: "4566/tcp",
                LocalhostPort: GetFreeTcpPort().ToString(CultureInfo.InvariantCulture));

            var containerConfig = new CreateContainerParameters
            {
                Image = imageName,
                Hostname = hostName,
                Name = containerName,
                Env = environmentVariables,
                HostConfig = new HostConfig
                {
                    AutoRemove = true,
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            portMapping.ContainerPort,
                            new List<PortBinding>
                            {
                                new()
                                {
                                    HostIP = "0.0.0.0",
                                    HostPort = portMapping.LocalhostPort
                                }
                            }
                        }
                    }
                }
            };

            // @SEE: https://github.com/Microsoft/Docker.DotNet#usage
            var uri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "npipe://./pipe/docker_engine"
                : "unix:///var/run/docker.sock";

            var dockerClient = new DockerClientConfiguration(new Uri(uri)).CreateClient();

            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = imageName
                },
                new AuthConfig(),
                new Progress<JSONMessage>(),
                cancellationToken);

            var container = await dockerClient.Containers.CreateContainerAsync(
                containerConfig,
                cancellationToken);

            await dockerClient.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters(),
                cancellationToken);

            var serviceUrl = $"http://{hostName}:{portMapping.LocalhostPort}";
            return new LocalStackContainer(dockerClient, container.ID, serviceUrl);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dockerClient?.Containers.StopContainerAsync(_containerId, new ContainerStopParameters()).GetAwaiter()
                    .GetResult();
                _dockerClient?.Dispose();
                _dockerClient = null;
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_dockerClient != null)
            {
                await _dockerClient.Containers.StopContainerAsync(_containerId, new ContainerStopParameters());
                _dockerClient.Dispose();
            }

            _dockerClient = null;
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
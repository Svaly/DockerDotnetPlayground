using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Xunit;

namespace DockerDotnetPlaygroundTests
{
    [Collection("LocalStack")]
    public class UnitTest1
    {
        private readonly AmazonSimpleNotificationServiceClient _snsClient;

        public UnitTest1(LocalStackTestCollectionFixture localStackTestCollectionFixture)
        {
            _snsClient = new AmazonSimpleNotificationServiceClient(
                localStackTestCollectionFixture.LocalStackContainer?.AwsCredentials,
                new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = localStackTestCollectionFixture.LocalStackContainer?.ServiceUrl
                });
        }

        [Fact]
        public async Task Test1()
        {
            var request = new PublishRequest
            {
                TopicArn = "sns:topic:arn",
                Message = "Message to publish"
            };

            await _snsClient.PublishAsync(request);
        }
    }
}
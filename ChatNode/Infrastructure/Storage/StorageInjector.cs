using Amazon.Runtime;
using Amazon.S3;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.Storage;

public static class StorageInjector
{
    public static IServiceCollection AddStorage(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3Options = sp.GetRequiredService<IOptions<S3Options>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = s3Options.ServiceUrl,
                ForcePathStyle = s3Options.ForcePathStyle,
                UseHttp = s3Options.UseHttp,
                AuthenticationRegion = "us-east-1"
            };

            AWSCredentials credentials =
                string.IsNullOrWhiteSpace(s3Options.AccessKey) || string.IsNullOrWhiteSpace(s3Options.SecretKey)
                    ? new AnonymousAWSCredentials()
                    : new BasicAWSCredentials(s3Options.AccessKey, s3Options.SecretKey);

            return new AmazonS3Client(credentials, config);
        });

        services.AddSingleton<IFileStorage, S3FileStorage>();
        services.AddScoped<IDocumentRegistry, DocumentRegistry>();

        return services;
    }
}

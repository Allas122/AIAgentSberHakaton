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
                ForcePathStyle = true,
                UseHttp = s3Options.UseHttp,
                AuthenticationRegion = "us-east-1"
            };
            return new AmazonS3Client(new AnonymousAWSCredentials(), config);
        });

        services.AddSingleton<IFileStorage, S3FileStorage>();

        return services;
    }
}

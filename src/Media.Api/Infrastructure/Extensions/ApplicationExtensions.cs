

namespace Media.Api.Infrastructure.Extensions
{
    public static class ApplicationExtensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddDbContext<MediaDbContext>(configure =>
            {
                configure.UseInMemoryDatabase("MediaDb");
            });

            builder.Services.AddMassTransit(configure =>
            {
                var brokerConfig = builder.Configuration.GetSection(BrokerOptions.SectionName)
                                                        .Get<BrokerOptions>();
                if (brokerConfig is null)
                {
                    throw new ArgumentNullException(nameof(BrokerOptions));
                }

                configure.AddConsumers(Assembly.GetExecutingAssembly());
                configure.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(brokerConfig.Host, hostConfigure =>
                    {
                        hostConfigure.Username(brokerConfig.Username);
                        hostConfigure.Password(brokerConfig.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });

            builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            builder.Services.Configure<AppSettings>(
              builder.Configuration);

        }
    }
}

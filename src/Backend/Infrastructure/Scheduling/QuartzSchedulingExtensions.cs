using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Infrastructure.Scheduling;

public static class QuartzSchedulingExtensions
{
    /// <summary>When true (default), Worker uses Quartz.NET instead of the lightweight Cronos in-process scheduler.</summary>
    public const string UseQuartzConfigKey = "Worker:UseQuartzScheduler";

    public static IServiceCollection AddWorkerScheduling(this IServiceCollection services, IConfiguration configuration)
    {
        var useQuartz = configuration.GetValue(UseQuartzConfigKey, true);

        if (!useQuartz)
        {
            services.AddCronScheduling();
            return services;
        }

        var connectionString = configuration.GetConnectionString("DatabaseConnectionString");
        var usePersistentStore = configuration.GetValue("Worker:Quartz:UsePersistentStore", false)
                                 && !string.IsNullOrWhiteSpace(connectionString);

        services.AddQuartz(q =>
        {
            q.SchedulerName = configuration["Worker:Quartz:SchedulerName"] ?? "BaseProjectWorker";

            if (usePersistentStore)
                q.UsePersistentStore(store =>
                {
                    store.UseProperties = true;
                    store.UsePostgres(connectionString!);
                    store.UseClustering();
                    store.UseSystemTextJsonSerializer();
                });
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.AddHostedService<QuartzCronJobRegistrarHostedService>();
        return services;
    }
}
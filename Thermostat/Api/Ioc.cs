﻿using System;
using Hqv.CSharp.Common.Logging;
using Hqv.Thermostat.Api.Domain;
using Hqv.Thermostat.Api.Domain.Repositories;
using Hqv.Thermostat.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Hqv.Thermostat.Api
{
    public static class Ioc
    {
        public static void Register(IServiceCollection services, IConfigurationRoot configuration)
        {
            RegisterLogging(services, configuration);
            RegisterServices(services, configuration);
            RegisterInfrastructure(services, configuration);
            RegisterRepositories(services, configuration);
        }

        private static void RegisterLogging(IServiceCollection services, IConfiguration configuration)
        {
            var loggingPath = configuration["logging:path"];
            if (string.IsNullOrEmpty(loggingPath))
            {
                const string message = "logging:path cannot be empty in configuration";
                Log.Logger.Fatal(message);
                throw new Exception(message);
            }

            if (!Enum.TryParse(configuration["logging:minimum-level"], out LogEventLevel level))
            {
                const string message = "logging:minimum-level is incorrect in configuration file";
                Log.Logger.Fatal(message);
                throw new Exception(message);
            }
            var logLevelSwitch = new LoggingLevelSwitch {MinimumLevel = level};

            services.AddSingleton<IHqvLogger, CSharp.Common.Logging.Serilog.Logger>();
            services.AddSingleton<ILogger>(
                new LoggerConfiguration().MinimumLevel.ControlledBy(logLevelSwitch)
                    .WriteTo.File(new JsonFormatter(), loggingPath)
                    .CreateLogger()
            );
        }

        private static void RegisterServices(IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped(provider => new AuthenticationService.Settings());         
        }

        private static void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IEcobeeAuthenticator, Infrastructure.Ecobee.BearerAuthenticator>();
            services.AddScoped(provider => new Infrastructure.Ecobee.BearerAuthenticator.Settings(
                configuration["ecobee:base-uri"],
                configuration["ecobee:authorization-uri"],
                configuration["ecobee:token-uri"]));
        }

        private static void RegisterRepositories(IServiceCollection services, IConfiguration configuration)
        {            
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped(provider => new ClientRepository.Settings(configuration["connection-string"]));

            services.AddScoped<IEventLogDatabaseRepository, EventLogRepository>();
            services.AddScoped<IEventLogRepository, EventLogRepository>();
            services.AddScoped(provider => new EventLogRepository.Settings(configuration["connection-string"]));
        }
    }
}
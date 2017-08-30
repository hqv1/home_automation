using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Hqv.CSharp.Common.Validations;
using Hqv.Thermostat.Api.Domain;
using Hqv.Thermostat.Api.Domain.Entities;
using Hqv.Thermostat.Api.Domain.Repositories;
using Hqv.Thermostat.Api.Infrastructure.Ecobee.Parsers;
using Hqv.Thermostat.Api.Infrastructure.Ecobee.Shared;
using Newtonsoft.Json;

namespace Hqv.Thermostat.Api.Infrastructure.Ecobee
{
    public class ThermostatProvider : IThermostatProvider
    {
        private readonly IEventLogRepository _eventLogRepository;
        private readonly IHqvHttpClient _httpClient;
        private readonly Settings _settings;
        private string _correlationId;

        public class Settings
        {
            public Settings(string baseUri, string thermostatUri, bool storeResponse)
            {
                BaseUri = baseUri;
                ThermostatUri = thermostatUri;
                StoreResponse = storeResponse;
                Validator.Validate<Settings, SettingsValidator>(this);
            }

            public string BaseUri { get; }
            public string ThermostatUri { get; }
            public bool StoreResponse { get; }
        }

        private class SettingsValidator : AbstractValidator<Settings>
        {
            public SettingsValidator()
            {
                RuleFor(x => x.BaseUri).NotEmpty();
                RuleFor(x => x.ThermostatUri).NotEmpty();
            }
        }

        public ThermostatProvider(IEventLogRepository eventLogRepository, IHqvHttpClient httpClient, Settings settings)
        {
            _eventLogRepository = eventLogRepository;
            _httpClient = httpClient;
            _settings = settings;
        }

        public async Task<IEnumerable<Domain.Entities.Thermostat>> GetThermostats(string bearerToken, string correlationId = null)
        {
            _correlationId = correlationId;
            var thermostats = await _httpClient.GetAsyncWithBearerToken(
                baseUri: _settings.BaseUri,
                relativeUri: _settings.ThermostatUri,
                queryParameters: CreateQueryParameters(),
                bearerToken: bearerToken,
                parser: async json => await Parse(json));

            return thermostats;
        }

        private static ICollection<KeyValuePair<string, string>> CreateQueryParameters()
        {
            var body = new
            {
                selection = new
                {
                    selectionType = "registered",
                    selectionMatch = "",
                    includeRuntime = true,
                    includeSettings = true,
                    includeEvents = true
                }
            };
            var queryParameters = new Dictionary<string, string>()
            {
                {"format", "json"},
                {"body",  JsonConvert.SerializeObject(body)}
            };
            return queryParameters;
        }

        private async Task<IEnumerable<Domain.Entities.Thermostat>> Parse(object json)
        {
            if (_settings.StoreResponse)
            {
                await _eventLogRepository.Add(new EventLog(
                    "Ecobee", _settings.ThermostatUri, "ResponseBody", DateTime.UtcNow, _correlationId, entityObject: json));
            }

            return ThermostatListParser.Parse(json);
        }
        
    }
}
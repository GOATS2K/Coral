using Coral.Configuration;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.EventHub;
using Coral.Plugin.LastFM.ResponseTypes;
using Coral.PluginBase;
using Coral.PluginHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using RestSharp.Serializers.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Coral.Plugin.LastFM
{
    public interface ILastFmService
    {
        public string GetApiKey();
        public string HelloWorld();
        public void SetUserToken(string token);
    }
    public class LastFmService : ILastFmService, IPluginService
    {
        private readonly ILogger<LastFmService> _logger;
        private readonly TrackPlaybackEventEmitter _playbackEvents;
        private readonly RestClient _client;
        private readonly LastFmConfiguration _configuration;
        private LastFmUserSession? _session;
        private readonly string _sessionFile = Path.Join(ApplicationConfiguration.Plugins, "LastFmUser.json");

        public LastFmService(ILogger<LastFmService> logger, IHostServiceProxy serviceProxy, IOptions<LastFmConfiguration> options)
        {
            _logger = logger;
            _playbackEvents = serviceProxy.GetHostService<TrackPlaybackEventEmitter>();
            _client = new RestClient("https://ws.audioscrobbler.com/2.0/");
            _configuration = options.Value;
            _client.UseSystemTextJson();
        }

        private string GenerateRequestSignature(RestRequest request)
        {
            var queryParams = request.Parameters.Where(a => a.Type == ParameterType.GetOrPost).OrderBy(o => o.Name).ToList();
            var queryString = string.Join("", queryParams.Select(q => $"{q.Name}{q.Value}")) + _configuration.SharedSecret;
            var md5 = MD5.Create();
            var checksumBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return Convert.ToHexString(checksumBytes);
        }

        public void LoadSession()
        {
            if (_session == null && File.Exists(_sessionFile))
            {
                var contents = File.ReadAllText(_sessionFile);
                _session = JsonSerializer.Deserialize<LastFmUserSession>(contents);
                var sessionValid = CheckSession();
                if (!sessionValid)
                {
                    _logger.LogError("Invalid session, please re-authenticate.");
                }
            }
            else
            {
                _logger.LogWarning("No Last.fm session found, please visit /api/plugin/lastfm/authorize");
            }
        }

        private RestRequest GenerateRequest(string method)
        {
            return new RestRequest()
                .AddParameter("api_key", _configuration.ApiKey)
                .AddParameter("method", method)
                .AddParameter("sk", _session?.Key);
                
        }

        public bool CheckSession()
        {
            var response = _client.Get<UserResponse>(GenerateRequest("user.getInfo").AddParameter("format", "json"));
            if (response?.User.Name == _session?.Username)
            {
                _logger.LogInformation("Welcome back, {Username}.", response?.User.Name);
                return true;
            }
            return false;
        }

        public void SetUserToken(string token)
        {
            // use token to create session
            var body = new RestRequest()
                .AddParameter("api_key", _configuration.ApiKey)
                .AddParameter("method", "auth.getSession")
                .AddParameter("token", token);
            
            var signature = GenerateRequestSignature(body);
            body.AddParameter("api_sig", signature);
            
            var response = _client.Get<GetSessionResponse>(body);
            ArgumentNullException.ThrowIfNull(response);
            WriteUserSession(response);
        }

        private void WriteUserSession(GetSessionResponse session)
        {
            var user = new LastFmUserSession()
            {
                Key = session.Session.Key,
                Username = session.Session.Name
            };
            _logger.LogInformation("Writing user session, welcome {Name}!", user.Username);
            _session = user;
            var jsonString = JsonSerializer.Serialize(user);
            File.WriteAllText(_sessionFile, jsonString);
        }

        private void ScrobbleTrack(TrackDto track)
        {
            LoadSession();

            var artistString = string.Join(", ", track.Artists.Where(a => a.Role == ArtistRole.Main).Select(a => a.Name));
            // generate request with body instead of query
            var request = GenerateRequest("track.scrobble")
                .AddParameter("artist", artistString)
                .AddParameter("track", track.Title)
                .AddParameter("album", track.Album.Name)
                .AddParameter("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            var signature = GenerateRequestSignature(request);
            request
                .AddParameter("api_sig", signature)
                .AddParameter("format", "json");
            
            try
            {
                var response = _client.Post<ScrobbleResponse>(request);
                _logger.LogInformation("Scrobbled track: {Artist} - {Title}", response?.Scrobbles.Scrobble.Artist.Text, response?.Scrobbles.Scrobble.Artist.Text);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError("Failed to scrobble track with exception: {ex}", ex);
            }
        }

        private void Scrobble(object? sender, TrackPlaybackEventArgs e)
        {
            _logger.LogDebug("Scrobble event received!");
            ScrobbleTrack(e.Track);
        }


        public string HelloWorld()
        {
            _logger.LogInformation("Logged message from loaded plugin assembly");
            return "Hello world from LastFMService";
        }

        public string GetApiKey()
        {
            return _configuration.ApiKey;
        }

        public void RegisterEventHandlers()
        {
            _playbackEvents.TrackPlaybackEvent += Scrobble;
        }

        public void UnregisterEventHandlers()
        {
            _playbackEvents.TrackPlaybackEvent -= Scrobble;
        }
    }
}

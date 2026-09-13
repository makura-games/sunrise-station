using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.TTS;
using Prometheus;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSManager
{
    private static readonly Histogram RequestTimings = Metrics.CreateHistogram(
        "tts_req_timings",
        "Timings of TTS API requests",
        new HistogramConfiguration()
        {
            LabelNames = new[] {"type"},
            Buckets = Histogram.ExponentialBuckets(.1, 1.5, 10),
        });

    private static readonly Counter WantedCount = Metrics.CreateCounter(
        "tts_wanted_count",
        "Amount of wanted TTS audio.");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logMan = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private string _apiUrl = string.Empty;

    public void Initialize()
    {
        _sawmill = _logMan.GetSawmill("tts");
        _cfg.OnValueChanged(SunriseCCVars.TTSApiUrl, OnApiUrlChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.TTSApiToken, OnApiTokenChanged, true);
    }

    private void OnApiUrlChanged(string value)
    {
        _apiUrl = value;
    }

    private void OnApiTokenChanged(string value)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
    }

    public async Task<byte[]?> ConvertTextToSpeech(TTSVoicePrototype voicePrototype, string text, string? effect = null)
    {
        WantedCount.Inc();
        _sawmill.Verbose($"Generate new audio for '{text}' speech by '{voicePrototype.Speaker}' speaker");

        var body = new GenerateVoiceRequest
        {
            Text = text,
            Speaker = voicePrototype.Speaker,
            Provider = voicePrototype.Provider,
            // Pitch = pitch,
            // Rate = rate,
            Effect = effect
        };

        var request = CreateRequestLink(_apiUrl, body);

        var reqTime = DateTime.UtcNow;
        try
        {
            var timeout = _cfg.GetCVar(SunriseCCVars.TTSApiTimeout);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            var response = await _httpClient.GetAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _sawmill.Warning("TTS request was rate limited");
                    return null;
                }

                _sawmill.Error($"TTS request returned bad status code: {response.StatusCode}");
                return null;
            }

            var soundData = await response.Content.ReadAsByteArrayAsync(cts.Token);

            _sawmill.Debug($"Generated new audio for '{text}' speech by '{voicePrototype.Speaker}' speaker ({soundData.Length} bytes)");
            RequestTimings.WithLabels("Success").Observe((DateTime.UtcNow - reqTime).TotalSeconds);

            return soundData;
        }
        catch (TaskCanceledException)
        {
            RequestTimings.WithLabels("Timeout").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Timeout of request generation new audio for '{text}' speech by '{voicePrototype.Speaker}' speaker");
            return null;
        }
        catch (Exception e)
        {
            RequestTimings.WithLabels("Error").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Failed of request generation new sound for '{text}' speech by '{voicePrototype.Speaker}' speaker\n{e}");
            return null;
        }
    }

    private static string CreateRequestLink(string url, GenerateVoiceRequest body)
    {
        var uriBuilder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["provider"] = body.Provider;
        query["speaker"] = body.Speaker;
        query["text"] = body.Text;
        query["pitch"] = body.Pitch;
        query["rate"] = body.Rate;
        query["file"] = "1";
        query["ext"] = "ogg";
        if (body.Effect != null)
            query["effect"] = body.Effect;

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    private record GenerateVoiceRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = default!;

        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = default!;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = default!;

        [JsonPropertyName("pitch")]
        public string Pitch { get; set; } = default!;

        [JsonPropertyName("rate")]
        public string Rate { get; set; } = default!;

        [JsonPropertyName("effect")]
        public string? Effect { get; set; }
    }
}

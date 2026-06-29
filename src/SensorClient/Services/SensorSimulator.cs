using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Models;
using System.Text;
using System.Text.Json;

namespace SensorClient.Services;

public class SensorSimulator
{
    private readonly SensorConfig _config;
    private readonly HttpClient _httpClient;
    private readonly Random _rng = new();
    private long _messageSequence = 0;

    public SensorSimulator(SensorConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var temperature = GenerateTemperature();
            var alarmPriority = EvaluateAlarm(temperature);

            PrintToConsole(temperature, alarmPriority);
            await SendToServerAsync(temperature, alarmPriority, ct);

            _messageSequence++;

            var intervalMs = _rng.Next(1000, 10001);
            await Task.Delay(intervalMs, ct);
        }
    }

    private double GenerateTemperature() =>
        _config.TempMin + _rng.NextDouble() * (_config.TempMax - _config.TempMin);

    private int EvaluateAlarm(double temp)
    {
        if (temp >= _config.Alarms.Priority3) return 3;
        if (temp >= _config.Alarms.Priority2) return 2;
        if (temp >= _config.Alarms.Priority1) return 1;
        return 0;
    }

    private void PrintToConsole(double temp, int priority)
    {
        Console.ForegroundColor = priority switch
        {
            1 => ConsoleColor.Yellow,
            2 => ConsoleColor.DarkYellow,
            3 => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
        Console.WriteLine($"[{_config.SensorId}] {temp:F2}°C @ {DateTime.UtcNow:HH:mm:ss} | Alarm: {priority}");
        Console.ResetColor();
    }

    private async Task SendToServerAsync(double temperature, int alarmPriority, CancellationToken ct)
    {
        var message = new SensorMessage
        {
            MessageId = Guid.NewGuid(),
            SensorId = _config.SensorId,
            MessageSequence = _messageSequence,
            SentAt = DateTime.UtcNow,
            Temperature = temperature,
            AlarmPriority = alarmPriority,
            EncryptedPayload = "",
            Signature = ""
        };

        var json = JsonSerializer.Serialize(message);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("api/ingest", content, ct);
            if (!response.IsSuccessStatusCode)
                Console.WriteLine($"[{_config.SensorId}] Server vratio: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_config.SensorId}] Greska: {ex.Message}");
        }
    }
}
using SensorClient.Services;
using Shared.Contracts;
using Shared.Models;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

var configs = new List<SensorConfig>
{
    new SensorConfig { SensorId = "SENSOR-01", TempMin = 200, TempMax = 350, InitialQuality = DataQuality.GOOD, Alarms = new AlarmThresholds { Priority1 = 280, Priority2 = 310, Priority3 = 340 } },
    new SensorConfig { SensorId = "SENSOR-02", TempMin = 200, TempMax = 350, InitialQuality = DataQuality.UNCERTAIN, Alarms = new AlarmThresholds { Priority1 = 280, Priority2 = 310, Priority3 = 340 } },
    new SensorConfig { SensorId = "SENSOR-03", TempMin = 200, TempMax = 350, InitialQuality = DataQuality.GOOD, Alarms = new AlarmThresholds { Priority1 = 280, Priority2 = 310, Priority3 = 340 } },
    new SensorConfig { SensorId = "SENSOR-04", TempMin = 200, TempMax = 350, InitialQuality = DataQuality.BAD, Alarms = new AlarmThresholds { Priority1 = 280, Priority2 = 310, Priority3 = 340 } },
    new SensorConfig { SensorId = "SENSOR-05", TempMin = 200, TempMax = 350, InitialQuality = DataQuality.GOOD, Alarms = new AlarmThresholds { Priority1 = 280, Priority2 = 310, Priority3 = 340 } },
};

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5010") };

var tasks = configs.Select(config =>
    Task.Run(() => new SensorSimulator(config, httpClient).RunAsync(cts.Token))
).ToList();

Console.WriteLine("Svih 5 senzora pokrenuto. Ctrl+C za zaustavljanje.");
await Task.WhenAll(tasks);
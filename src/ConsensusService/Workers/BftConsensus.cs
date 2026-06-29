using ConsensusService.Data.Entities;

namespace ConsensusService.Workers;

public static class BftConsensus
{
    private const double DeviationThreshold = 50.0; // treshold for considering a reading as malicious

    public record ConsensusResult(double ConsensusValue, List<string> MaliciousSensorIds, int SampleCount);

    public static ConsensusResult Calculate(List<SensorReadingEntity> readingsLastMinute)
    {
        var latestPerSensor = readingsLastMinute
            .GroupBy(r => r.SensorId)
            .Select(g => g.OrderByDescending(r => r.ReceivedAt).First())
            .ToList();

        if (latestPerSensor.Count == 0)
            return new ConsensusResult(0, new List<string>(), 0);

        var values = latestPerSensor.Select(r => r.Temperature).OrderBy(v => v).ToList();
        double median = GetMedian(values);

        // Rangiraj senzore po odstupanju od medijane (najbliži prvi)
        var rankedByDeviation = latestPerSensor
            .Select(r => new { Reading = r, Deviation = Math.Abs(r.Temperature - median) })
            .OrderBy(x => x.Deviation)
            .ToList();

        const int minimumTrustedSensors = 2; // nikad manje od ovoliko GOOD senzora

        var malicious = new List<string>();
        var trusted = new List<double>();

        for (int i = 0; i < rankedByDeviation.Count; i++)
        {
            var item = rankedByDeviation[i];
            bool withinThreshold = item.Deviation <= DeviationThreshold;
            bool mustKeepForMinimum = trusted.Count < minimumTrustedSensors;

            if (withinThreshold || mustKeepForMinimum)
            {
                trusted.Add(item.Reading.Temperature);
            }
            else
            {
                malicious.Add(item.Reading.SensorId);
            }
        }

        double consensusValue = trusted.Average();

        return new ConsensusResult(consensusValue, malicious, latestPerSensor.Count);
    }

    // Helper method to calculate the median of a sorted list - middle value 
    private static double GetMedian(List<double> sortedValues)
    {
        int n = sortedValues.Count;
        if (n % 2 == 1) return sortedValues[n / 2];
        return (sortedValues[n / 2 - 1] + sortedValues[n / 2]) / 2.0;
    }
}
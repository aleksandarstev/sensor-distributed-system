using ConsensusService.Data.Entities;

namespace ConsensusService.Workers;

public static class BftConsensus
{
    private const double DeviationThreshold = 50.0; // treshold for considering a reading as malicious

    public record ConsensusResult(double ConsensusValue, List<string> MaliciousSensorIds, int SampleCount);

    public static ConsensusResult Calculate(List<SensorReadingEntity> readingsLastMinute)
    {
        // Group readings by SensorId and select the latest reading for each sensor
        var latestPerSensor = readingsLastMinute
            .GroupBy(r => r.SensorId)
            .Select(g => g.OrderByDescending(r => r.ReceivedAt).First())
            .ToList();

        if (latestPerSensor.Count == 0)
            return new ConsensusResult(0, new List<string>(), 0);

        var values = latestPerSensor.Select(r => r.Temperature).OrderBy(v => v).ToList();
        double median = GetMedian(values);

        var malicious = new List<string>();
        var trusted = new List<double>();

        foreach (var reading in latestPerSensor)
        {
            if (reading.Quality == "BAD")
            {
                malicious.Add(reading.SensorId);
                continue;
            }

            double deviation = Math.Abs(reading.Temperature - median);
            if (deviation > DeviationThreshold)
                malicious.Add(reading.SensorId);
            else
                trusted.Add(reading.Temperature);
        }

        double consensusValue = trusted.Count > 0 ? trusted.Average() : median;

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
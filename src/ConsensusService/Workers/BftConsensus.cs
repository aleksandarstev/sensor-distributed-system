using ConsensusService.Data.Entities;

namespace ConsensusService.Workers;

public static class BftConsensus
{
    private const double DeviationThreshold = 80.0; // maximum allowed deviation from the median to consider a reading as trusted

    public record ConsensusResult(
        double ConsensusValue,
        List<string> MaliciousSensorIds,    // sensors whose readings were considered outliers
        List<string> TrustedSensorIds,  // sensors whose readings were used to calculate the consensus value
        int SampleCount);

    public static ConsensusResult Calculate(List<SensorReadingEntity> readingsLastMinute)
    {
        // take the latest reading for each sensor
        var latestPerSensor = readingsLastMinute
            .GroupBy(r => r.SensorId)
            .Select(g => g.OrderByDescending(r => r.ReceivedAt).First())
            .ToList();

        if (latestPerSensor.Count == 0)
            return new ConsensusResult(0, new List<string>(), new List<string>(), 0);

        var values = latestPerSensor.Select(r => r.Temperature).OrderBy(v => v).ToList();
        double median = GetMedian(values);  // calculate the median of the latest readings

        var rankedByDeviation = latestPerSensor
            .Select(r => new { Reading = r, Deviation = Math.Abs(r.Temperature - median) })
            .OrderBy(x => x.Deviation)
            .ToList();

        const int minimumTrustedSensors = 2;    // ensure at least 2 sensors are trusted even if they exceed the deviation threshold

        var malicious = new List<string>();
        var trusted = new List<double>();
        var trustedIds = new List<string>();

        // Rank the readings by their deviation from the median and classify them as trusted or malicious
        for (int i = 0; i < rankedByDeviation.Count; i++)
        {
            var item = rankedByDeviation[i];
            bool withinThreshold = item.Deviation <= DeviationThreshold;
            bool mustKeepForMinimum = trusted.Count < minimumTrustedSensors;

            if (withinThreshold || mustKeepForMinimum)
            {
                trusted.Add(item.Reading.Temperature);
                trustedIds.Add(item.Reading.SensorId);
            }
            else
            {
                malicious.Add(item.Reading.SensorId);
            }
        }

        double consensusValue = trusted.Average();

        return new ConsensusResult(consensusValue, malicious, trustedIds, latestPerSensor.Count);
    }

    private static double GetMedian(List<double> sortedValues)
    {
        int n = sortedValues.Count;
        if (n % 2 == 1) return sortedValues[n / 2];
        return (sortedValues[n / 2 - 1] + sortedValues[n / 2]) / 2.0;
    }
}
namespace Sharding.Durable.Functions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Common.Sharding;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Newtonsoft.Json;

    public static class WriteOutput
    {
        [FunctionName("WriteOutput")]
        public static async Task Run([ActivityTrigger]DurableActivityContext ctx,
                                     [Blob("olympic-data-results/results.csv", Connection = "AzureWebJobsStorage")]CloudBlockBlob outputBlob,
                                     TraceWriter log)
        {
            var input = ctx.GetInput<IEnumerable<string>>();
            var resultsPerYear = DecompressAndDeserialize(input);
            var overallResults = GetOverallResults(resultsPerYear);

            outputBlob.Properties.ContentType = "text/csv";
            await outputBlob.UploadTextAsync(
                GetHeaders() +
                Environment.NewLine +
                string.Join(Environment.NewLine,
                    GetOrderedResults(overallResults)));
        }

        private static IEnumerable<Dictionary<string, MedalCount>> DecompressAndDeserialize(IEnumerable<string> input)
        {
            return input
                .Select(x => JsonConvert.DeserializeObject<Dictionary<string, MedalCount>>(StringCompressor.DecompressString(x)));
        }

        private static Dictionary<string, MedalCount> GetOverallResults(IEnumerable<Dictionary<string, MedalCount>> resultsPerYear)
        {
            var results = new Dictionary<string, MedalCount>();
            foreach (var resultPerYear in resultsPerYear)
            {
                foreach (var countryEntry in resultPerYear)
                {
                    if (results.ContainsKey(countryEntry.Key))
                    {
                        results[countryEntry.Key].Gold += countryEntry.Value.Gold;
                        results[countryEntry.Key].Silver += countryEntry.Value.Silver;
                        results[countryEntry.Key].Bronze += countryEntry.Value.Bronze;
                    }
                    else
                    {
                        results.Add(countryEntry.Key, countryEntry.Value);
                    }
                }
            }

            return results;
        }

        private static string GetHeaders()
        {
            return "Country,Golds,Silver,Bronze";
        }

        private static IEnumerable<string> GetOrderedResults(Dictionary<string, MedalCount> overallResults)
        {
            return overallResults
                .OrderByDescending(x => x.Value.Gold)
                .ThenByDescending(x => x.Value.Silver)
                .ThenByDescending(x => x.Value.Bronze)
                .Select(x => $"{x.Key},{x.Value.Gold},{x.Value.Silver},{x.Value.Bronze}");
        }
    }
}

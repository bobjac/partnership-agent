using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CsvHelper;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// Service that loads and manages ground truth data from embedded CSV resources.
    /// </summary>
    public class GroundTruthService : IGroundTruthService
    {
        private readonly List<GroundTruthItem> _groundTruthItems;

        public GroundTruthService()
        {
            _groundTruthItems = LoadGroundTruthData();
        }

        /// <summary>
        /// Retrieves the expected output for a given user prompt if it exists in the ground truth data.
        /// Uses case-insensitive comparison to match prompts.
        /// </summary>
        /// <param name="userPrompt">The user input prompt to search for.</param>
        /// <param name="module">Optional module filter to restrict search to specific modules.</param>
        /// <returns>The expected output if found, null otherwise.</returns>
        public string? GetExpectedOutput(string userPrompt, string? module = null)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
                return null;

            // First try without module filter to find any match
            var allItems = _groundTruthItems.AsEnumerable();

            // Find exact match first
            var exactMatch = allItems.FirstOrDefault(item => 
                string.Equals(item.UserPrompt, userPrompt, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
                return exactMatch.ExpectedOutput;

            // If no exact match, try partial match (contains)
            var partialMatch = allItems.FirstOrDefault(item => 
                item.UserPrompt.Contains(userPrompt, StringComparison.OrdinalIgnoreCase) ||
                userPrompt.Contains(item.UserPrompt, StringComparison.OrdinalIgnoreCase));

            return partialMatch?.ExpectedOutput;
        }

        /// <summary>
        /// Gets all ground truth items for a specific module.
        /// </summary>
        /// <param name="module">The module name to filter by.</param>
        /// <returns>Collection of ground truth items for the specified module.</returns>
        public IEnumerable<GroundTruthItem> GetGroundTruthByModule(string module)
        {
            return _groundTruthItems.Where(item => 
                string.Equals(item.Module, module, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all ground truth items.
        /// </summary>
        /// <returns>All ground truth items.</returns>
        public IEnumerable<GroundTruthItem> GetAllGroundTruth()
        {
            return _groundTruthItems.AsReadOnly();
        }

        /// <summary>
        /// Loads ground truth data from the embedded CSV resource.
        /// </summary>
        /// <returns>List of ground truth items.</returns>
        private static List<GroundTruthItem> LoadGroundTruthData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "PartnershipAgent.Core.Data.GroundTruthData.csv";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var records = csv.GetRecords<GroundTruthItem>().ToList();
            return records;
        }
    }
}
using System.Collections.Generic;
using System.Threading.Tasks;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Agents;

public interface IEntityResolutionAgent
{
    Task<List<ExtractedEntity>> ExtractEntitiesAsync(string prompt);
}
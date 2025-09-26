using System.Data.Common;

namespace PartnershipAgent.Core.Services;

public interface ISqlConnectionFactory
{
    DbConnection CreateConnection();
}
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}
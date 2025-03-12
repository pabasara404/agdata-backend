// Data/IDbConnectionFactory.cs
using System.Data;
using System.Threading.Tasks;

namespace AgData.Data
{
    public interface IDbConnectionFactory
    {
        Task<IDbConnection> CreateConnectionAsync();
    }
}
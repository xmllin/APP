using System.Threading;
using System.Threading.Tasks;
using Nexora.Models;

namespace Nexora.Services.Downloads
{
    public interface IDownloadProvider
    {
        Task<DownloadInfo> ResolveAsync(AppDefinition app, CancellationToken cancellationToken);
    }
}

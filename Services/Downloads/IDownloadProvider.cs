using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services.Downloads
{
    public interface IDownloadProvider
    {
        Task<DownloadInfo> ResolveAsync(AppDefinition app, CancellationToken cancellationToken);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services.Downloads
{
    public interface IReleaseDownloadProvider
    {
        Task<IReadOnlyList<AppRelease>> GetReleasesAsync(AppDefinition app, CancellationToken token);
    }
}

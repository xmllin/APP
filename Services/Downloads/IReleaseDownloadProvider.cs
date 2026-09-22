using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Models;

namespace Nexora.Services.Downloads
{
    public interface IReleaseDownloadProvider
    {
        Task<IReadOnlyList<AppRelease>> GetReleasesAsync(AppDefinition app, CancellationToken token);
    }
}

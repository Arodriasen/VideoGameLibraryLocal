using System.Threading.Tasks;
using VideoGameLibrary.Application.Models;

namespace VideoGameLibrary.Application.Abstractions
{
    public interface IUpdateCheckService
    {
        Task<UpdateInfo?> CheckForUpdateAsync();
    }
}

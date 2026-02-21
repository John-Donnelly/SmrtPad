using System.Threading.Tasks;
using Windows.Storage;

namespace SmrtPad.Services
{
    public interface IFileService
    {
        Task<StorageFile?> PickOpenFileAsync(string[] fileTypes);
        Task<StorageFile?> PickSaveFileAsync(string suggestedName, string defaultExtension);
        Task<StorageFile?> GetFileFromPathAsync(string path);
    }
}

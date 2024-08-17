using System.IO;
using System.Threading.Tasks;

namespace ICSharpCode.ILSpyX.FileLoaders
{
	public sealed class TmodFileLoader : IFileLoader
	{
		public Task<LoadResult?> Load(string fileName, Stream stream, FileLoadContext settings)
		{
			if (settings.ParentBundle != null)
			{
				return Task.FromResult<LoadResult?>(null);
			}

			var tmod = LoadedPackage.FromTmodFile(fileName);
			var result = tmod != null ? new LoadResult { Package = tmod } : null;
			return Task.FromResult(result);
		}
	}
}

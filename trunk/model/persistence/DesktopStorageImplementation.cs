using System;
using System.Linq;
using System.IO;
using System.Threading;

namespace LogJoint.Persistence
{
	public class DesktopStorageImplementation : IStorageImplementation
	{
		public DesktopStorageImplementation()
		{
			this.rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LogJoint\\";
			Directory.CreateDirectory(rootDirectory);
		}

		void IStorageImplementation.SetTrace(LJTraceSource trace)
		{
			this.trace = trace;
		}

		public void EnsureDirectoryCreated(string dirName)
		{
			// CreateDirectory doesn't fail is dir already exists
			Directory.CreateDirectory(rootDirectory + dirName);
		}

		public Stream OpenFile(string relativePath, bool readOnly)
		{
			// It is a common case when existing file is opened for reading.
			// Handle that without throwing hidden exceptions.
			if (readOnly && !File.Exists(rootDirectory + relativePath))
				return null;

			int maxTryCount = 10;
			int millisecsToWaitBetweenTries = 50;

			for (int tryIdx = 0; ; ++tryIdx)
			{
				try
				{
					var ret = new FileStream(rootDirectory + relativePath,
						readOnly ? FileMode.Open : FileMode.OpenOrCreate,
						readOnly ? FileAccess.Read : FileAccess.ReadWrite,
						FileShare.None);
					return ret;
				}
				catch (Exception e)
				{
					trace.Warning("Failed to open file {0}: {1}", relativePath, e.Message);
					if (tryIdx >= maxTryCount)
					{
						trace.Error(e, "No more tries. Giving up");
						if (readOnly)
							return null;
						else
							throw;
					}
					trace.Info("Will try agian. Tries left: {0}", maxTryCount - tryIdx);
					Thread.Sleep(millisecsToWaitBetweenTries);
				}
			}
		}

		public string[] ListDirectories(string rootRelativePath, CancellationToken cancellation)
		{
			return Directory.EnumerateDirectories(rootDirectory + rootRelativePath).Select(dir =>
			{
				cancellation.ThrowIfCancellationRequested();
				if (rootRelativePath == "")
					return Path.GetFileName(dir);
				else
					return rootRelativePath + Path.DirectorySeparatorChar + Path.GetFileName(dir);
			}).ToArray();
		}

		public void DeleteDirectory(string relativePath)
		{
			Directory.Delete(rootDirectory + relativePath, true);
		}

		static long CalcDirSize(DirectoryInfo d, CancellationToken cancellation)
		{
			cancellation.ThrowIfCancellationRequested();
			long ret = 0;
			ret = d.EnumerateFiles().Aggregate(ret, (c, fi) => { cancellation.ThrowIfCancellationRequested(); return c + fi.Length; });
			ret = d.EnumerateDirectories().Aggregate(ret, (c, di) => c + CalcDirSize(di, cancellation));
			return ret;
		}

		public long CalcStorageSize(CancellationToken cancellation)
		{
			return CalcDirSize(new DirectoryInfo(rootDirectory), cancellation);
		}

		public string AbsoluteRootPath { get { return rootDirectory; } }

		LJTraceSource trace = LJTraceSource.EmptyTracer;
		readonly string rootDirectory;
	};
}

using contracts;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace storage.disk
{
    public class DiskStorageProvider : IStorageProvider
    {
        public DiskStorageProvider(DiskStorageProviderConfig config)
        {
            mConfig = config;
        }

        public void WriteContent(string repositoryPath, string content)
        {
            var fileName = GetRepoPath(repositoryPath);

            var path = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(path);

            File.WriteAllText(fileName, content);
        }

        public string SaveBinaryContent(Stream content, string extension)
        {
            var repoPath = GetTargetFilePath(extension);
            var fileName = GetUploadPath(repoPath);
            var path = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            using (var w = File.Create(fileName)) content.CopyTo(w);

            return repoPath;
        }

        public string GetPhysicalPath(string binaryContentPath)
        {
            return GetUploadPath(binaryContentPath);
        }


        private string GetRepoPath(string path)
        {
            if (path.Contains("..")) throw new ArgumentException("Path cannot contain '..'");

            var relPath = path.TrimStart('/').TrimStart('\\');
            return Path.Combine(mConfig.BasePath, path);
        }

        private string GetUploadPath(string repoPath)
        {
            return Path.Combine(mConfig.UploadPath, repoPath);
        }

        private string GetTargetFilePath(string extension)
        {
            var f = Path.GetRandomFileName();
            f = Path.GetFileNameWithoutExtension(f) + extension;
            var h = GetHash(f);
            var p1 = h[0].ToString("X2");
            var p2 = h[1].ToString("X2");

            return p1 + '/' + p2 + '/' + f;
        }

        private static byte[] GetHash(string s)
        {
            MD5 md5Hasher = MD5.Create();
            return md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(s));
        }


        private DiskStorageProviderConfig mConfig;
    }
}

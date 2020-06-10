using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace contracts
{
    public interface IStorageProvider
    {
        void WriteContent(string repositoryPath, string content);

        string SaveBinaryContent(Stream content, string extension);

        string GetPhysicalPath(string binaryContentPath);
    }
}

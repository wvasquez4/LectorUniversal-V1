﻿using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace LectorUniversal.Server.Helpers
{
    public class FileUpload : IFileUpload
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _contextAccessor;
        public FileUpload(IWebHostEnvironment webHostEnvironment, IHttpContextAccessor httpContextAccessor)
        {
            _webHostEnvironment = webHostEnvironment;
            _contextAccessor = httpContextAccessor;
        }

        public Task DeleteFile(string Folder,string Type, string ImgUrl, bool complete)
        {
            string directory;

            if (complete != true)
            {
                var filename = Path.GetFileName(ImgUrl);
                directory = Path.Combine(_webHostEnvironment.WebRootPath, Type, Folder, filename);
               
                if (File.Exists(directory))
                {
                    File.Delete(directory);
                }
            }
            else
            {
                directory = Path.Combine(_webHostEnvironment.WebRootPath, Type, Folder);

                Directory.Delete(directory, true);
            }
            return Task.FromResult(0);
        }

        public Task DeleteChapter(string Folder, string Type, string ImgUrl, bool complete)
        {
            string directory;

            if (complete == true)
            {
                directory = Path.Combine(_webHostEnvironment.WebRootPath, Type, Folder);

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory,true);
                }
            }
            return Task.FromResult(0);
        }

        public async Task<string> EditFile(byte[] content, string extention, string actualFolder,string newFolder, string ImgUrl, string Type, bool complete)
        {
            if (!string.IsNullOrEmpty(ImgUrl))
            {
                await DeleteFile(actualFolder, Type ,ImgUrl, complete);
            }

            return await SaveFile(content, extention, Type, newFolder);
        }

        public async Task<string> SaveFile(byte[] content, string extention, string Type, string Folder)
        {
            var fileName = $"{Guid.NewGuid()}.{extention}";
            string wwwRootPath = _webHostEnvironment.WebRootPath;

            if (string.IsNullOrEmpty(wwwRootPath))
            {
                throw new Exception();
            }

            string Container = Path.Combine(wwwRootPath,Type, Folder).Replace(" ", "-");

            if (!Directory.Exists(Container))
            {
                Directory.CreateDirectory(Container);
            }

            string path = Path.Combine(Container, fileName);
            await File.WriteAllBytesAsync(path, content);

            var url = $"{_contextAccessor.HttpContext?.Request.Scheme}://{_contextAccessor.HttpContext?.Request.Host}";
            var dbPathUrl = Path.Combine(url,Type, Folder, fileName).Replace("\\","/");
            return dbPathUrl;
        }

    }
}

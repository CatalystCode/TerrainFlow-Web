using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using TerrainFlow.Models;
using TerrainFlow.ViewModels.Projects;

namespace TerrainFlow.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private IConfiguration _configuration;
        private readonly IHostingEnvironment _environment;
        private Storage _storage;

        public ProjectsController(IHostingEnvironment environment, IConfiguration configuration)
        {
            _storage = new Storage(configuration);

            _environment = environment;
            _configuration = configuration;
        }

        private ICollection<ProjectViewModel> GetProjectsFromTables()
        {
            var projects = _storage.GetProjectsForUser(GetEmailFromUser());

            var collection = new Collection<ProjectViewModel>();

            foreach (var entity in projects)
            {
                collection.Add(new ProjectViewModel
                {
                    Name = entity.Name,
                    URL = entity.RowKey
                });
            }

            return collection;
        }

        [HttpPost]
        public async Task<IActionResult> UploadProjectFiles()
        {
            var files = Request.Form.Files;
            var hashes = new List<string>();

            foreach (var file in files.Where(file => file.Length > 0))
            {
                hashes.Add(await ProcessUpload(file));
            }

            return new JsonResult(hashes);
        }

        // /Projects/
        [HttpGet]
        public IActionResult Index()
        {
            var projects = new ProjectsViewModel
            {
                Projects = GetProjectsFromTables()
            };

            return View(projects);
        }

        // /Projects/Add
        [Authorize]
        [HttpGet]
        public IActionResult Add()
        {
            if (GetEmailFromUser() == null)
            {
                return RedirectToAction("Signin", "Account");
            }

            return View();
        }

        #region Helpers

        private async Task<string> ProcessUpload(IFormFile file)
        {
            var t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var epoch = (int)t.TotalSeconds;
            var uploads = Path.Combine(_environment.WebRootPath, "uploads");
            var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            var unqiuefileName = fileName + "_" + epoch;
            var filePath = Path.Combine(uploads, unqiuefileName);

            // Save locally
            await file.SaveAsAsync(filePath);

            // TODO: Process file (transformations, etc)
            // TODO: Deal with file extension?

            // Make hash, upload to Azure Storage Blob
            var hash = CreateHashFromFile(filePath);
            await _storage.UploadFileToBlob(filePath, hash);

            // Take hash and blob url, save to tables
            _storage.SaveFileToTables(fileName, hash);

            return hash;
        }

        private string CreateHashFromFile(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = System.IO.File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return string.Concat(hash.Select(x => x.ToString("X2")));
                }
            }
        }

        private string GetEmailFromUser()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var email = identity.FindFirst(ClaimTypes.Email).Value;

            return email;
        }

        #endregion
    }
}

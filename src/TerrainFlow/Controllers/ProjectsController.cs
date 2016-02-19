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
        private CloudStorageAccount _storageAccount;
        private CloudBlobClient _blobClient;
        private CloudBlobContainer _container;
        private CloudTableClient _tableClient;
        private CloudTable _projectsTable;
        private IConfiguration _configuration;
        private readonly IHostingEnvironment _environment;

        public ProjectsController(IHostingEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public ICollection<ProjectViewModel> GetProjectsFromTables()
        {
            EnsureTable();

            var username = GetEmailFromUser();
            if (username == null) throw new ArgumentNullException(nameof(username));

            var collection = new Collection<ProjectViewModel>();
            var query = new TableQuery<ProjectEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey",
                    QueryComparisons.Equal, username));

            foreach (var entity in _projectsTable.ExecuteQuery(query))
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
            await UploadFileToBlob(filePath, hash);

            // Take hash and blob url, save to tables
            SaveFileToTables(fileName, hash);

            return hash;
        }

        private void EnsureTable()
        {
            if (_storageAccount == null)
            {
                _storageAccount = CloudStorageAccount.Parse(_configuration["STORAGE_CONNECTION"]);
            }

            if (_tableClient == null)
            {
                _tableClient = _storageAccount.CreateCloudTableClient();
            }

            if (_projectsTable == null)
            {
                _projectsTable = _tableClient.GetTableReference("projects");
                _projectsTable.CreateIfNotExists();
            }
        }

        private void EnsureContainer()
        {
            if (_storageAccount == null)
            {
                _storageAccount =  CloudStorageAccount.Parse(_configuration["STORAGE_CONNECTION"]);
            }

            if (_blobClient == null)
            {
                _blobClient = _storageAccount.CreateCloudBlobClient();
            }

            if (_container == null)
            {
                _container = _blobClient.GetContainerReference("projects");
            }
        }

        private void SaveFileToTables(string name, string file)
        {
            EnsureTable();
            var username = "";//this.User.GetUserName();
            if (username == null) throw new ArgumentNullException(nameof(username));

            var entity = new ProjectEntity(username, file, name);

            var insertOperation = TableOperation.Insert(entity);
            _projectsTable.Execute(insertOperation);
        }

        private async Task UploadFileToBlob(string filePath, string blobName)
        {
            EnsureContainer();

            CloudBlockBlob blockBlob = _container.GetBlockBlobReference(blobName);

            using (var fileStream = System.IO.File.OpenRead(filePath))
            {
                await blockBlob.UploadFromStreamAsync(fileStream);
            }
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

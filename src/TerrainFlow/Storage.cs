using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using TerrainFlow.Models;
using TerrainFlow.ViewModels.Projects;

namespace TerrainFlow
{
    public class Storage
    {
        private CloudStorageAccount _storageAccount;
        private CloudBlobClient _blobClient;
        private CloudBlobContainer _container;
        private CloudTableClient _tableClient;
        private CloudTable _projectsTable;

        private IConfiguration _configuration;

        public Storage(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void EnsureTable()
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

        public void EnsureContainer()
        {
            if (_storageAccount == null)
            {
                _storageAccount = CloudStorageAccount.Parse(_configuration["STORAGE_CONNECTION"]);
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

        public void SaveFileToTables(string name, string file)
        {
            EnsureTable();
            var username = "";//this.User.GetUserName();
            if (username == null) throw new ArgumentNullException(nameof(username));

            var entity = new ProjectEntity(username, file, name);

            var insertOperation = TableOperation.Insert(entity);
            _projectsTable.Execute(insertOperation);
        }

        public async Task UploadFileToBlob(string filePath, string blobName)
        {
            EnsureContainer();

            CloudBlockBlob blockBlob = _container.GetBlockBlobReference(blobName);

            using (var fileStream = System.IO.File.OpenRead(filePath))
            {
                await blockBlob.UploadFromStreamAsync(fileStream);
            }
        }

        public IEnumerable<ProjectEntity> GetProjectsForUser(string username)
        {
            EnsureTable();

            if (username == null) throw new ArgumentNullException(nameof(username));

            var query = new TableQuery<ProjectEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey",
                    QueryComparisons.Equal, username));

            return _projectsTable.ExecuteQuery(query);
        }
    }
}

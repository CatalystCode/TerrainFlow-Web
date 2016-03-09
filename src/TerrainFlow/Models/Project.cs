using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace TerrainFlow.Models
{
    public class ProjectEntity : TableEntity
    {
        public ProjectEntity(string user, string file, string name)
        {
            this.PartitionKey = user;
            this.RowKey = user;
            this.Name = name;
            this.Url = file;
        }

        public ProjectEntity() { }

        public string Name { get; set; }

        public string Url { get; set; }
    }
}

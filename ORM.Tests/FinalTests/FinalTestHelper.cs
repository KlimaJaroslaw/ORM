using Microsoft.VisualStudio.TestPlatform.TestHost;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ORM.Tests.FinalTests
{
    public static class FinalTestHelper
    {
        public static DbConfiguration BuildDb(string dbName, params Type[] entityTypes)
        {
            var metadataBuilder = new MetadataStoreBuilder();
            
            foreach (var entityType in entityTypes)
            {
                metadataBuilder.AddEntity(entityType);
            }
            
            metadataBuilder.UseNamingStrategy(new SnakeCaseNamingStrategy());
            var metadataStore = metadataBuilder.Build();
            var dbConfig = new DbConfiguration($"Data Source={dbName}", metadataStore);
            using (var context = new DbContext(dbConfig))
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }
            return dbConfig;
        }
    }
}

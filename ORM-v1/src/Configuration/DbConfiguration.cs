using ORM_v1.Mapping;
using System;

namespace ORM_v1.Configuration
{
    public class DbConfiguration
    {
        public string ConnectionString { get; }
        public IMetadataStore MetadataStore { get; }

        public DbConfiguration(string connectionString, IMetadataStore metadataStore)
        {
            ConnectionString = connectionString 
                               ?? throw new ArgumentNullException(nameof(connectionString));

            MetadataStore = metadataStore 
                            ?? throw new ArgumentNullException(nameof(metadataStore));
        }
    }
}
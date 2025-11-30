using ORM_v1.Mapping;
using System.Data;

public interface ISqlGenerator
{    
    IDbCommand GenerateSelect(IDbConnection connection, EntityMap map, object id);
    
    IDbCommand GenerateSelectAll(IDbConnection connection, EntityMap map);
    
    IDbCommand GenerateInsert(IDbConnection connection, EntityMap map, object entity);
    
    IDbCommand GenerateUpdate(IDbConnection connection, EntityMap map, object entity);
    
    IDbCommand GenerateDelete(IDbConnection connection, EntityMap map, object entity);
}
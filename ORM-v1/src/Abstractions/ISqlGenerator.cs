using ORM_v1.Mapping;
using ORM_v1.Query;

public interface ISqlGenerator
{    
    string QuoteIdentifier(string name); // e.g. [Age] or "Age"
    string GetParameterName(string name, int index); // e.g. @p1 
    SqlQuery GenerateSelect(EntityMap map, object id);
    SqlQuery GenerateSelectAll(EntityMap map);
    SqlQuery GenerateInsert(EntityMap map, object entity);
    SqlQuery GenerateUpdate(EntityMap map, object entity);
    SqlQuery GenerateDelete(EntityMap map, object entity);   
    SqlQuery GenerateComplexSelect(EntityMap map, QueryModel queryModel);
}




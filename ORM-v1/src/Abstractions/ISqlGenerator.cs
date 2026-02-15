using ORM_v1.Mapping;
using ORM_v1.Query;

public interface ISqlGenerator
{    
    string QuoteIdentifier(string name); // e.g. [Age] or "Age"
    string GetParameterName(string name, int index); // e.g. @p1 
    
    /// <summary>
    /// Zwraca alias tabeli dla danej encji według strategii dziedziczenia.
    /// </summary>
    string GetTableAlias(EntityMap map, IMetadataStore metadataStore);
    
    SqlQuery GenerateSelect(EntityMap map, object id, IMetadataStore metadataStore);
    SqlQuery GenerateSelectAll(EntityMap map, IMetadataStore metadataStore);
    SqlQuery GenerateInsert(EntityMap map, object entity);
    SqlQuery GenerateUpdate(EntityMap map, object entity);
    SqlQuery GenerateDelete(EntityMap map, object entity);   
    SqlQuery GenerateComplexSelect(EntityMap map, QueryModel queryModel, IMetadataStore metadataStore);
}




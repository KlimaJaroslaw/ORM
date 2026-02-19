using ORM_v1.Mapping;

namespace ORM_v1.Query;

/// <summary>
/// Kontekst strategii dziedziczenia - zawiera wszystkie informacje potrzebne do budowania SQL dla danej strategii.
/// </summary>
internal class InheritanceContext
{
    /// <summary>
    /// Typ strategii dziedziczenia.
    /// </summary>
    public InheritanceStrategyType StrategyType { get; set; }
    
    /// <summary>
    /// G³ówny alias tabeli (zawsze ustawiony).
    /// </summary>
    public string PrimaryAlias { get; set; } = null!;
    
    /// <summary>
    /// Nazwa g³ównej tabeli (FROM clause).
    /// </summary>
    public string BaseTable { get; set; } = null!;
    
    /// <summary>
    /// JOIN-y do tabel rodzica (TPT - INNER JOIN w górê hierarchii).
    /// </summary>
    public List<InheritanceJoinClause> ParentJoins { get; set; } = new();
    
    /// <summary>
    /// JOIN-y do tabel dzieci (TPT abstrakcyjna - LEFT JOIN w dó³ hierarchii).
    /// </summary>
    public List<InheritanceJoinClause> ChildJoins { get; set; } = new();
    
    /// <summary>
    /// Warunek WHERE dla discriminatora (TPH).
    /// </summary>
    public string? DiscriminatorWhereClause { get; set; }
    
    /// <summary>
    /// Parametry dla discriminatora.
    /// </summary>
    public Dictionary<string, object> DiscriminatorParameters { get; set; } = new();
    
    /// <summary>
    /// Lista wszystkich aliasów tabel w hierarchii (TPT).
    /// Format: Dictionary[TableName, Alias]
    /// </summary>
    public Dictionary<string, string> TableAliases { get; set; } = new();
    
    /// <summary>
    /// Lista wszystkich EntityMap w hierarchii TPH (root + wszystkie pochodne).
    /// </summary>
    public List<EntityMap> HierarchyMaps { get; set; } = new();
}

internal class InheritanceJoinClause
{
    public string Table { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public string Condition { get; set; } = null!;
    public JoinType JoinType { get; set; }
}

internal enum InheritanceStrategyType
{
    None,           // Brak dziedziczenia
    TablePerHierarchy,
    TablePerType,
    TablePerConcrete
}

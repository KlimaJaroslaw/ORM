using System;
using System.Linq.Expressions;

namespace ORM_v1.Query;

/// <summary>
/// Przechowuje informację o navigation property do załadowania (eager loading).
/// Obsługuje zarówno proste Include jak i zagnieżdżone ThenInclude.
/// </summary>
public class IncludeInfo
{
    public string NavigationPropertyName { get; }
    public LambdaExpression NavigationExpression { get; }

    /// <summary>
    /// Pełna ścieżka dla zagnieżdżonych includes (np. "Posts.Comments.Author").
    /// Dla prostego Include to po prostu NavigationPropertyName.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Segmenty ścieżki (np. ["Posts", "Comments", "Author"]).
    /// </summary>
    public string[] PathSegments => FullPath.Split('.');

    /// <summary>
    /// Czy to zagnieżdżony include (ThenInclude)?
    /// </summary>
    public bool IsNested => PathSegments.Length > 1;

    public IncludeInfo(string navigationPropertyName, LambdaExpression navigationExpression)
    {
        NavigationPropertyName = navigationPropertyName ?? throw new ArgumentNullException(nameof(navigationPropertyName));
        NavigationExpression = navigationExpression ?? throw new ArgumentNullException(nameof(navigationExpression));
        FullPath = navigationPropertyName; // Domyślnie pełna ścieżka = nazwa property
    }

    /// <summary>
    /// Konstruktor dla zagnieżdżonych includes (ThenInclude).
    /// </summary>
    internal IncludeInfo(string navigationPropertyName, LambdaExpression navigationExpression, string fullPath)
    {
        NavigationPropertyName = navigationPropertyName ?? throw new ArgumentNullException(nameof(navigationPropertyName));
        NavigationExpression = navigationExpression ?? throw new ArgumentNullException(nameof(navigationExpression));
        FullPath = fullPath ?? navigationPropertyName;
    }
}

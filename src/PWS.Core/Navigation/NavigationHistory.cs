using PWS.Core.Models;

namespace PWS.Core.Navigation;

/// <summary>
/// Gestisce lo stack di navigazione back/forward del browser.
/// </summary>
public sealed class NavigationHistory
{
    private readonly List<NavigationEntry> _entries = [];
    private int _currentIndex = -1;

    public NavigationEntry? Current =>
        _currentIndex >= 0 && _currentIndex < _entries.Count
            ? _entries[_currentIndex]
            : null;

    public bool CanGoBack => _currentIndex > 0;
    public bool CanGoForward => _currentIndex < _entries.Count - 1;

    public IReadOnlyList<NavigationEntry> BackStack =>
        _currentIndex > 0 ? _entries[.._currentIndex] : [];

    public IReadOnlyList<NavigationEntry> ForwardStack =>
        _currentIndex < _entries.Count - 1 ? _entries[(_currentIndex + 1)..] : [];

    /// <summary>Aggiunge un nuovo entry (tronca il forward stack).</summary>
    public void Push(NavigationEntry entry)
    {
        // Rimuove tutto ciò che è "avanti" rispetto alla posizione corrente
        if (_currentIndex < _entries.Count - 1)
            _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);

        _entries.Add(entry);
        _currentIndex = _entries.Count - 1;
    }

    /// <summary>Torna indietro di un passo. Restituisce l'entry corrente dopo il movimento.</summary>
    public NavigationEntry? GoBack()
    {
        if (!CanGoBack) return null;
        _currentIndex--;
        return Current;
    }

    /// <summary>Va avanti di un passo. Restituisce l'entry corrente dopo il movimento.</summary>
    public NavigationEntry? GoForward()
    {
        if (!CanGoForward) return null;
        _currentIndex++;
        return Current;
    }

    public void Clear()
    {
        _entries.Clear();
        _currentIndex = -1;
    }
}


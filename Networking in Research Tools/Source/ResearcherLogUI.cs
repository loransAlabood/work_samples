using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the researcher's received log stream and keeps the newest entry visible.</summary>
public class ResearcherLogUI : MonoBehaviour
{
    public static ResearcherLogUI Instance { get; private set; }
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField, Min(1)] private int maxCharacters = 30000;
    private readonly StringBuilder history = new StringBuilder();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Append one log entry, trimming older entries if the display grows too large.</summary>
    public void AddLogMessage(string message)
    {
        history.AppendLine(message);
        if (history.Length > maxCharacters)
        {
            int removeThrough = history.Length - maxCharacters;
            // Trim at the next line boundary to avoid cutting through an entry. Scanning the
            // builder directly avoids allocating a full copy of the history on every trim.
            int end = -1;
            for (int i = removeThrough; i < history.Length; i++)
            {
                if (history[i] == '\n') { end = i; break; }
            }
            history.Remove(0, end >= 0 ? end + 1 : removeThrough);
        }
        if (logText != null) logText.text = history.ToString();
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void ClearLog()
    {
        history.Clear();
        if (logText != null) logText.text = string.Empty;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}

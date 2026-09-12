using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The scrolling feed of what happened in the game, written in plain words.
/// The feed keeps only the newest lines and reuses the oldest widget once the
/// limit is reached.
/// </summary>
public sealed class HexworldEventLog : MonoBehaviour
{
    /// <summary>Where the lines are placed.</summary>
    [SerializeField]
    private RectTransform _content;

    /// <summary>The line the log clones. It stays switched off.</summary>
    [SerializeField]
    private TMP_Text _lineTemplate;

    /// <summary>The scroll view around the lines.</summary>
    [SerializeField]
    private ScrollRect _scrollRect;

    /// <summary>How many lines the feed keeps.</summary>
    [SerializeField]
    private int _maxLines = 50;

    /// <summary>The line widgets, oldest first.</summary>
    private readonly List<TMP_Text> _lines = new List<TMP_Text>();

    /// <summary>How many lines the feed keeps.</summary>
    public int MaxLines
    {
        get { return _maxLines; }
    }

    /// <summary>How many lines the feed shows right now.</summary>
    public int LineCount
    {
        get { return _lines.Count; }
    }

    /// <summary>
    /// Adds a line in the normal colour.
    /// </summary>
    /// <param name="text">What to write.</param>
    public void Append(string text)
    {
        Append(text, HexworldUiTheme.TextColor);
    }

    /// <summary>
    /// Adds a line.
    /// </summary>
    /// <param name="text">What to write.</param>
    /// <param name="color">Colour of the line.</param>
    public void Append(string text, Color color)
    {
        if (string.IsNullOrEmpty(text) || _content == null || _lineTemplate == null)
        {
            return;
        }

        _lineTemplate.gameObject.SetActive(false);

        TMP_Text line;
        if (_lines.Count >= Mathf.Max(1, _maxLines))
        {
            line = _lines[0];
            _lines.RemoveAt(0);
        }
        else
        {
            line = Instantiate(_lineTemplate, _content);
            line.gameObject.SetActive(true);
        }

        line.gameObject.name = "Line";
        line.rectTransform.SetAsLastSibling();
        line.text = text;
        line.color = color;
        _lines.Add(line);

        ScrollToBottom();
    }

    /// <summary>
    /// Throws every line away.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(_lines[i].gameObject);
            }
            else
            {
                DestroyImmediate(_lines[i].gameObject);
            }
        }

        _lines.Clear();
    }

    /// <summary>
    /// Moves the view to the newest line.
    /// </summary>
    public void ScrollToBottom()
    {
        if (_scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0f;
    }
}

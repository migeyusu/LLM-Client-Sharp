#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DiffPlex.Wpf.Controls;

namespace LLMClient.Component;

public partial class StyleResourceDictionary
{
    public StyleResourceDictionary()
    {
        InitializeComponent();
    }

    private void PreviousTrackCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DiffViewer diffViewer)
        {
            diffViewer.PreviousDiff();
        }
    }

    private void NextTrackCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DiffViewer diffViewer)
        {
            diffViewer.NextDiff();
        }
    }
}
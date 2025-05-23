﻿using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace LLMClient.UI;

public partial class DialogView : UserControl
{
    public DialogView()
    {
        InitializeComponent();
    }

    DialogViewModel ViewModel => (DialogViewModel)DataContext;

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogViewItem dialogViewItem)
        {
            ViewModel.DeleteItem(dialogViewItem);
        }
    }

    private void OnRedoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.ReBase(requestViewItem);
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.InsertClearContextItem(requestViewItem);
        }
    }
    
    private void Refresh_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is MultiResponseViewItem multiResponseViewItem)
        {
            ViewModel.RetryCurrent(multiResponseViewItem);
        }
    }

    private void EnterKeyInputBinding_OnChecked(object sender, RoutedEventArgs e)
    {
        if (this.FindResource("PromptKeyBinding") is InputBinding findResource)
        {
            PromptTextBox.InputBindings.Add(findResource);
        }
    }

    private void EnterKeyInputBinding_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (this.FindResource("PromptKeyBinding") is InputBinding findResource)
        {
            PromptTextBox.InputBindings.Remove(findResource);
        }
    }

    private void Redo_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ViewModel.Client != null;
    }

    private void PopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            var responseViewItem = popupBox.DataContext as MultiResponseViewItem;
            popupBox.PopupContent = new ResponseAddClientViewModel(responseViewItem!, this.ViewModel);
        }
    }

}

/*public class FlowDocumentScrollViewerEx : ContentControl
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(FlowDocument), typeof(FlowDocumentScrollViewerEx),
        new FrameworkPropertyMetadata(default(FlowDocument),
            new PropertyChangedCallback(DocumentPropertyChangedCallback)));

    private bool _documentAsLogicalChild;

    private static void DocumentPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlowDocumentScrollViewerEx flowDocumentScrollViewerEx)
        {
            FlowDocument? oldDocument = (FlowDocument?)e.OldValue;
            FlowDocument? newDocument = (FlowDocument?)e.NewValue;
            flowDocumentScrollViewerEx.OnDocumentChanged(oldDocument, newDocument);
        }
    }

    public void OnDocumentChanged(FlowDocument? oldDocument, FlowDocument? newDocument)
    {
        if (oldDocument != null)
        {
            if (this._documentAsLogicalChild)
                this.RemoveLogicalChild((object)oldDocument);
            if (this.docViewer != null)
                this.docViewer.Document = (FlowDocument)null;
            oldDocument.ClearValue(PathNode.HiddenParentProperty);
            oldDocument.StructuralCache.ClearUpdateInfo(true);
        }

        if (newDocument != null && LogicalTreeHelper.GetParent((DependencyObject)newDocument) != null)
        {
            ContentOperations.SetParent((ContentElement)newDocument, (DependencyObject)this);
            this._documentAsLogicalChild = false;
        }
        else
            this._documentAsLogicalChild = true;

        if (newDocument != null)
        {
            if (this.RenderScope != null)
                this.RenderScope.Document = newDocument;
            if (this._documentAsLogicalChild)
                this.AddLogicalChild((object)newDocument);
            newDocument.SetValue(PathNode.HiddenParentProperty, (object)this);
            newDocument.StructuralCache.ClearUpdateInfo(true);
        }
    }

    public FlowDocument Document
    {
        get { return (FlowDocument)GetValue(DocumentProperty); }
        set { SetValue(DocumentProperty, value); }
    }

    private object? docViewer;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        /*var type = Assembly.GetEntryAssembly().GetType("MS.Internal.Documents.FlowDocumentView");
        var instance = Activator.CreateInstance(type);#1#
        var assembly =
            Assembly.Load(new AssemblyName(
                "PresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
        var type = assembly.GetType("MS.Internal.Documents.FlowDocumentView");
        /*var templateChild = this.GetTemplateChild("PART_ContentHost") as ScrollViewer;
        var type = templateChild.Content.GetType();#1#
        var instance = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        docViewer = instance.Invoke(null);
        this.Content = docViewer;
    }
}*/
﻿using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component.CustomControl;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;

namespace LLMClient.Project;

public class ProjectSessionViewModel : DialogSessionViewModel, IProjectSession
{
    public string? WorkingDirectory
    {
        get { return ParentProject.Option.RootPath; }
    }

    public RunPlatform Platform
    {
        get { return ParentProject.Option.Platform; }
    }

    public override AIContextProvider[]? ContextProviders
    {
#pragma warning disable MAAI001
        get
        {
            if (ParentProject.SkillsProviders == null)
            {
                return null;
            }

            return ParentProject.SkillsProviders.OfType<AIContextProvider>().ToArray();
        }
#pragma warning restore MAAI001
    }

    public override IPromptCommandAggregate? PromptCommand
    {
        get { return ParentProject.Option.PromptInjector; }
    }

    /// <summary>
    /// Task内的上下文
    /// </summary>
    public override string? SystemPrompt
    {
        get { return ParentProject.Context; }
    }

    public override IEnumerable<Type> SupportedAgents
    {
        get { return base.SupportedAgents.Concat(ParentProject.ProjectAgents); }
    }

    /// <summary>
    /// 工具是否可以选择
    /// </summary>
    public virtual bool IsToolsSelectable => true;

    public ProjectViewModel ParentProject { get; }

    /// <summary>
    /// summary of the task, when task end, generate for total context.
    /// </summary>
    public string? Summary
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public static ICommand RemoveFromProjectCommand
    {
        get
        {
            return field ??= new RelayCommand<ProjectSessionViewModel>(async projectSession =>
            {
                if (!await Extension.ShowConfirm(
                        "Are you sure to remove this session from project? This action cannot be undone."))
                {
                    return;
                }

                try
                {
                    projectSession?.ParentProject.RemoveSession(projectSession);
                }
                catch (Exception e)
                {
                    MessageBoxes.Error("Failed to remove session from project: " + e.Message, "Error");
                }
            });
        }
    }

    public ProjectSessionViewModel(ProjectViewModel parentProject, IMapper mapper,
        Summarizer summarizer, GlobalOptions options,
        IList<CheckableFunctionGroupTree>? functionGroupTrees = null,
        IDialogItem? rootNode = null, IDialogItem? currentLeaf = null)
        : base(options, summarizer, rootNode, currentLeaf)
    {
        ParentProject = parentProject;
        SelectedFunctionGroups = functionGroupTrees;
        PropertyChanged += OnPropertyChanged;
    }

    private readonly string[] _notTrackingProperties =
    [
        nameof(ScrollViewItem),
        nameof(SearchText)
    ];

    public override Task OnPreviewRequest(CancellationToken token)
    {
        return ParentProject.PreviewProcessing(token);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        if (_notTrackingProperties.Contains(propertyName))
        {
            return;
        }

        IsDataChanged = true;
    }

    public override IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        return base.GetFunctionGroups().Concat(ParentProject.ProjectTools);
    }
}
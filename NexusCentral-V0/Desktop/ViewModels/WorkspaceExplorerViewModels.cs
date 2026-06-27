using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NexusCentral_Avalonia.ViewModels;

public enum ExplorerTreeNodeKind
{
    WorkspaceRoot,
    Directory,
    File
}

public partial class ExplorerTreeNodeViewModel : ObservableObject
{
    public ExplorerTreeNodeViewModel(
        ExplorerTreeNodeKind nodeKind,
        string workspaceId,
        string workspaceName,
        string workspaceKind,
        string fullPath,
        string relativePath,
        string displayName,
        bool hasChildren,
        int depth,
        bool isPinned = false)
    {
        NodeKind = nodeKind;
        WorkspaceId = workspaceId;
        WorkspaceName = workspaceName;
        WorkspaceKind = workspaceKind;
        FullPath = fullPath;
        RelativePath = relativePath;
        DisplayName = displayName;
        HasChildren = hasChildren;
        Depth = depth;
        IsPinned = isPinned;
    }

    public ExplorerTreeNodeKind NodeKind { get; }
    public string WorkspaceId { get; }
    public string WorkspaceName { get; }
    public string WorkspaceKind { get; }
    public string FullPath { get; }
    public string RelativePath { get; }
    public string DisplayName { get; }
    public bool HasChildren { get; }
    public int Depth { get; }
    public bool IsPinned { get; }
    public ObservableCollection<ExplorerTreeNodeViewModel> Children { get; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _childrenLoaded;

    public bool IsWorkspaceRoot => NodeKind == ExplorerTreeNodeKind.WorkspaceRoot;
    public bool IsDirectory => NodeKind != ExplorerTreeNodeKind.File;
    public bool IsFile => NodeKind == ExplorerTreeNodeKind.File;
    public bool IsSandbox => string.Equals(WorkspaceKind, "sandbox", StringComparison.OrdinalIgnoreCase);
    public bool IsDefaultSandbox => string.Equals(WorkspaceId, "sandbox", StringComparison.OrdinalIgnoreCase);
    public bool CanDeleteSandbox => IsWorkspaceRoot && IsSandbox && !IsDefaultSandbox;
    public string WorkspaceCaption => IsSandbox ? WorkspaceName : IsPinned ? $"{WorkspaceName} *" : WorkspaceName;
    public string PathSummary => IsSandbox ? "Temp-backed scratch workspace" : FullPath;
    public string ExpansionGlyph => HasChildren ? (IsExpanded ? "▾" : "▸") : " ";
    public string KindGlyph => NodeKind switch
    {
        ExplorerTreeNodeKind.WorkspaceRoot => IsSandbox ? "⌂" : "▣",
        ExplorerTreeNodeKind.Directory => "▣",
        _ => "•"
    };
    public Thickness IndentMargin => new(Math.Max(0, Depth) * 12, 0, 0, 0);
    public string TooltipText => IsWorkspaceRoot && IsSandbox ? "Sandbox workspace" : FullPath;
    public string NodeKey => IsWorkspaceRoot ? $"root::{WorkspaceId}" : $"path::{WorkspaceId}::{FullPath}";

    public IEnumerable<ExplorerTreeNodeViewModel> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpansionGlyph));
    }
}

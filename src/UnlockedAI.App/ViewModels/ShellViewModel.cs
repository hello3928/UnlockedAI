using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;

namespace UnlockedAI.ViewModels;

/// <summary>The sidebar: the list of saved chats and which one is open.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly ConversationRepository _conversations;
    private readonly ChatViewModel _chat;

    // True while this class is moving the selection itself, so that doesn't count as the user picking a chat.
    private bool _syncingSelection;

    public ShellViewModel(ConversationRepository conversations, ChatViewModel chat)
    {
        _conversations = conversations;
        _chat = chat;
        HasNoConversations = true;

        _chat.ConversationChanged += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = [];

    [ObservableProperty]
    public partial ConversationItemViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial bool HasNoConversations { get; set; }

    public Task InitializeAsync() => RefreshAsync();

    public Task RenameAsync(ConversationItemViewModel item, string title) =>
        TryAsync(async () =>
        {
            await _conversations.RenameAsync(item.Id, title);
            item.Title = title;
        });

    public Task DeleteAsync(ConversationItemViewModel item) =>
        TryAsync(async () =>
        {
            if (_chat.ConversationId == item.Id)
            {
                await _chat.StartNewAsync();
            }

            await _conversations.DeleteAsync(item.Id);
            await RefreshAsync();
        });

    [RelayCommand]
    private async Task NewChatAsync()
    {
        await _chat.StartNewAsync();
        Select(null);
    }

    partial void OnSelectedChanged(ConversationItemViewModel? value)
    {
        if (!_syncingSelection && value is not null)
        {
            _ = _chat.OpenAsync(value.Id);
        }
    }

    private Task RefreshAsync() =>
        TryAsync(async () =>
        {
            Reconcile(await _conversations.ListAsync());
            HasNoConversations = Conversations.Count == 0;
            Select(Conversations.FirstOrDefault(item => item.Id == _chat.ConversationId));
        });

    /// <summary>
    /// Brings the list in line with what is stored by moving, adding and removing single rows,
    /// so the list view keeps its scroll position and doesn't flicker.
    /// </summary>
    private void Reconcile(List<ConversationSummary> stored)
    {
        _syncingSelection = true;
        try
        {
            for (var index = 0; index < stored.Count; index++)
            {
                var summary = stored[index];
                var existingIndex = IndexOf(summary.Id, startAt: index);

                if (existingIndex < 0)
                {
                    Conversations.Insert(index, new ConversationItemViewModel(summary.Id, summary.Title));
                    continue;
                }

                if (existingIndex != index)
                {
                    Conversations.Move(existingIndex, index);
                }

                Conversations[index].Title = summary.Title;
            }

            while (Conversations.Count > stored.Count)
            {
                Conversations.RemoveAt(Conversations.Count - 1);
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private int IndexOf(long id, int startAt)
    {
        for (var index = startAt; index < Conversations.Count; index++)
        {
            if (Conversations[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private void Select(ConversationItemViewModel? item)
    {
        _syncingSelection = true;
        try
        {
            Selected = item;
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}

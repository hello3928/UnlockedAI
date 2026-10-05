using CommunityToolkit.Mvvm.ComponentModel;

namespace UnlockedAI.ViewModels;

/// <summary>Anything that takes a row in the chat: a message, or a tool the model used.</summary>
public abstract class ChatItemViewModel : ObservableObject;

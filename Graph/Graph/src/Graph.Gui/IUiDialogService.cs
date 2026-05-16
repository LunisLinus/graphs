namespace Graph.Gui;

public interface IUiDialogService
{
    Task ShowMessage(string title, string message);
    Task<bool> Confirm(string title, string message, string accept, string cancel);
    Task<string?> Prompt(string title, string message, string? placeholder = null, string? initialValue = null);
    Task<string?> Choose(string title, string cancel, params string[] options);
}


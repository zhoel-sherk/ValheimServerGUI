using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ValheimServerGUI.Avalonia
{
    /// <summary>
    /// Resolves a ViewModel to its View by naming convention (FooViewModel -> FooView).
    /// ViewModels that are shown via DataTemplates are located here; the main window is
    /// resolved from DI instead.
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null) return null;

            var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data) => data is ObservableObject;
    }
}
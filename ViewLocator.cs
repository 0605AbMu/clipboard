using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MacDesktopApp.ViewModels;
using MacDesktopApp.Views;

namespace MacDesktopApp;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is MainWindowViewModel)
        {
            return new MainWindow();
        }

        return null;
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}


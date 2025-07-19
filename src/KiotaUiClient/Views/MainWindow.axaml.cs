using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using Path = Avalonia.Controls.Shapes.Path;

namespace KiotaUiClient.Views;

public partial class MainWindow : Window
{
    private Path? _maximizeIcon;
    private Grid? _titleBar;

    public MainWindow()
    {
        InitializeComponent();

        // Get a reference to the maximize icon for updating
        _maximizeIcon = this.FindControl<Path>("MaximizeIcon");

        // Get a reference to the title bar for dragging
        _titleBar = this.FindControl<Grid>("TitleBar");

        // Update the maximize/restore icon based on the initial window state
        UpdateMaximizeRestoreIcon();

        // Add event handler for window state changes to update the icon
        this.PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
            {
                UpdateMaximizeRestoreIcon();
            }
        };

        // Make the window draggable from the title bar
        if (_titleBar != null)
        {
            _titleBar.PointerPressed += TitleBar_PointerPressed;
        }
    }


    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        if (e.ClientSize.Height <= 700)
        {
            this.Height = 700;
        }
        if (e.ClientSize.Width <= 600)
        {
            this.Width = 600;
        }
    }
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // If window is maximized, we need to restore it before dragging
            if (WindowState == WindowState.Maximized)
            {
                // Get the mouse position relative to the window
                var position = e.GetPosition(this);

                // Restore the window
                WindowState = WindowState.Normal;

                // Adjust window position to make it appear under the cursor
                // This creates a more natural feel when dragging from maximized state
                Position = new PixelPoint(
                    (int)(e.GetPosition(null).X - (position.X * Width / Bounds.Width)),
                    0);
            }

            // Begin the drag operation
            BeginMoveDrag(e);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateMaximizeRestoreIcon()
    {
        if (_maximizeIcon != null)
        {
            // Use a different icon depending on whether the window is maximized
            if (WindowState == WindowState.Maximized)
            {
                _maximizeIcon.Data = Geometry.Parse("M 0,2 H 8 V 10 H 0 Z M 2,0 H 10 V 8 H 8 V 2 H 2 Z");
            }
            else
            {
                _maximizeIcon.Data = Geometry.Parse("M 0,0 H 10 V 10 H 0 Z");
            }
        }
    }

    private void Exit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

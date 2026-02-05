using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using KiotaUiClient.Core.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KiotaUiClient.Views;

using Path = Avalonia.Controls.Shapes.Path;


public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private Path? _maximizeIcon;
    private Grid? _titleBar;

    // Guarda o último tamanho em estado Normal (para não salvar dimensões maximizadas).
    private double _lastNormalWidth;
    private double _lastNormalHeight;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = (Application.Current as App)?.Services?.GetRequiredService<ISettingsService>()
                           ?? throw new InvalidOperationException("Services not initialized");

        // Carrega tamanho salvo (aplica mínimos).
        var savedWidth = _settingsService.GetDouble("Window.Width", Width);
        var savedHeight = _settingsService.GetDouble("Window.Height", Height);
        Width = Math.Max(600, savedWidth);
        Height = Math.Max(600, savedHeight);

        // Inicializa o último tamanho normal com os atuais.
        _lastNormalWidth = Width;
        _lastNormalHeight = Height;

        // Get a reference to the maximize icon for updating
        _maximizeIcon = this.FindControl<Path>("MaximizeIcon");

        // Get a reference to the title bar for dragging
        _titleBar = this.FindControl<Grid>("TitleBar");

        // Update the maximize/restore icon based on the initial window state
        UpdateMaximizeRestoreIcon();

        // Add event handler for window state changes to update the icon
        PropertyChanged += (_, args) =>
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

        // Guardar tamanho ao fechar
        Closing += (_, _) => SaveWindowSize();
    }


    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);

        // Enforce mínimos
        if (e.ClientSize.Height <= 700)
        {
            Height = 700;
        }
        if (e.ClientSize.Width <= 600)
        {
            Width = 600;
        }

        // Memoriza último tamanho apenas quando em estado normal
        if (WindowState == WindowState.Normal)
        {
            _lastNormalWidth = Width;
            _lastNormalHeight = Height;
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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

    private void SaveWindowSize()
    {
        // Se estiver normal, usa tamanho atual; caso contrário, usa último tamanho normal memorizado.
        var w = WindowState == WindowState.Normal ? Width : _lastNormalWidth;
        var h = WindowState == WindowState.Normal ? Height : _lastNormalHeight;

        // Respeita mínimos
        w = Math.Max(600, w);
        h = Math.Max(600, h);

        _settingsService.SetDouble("Window.Width", w);
        _settingsService.SetDouble("Window.Height", h);
    }

    private void Exit_OnClick(object sender, RoutedEventArgs e) => Close();
}


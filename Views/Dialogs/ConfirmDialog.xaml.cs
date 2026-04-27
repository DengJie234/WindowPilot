using System.Windows;
using System.Windows.Media;
using WindowPilot.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace WindowPilot.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(ConfirmDialogOptions options)
    {
        InitializeComponent();
        DataContext = new ConfirmDialogState(this, options);
    }

    public ConfirmDialogResult Result { get; private set; } = ConfirmDialogResult.Cancel;

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmDialogResult.Primary;
        DialogResult = true;
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmDialogResult.Secondary;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmDialogResult.Cancel;
        DialogResult = false;
    }

    private sealed class ConfirmDialogState
    {
        private readonly Window _window;

        public ConfirmDialogState(Window window, ConfirmDialogOptions options)
        {
            _window = window;
            Title = options.Title;
            Message = options.Message;
            DetailLines = options.DetailLines;
            PrimaryButtonText = options.PrimaryButtonText;
            SecondaryButtonText = options.SecondaryButtonText;
            CancelButtonText = options.CancelButtonText;
            HasSecondaryButton = !string.IsNullOrWhiteSpace(options.SecondaryButtonText);
            HasDetailLines = options.DetailLines.Count > 0;
            PrimaryButtonStyle = (Style)_window.FindResource(options.IsPrimaryDanger ? "DangerButton" : "PrimaryButton");

            (BadgeText, AccentBackground, AccentForeground) = options.Kind switch
            {
                ConfirmDialogKind.Danger => ("!", Brush("#FEE2E2"), Brush("#DC2626")),
                ConfirmDialogKind.Warning => ("!", Brush("#FEF3C7"), Brush("#D97706")),
                _ => ("i", Brush("#DBEAFE"), Brush("#2563EB"))
            };
        }

        public string Title { get; }
        public string Message { get; }
        public IReadOnlyList<string> DetailLines { get; }
        public string PrimaryButtonText { get; }
        public string SecondaryButtonText { get; }
        public string CancelButtonText { get; }
        public bool HasSecondaryButton { get; }
        public bool HasDetailLines { get; }
        public Style PrimaryButtonStyle { get; }
        public string BadgeText { get; }
        public MediaBrush AccentBackground { get; }
        public MediaBrush AccentForeground { get; }

        private static MediaBrush Brush(string color) =>
            new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }
}

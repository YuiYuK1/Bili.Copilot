// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 长按倍速播放时显示的速度指示器.
/// </summary>
public sealed partial class SpeedPlayOverlay : PlayerControlBase
{
    private static readonly Brush SelectedBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
    private static readonly Brush UnselectedBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeedPlayOverlay"/> class.
    /// </summary>
    public SpeedPlayOverlay() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        BuildSpeedOptions();
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(PlayerViewModel? oldValue, PlayerViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnViewModelPropertyChanged;
        }

        BuildSpeedOptions();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.TempSpeedIndex) or nameof(PlayerViewModel.IsHoldingSpeedChanging))
        {
            BuildSpeedOptions();
        }
    }

    private void BuildSpeedOptions()
    {
        if (SpeedOptions is null || ViewModel is null)
        {
            return;
        }

        SpeedOptions.Children.Clear();
        var options = ViewModel.TempSpeedOptions;
        for (var i = 0; i < options.Count; i++)
        {
            var isSelected = i == ViewModel.TempSpeedIndex;
            var text = new TextBlock
            {
                Text = $"{options[i]:0.#}X",
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                FontSize = isSelected ? 18 : 12,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? SelectedBrush : UnselectedBrush,
                Opacity = isSelected ? 1 : 0.55,
                Margin = new Thickness(0, isSelected ? 2 : 0, 0, isSelected ? 2 : 0),
            };
            SpeedOptions.Children.Add(text);
        }
    }
}

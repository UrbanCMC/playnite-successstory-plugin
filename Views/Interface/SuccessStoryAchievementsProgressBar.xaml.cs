﻿using System;
using System.Windows;
using System.Windows.Controls;


namespace SuccessStory.Views.Interface
{
    /// <summary>
    /// Logique d'interaction pour SuccessStoryAchievementsProgressBar.xaml
    /// </summary>
    public partial class SuccessStoryAchievementsProgressBar : UserControl
    {
        public SuccessStoryAchievementsProgressBar(long value, long maxValue, bool showPercent, bool showIndicator)
        {
            InitializeComponent();

            if (showIndicator)
            {
                AchievementsIndicator.Content = value + "/" + maxValue;
            }
            else
            {
                AchievementsIndicator.Content = "";
                AchievementsProgressBar.SetValue(Grid.ColumnProperty, 0);
                AchievementsProgressBar.SetValue(Grid.ColumnSpanProperty, 3);
            }

            AchievementsProgressBar.Value = value;
            AchievementsProgressBar.Maximum = maxValue;

            if (showPercent)
            {
                AchievementsPercent.Content = (maxValue != 0) ? (int)Math.Round((double)(value * 100 / maxValue)) + "%" : 0 + "%";
            }
            else
            {
                AchievementsPercent.Content = "";
            }
        }

        private void Grid_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Define height & width
            var parent = ((FrameworkElement)((FrameworkElement)((FrameworkElement)sender).Parent).Parent);

            if (!double.IsNaN(parent.Height))
            {
                ((FrameworkElement)sender).Height = parent.Height;
            }

            if (!double.IsNaN(parent.Width))
            {
                ((FrameworkElement)sender).Width = parent.Width;
            }
        }
    }
}

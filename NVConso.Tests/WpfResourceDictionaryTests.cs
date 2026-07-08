using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NVConso.Tests;

public sealed class WpfResourceDictionaryTests
{
    [Fact]
    public void ThemeAndCommonStyles_ShouldLoad_ForLightTheme()
    {
        RunOnStaThread(() => AssertDesignSystemResourcesLoad("LightTheme"));
    }

    [Fact]
    public void ThemeAndCommonStyles_ShouldLoad_ForDarkTheme()
    {
        RunOnStaThread(() => AssertDesignSystemResourcesLoad("DarkTheme"));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void RepresentativeDesignSystemControls_ShouldMeasure_ForDpiScale(double scale)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(LoadResourceDictionary("Themes/LightTheme.xaml"));
            resources.MergedDictionaries.Add(LoadResourceDictionary("Themes/CommonStyles.xaml"));

            var panel = new StackPanel
            {
                LayoutTransform = new ScaleTransform(scale, scale),
                Resources = resources
            };

            panel.Children.Add(new Button
            {
                Content = "Enregistrer",
                Tag = "\uE73E",
                Style = (Style)resources["ButtonPrimary"]
            });
            panel.Children.Add(new Button
            {
                Content = "Ouvrir dossier",
                Tag = "\uE8B7",
                Style = (Style)resources["IconTextButton"]
            });
            panel.Children.Add(new Border
            {
                Style = (Style)resources["StatusBadge"],
                Child = new TextBlock { Text = "Modifications non enregistrées" }
            });
            panel.Children.Add(new Border
            {
                Style = (Style)resources["Card"],
                Child = new TextBlock { Text = "Carte de test DPI" }
            });

            panel.Measure(new Size(800, 600));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, panel.DesiredSize.Height));
            panel.UpdateLayout();

            Assert.True(panel.DesiredSize.Width > 0);
            Assert.True(panel.DesiredSize.Height > 0);
            Assert.False(double.IsNaN(panel.DesiredSize.Width));
            Assert.False(double.IsNaN(panel.DesiredSize.Height));
        });
    }

    private static void AssertDesignSystemResourcesLoad(string themeName)
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(LoadResourceDictionary($"Themes/{themeName}.xaml"));
        resources.MergedDictionaries.Add(LoadResourceDictionary("Themes/CommonStyles.xaml"));

        AssertStyle<Button>(resources, "ButtonPrimary");
        AssertStyle<Button>(resources, "ButtonSecondary");
        AssertStyle<Button>(resources, "ButtonGhost");
        AssertStyle<Button>(resources, "IconButton");
        AssertStyle<Button>(resources, "IconTextButton");
        AssertStyle<RadioButton>(resources, "SegmentedRadioButton");
        AssertStyle<Border>(resources, "Card");
        AssertStyle<TextBlock>(resources, "SectionHeader");
        AssertStyle<StackPanel>(resources, "FormField");
        AssertStyle<TextBlock>(resources, "HelpText");
        AssertStyle<Border>(resources, "StatusBadge");

        Assert.IsType<DataTemplate>(resources["IconTextButtonContentTemplate"]);
    }

    private static ResourceDictionary LoadResourceDictionary(string relativePath)
    {
        return Assert.IsType<ResourceDictionary>(Application.LoadComponent(
            new Uri($"/WattPilot;component/{relativePath}", UriKind.Relative)));
    }

    private static void AssertStyle<TTarget>(ResourceDictionary resources, string key)
        where TTarget : FrameworkElement
    {
        var style = Assert.IsType<Style>(resources[key]);
        Assert.Equal(typeof(TTarget), style.TargetType);
    }

    private static void RunOnStaThread(Action assertion)
    {
        Exception exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                assertion();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}

namespace SyncConfiguration.Views.AppDetails;

public partial class ConfigureAppPage : ContentPage
{
	public ConfigureAppPage()
	{
		InitializeComponent();
	}

    private void DetailsGrid_SizeChanged(object sender, EventArgs e)
    {
        var wide = DetailsGrid.Width >= 680;
        DetailsGrid.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
        View[] panels = [NamePanel, DescriptionPanel, EndpointPanel, TokenPanel, RefreshPanel];
        for (var index = 0; index < panels.Length; index++)
        {
            Grid.SetRow(panels[index], wide ? index / 2 : index);
            Grid.SetColumn(panels[index], wide ? index % 2 : 0);
        }
    }

    private void Entry_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Entry entry)
        {
            // Transform text to uppercase and replace spaces with hyphens
            string newText = e.NewTextValue?.ToUpper().Replace(" ", "-") ?? string.Empty;

            // Prevent infinite loop: Only update if text actually changed
            if (entry.Text != newText)
            {
                entry.Text = newText;
            }
        }
    }
}

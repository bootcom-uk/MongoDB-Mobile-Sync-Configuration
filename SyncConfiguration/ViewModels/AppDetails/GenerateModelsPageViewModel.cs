using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Services.CodeGeneration;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace SyncConfiguration.ViewModels.AppDetails;

public partial class GenerateModelsPageViewModel : ObservableObject, INavigationAware
{
    private EditCollectionModel collection;
    private ModelGenerationResult result;

    [ObservableProperty] private string collectionTitle;
    [ObservableProperty] private string className;
    [ObservableProperty] private string targetNamespace = "Models.Data";
    [ObservableProperty] private string baseNamespace = "MongoDB.Sync.Core.Services.Models.Models";
    [ObservableProperty] private string collectionMapping;
    [ObservableProperty] private bool selectedFieldsOnly;
    [ObservableProperty] private string sampleJson;
    [ObservableProperty] private string status;
    [ObservableProperty] private string reviewNotes;
    [ObservableProperty] private bool hasReviewNotes;
    [ObservableProperty] private ObservableCollection<GeneratedModelFile> files = [];
    [ObservableProperty] private GeneratedModelFile selectedFile;
    [ObservableProperty] private string previewCode;
    [ObservableProperty] private bool hasOutput;

    public void OnNavigatedTo(INavigationParameters parameters)
    {
        if (!parameters.TryGetValue<EditCollectionModel>("collection", out var source)) return;
        collection = source;
        CollectionTitle = $"{source.DatabaseName} / {source.CollectionName}";
        ClassName = CSharpModelGenerator.SuggestClassName(source.CollectionName);
        CollectionMapping = $"{source.DatabaseName}_{source.CollectionName}".Replace("-", "_");
        Generate();
    }

    public void OnNavigatedFrom(INavigationParameters parameters) { }

    partial void OnSelectedFileChanged(GeneratedModelFile value) => PreviewCode = value?.Code ?? string.Empty;
    partial void OnClassNameChanged(string value) => Invalidate();
    partial void OnTargetNamespaceChanged(string value) => Invalidate();
    partial void OnBaseNamespaceChanged(string value) => Invalidate();
    partial void OnCollectionMappingChanged(string value) => Invalidate();
    partial void OnSelectedFieldsOnlyChanged(bool value) => Invalidate();
    partial void OnSampleJsonChanged(string value) => Invalidate();

    private void Invalidate()
    {
        HasOutput = false;
        result = null;
        Files.Clear();
        SelectedFile = null;
        ReviewNotes = string.Empty;
        HasReviewNotes = false;
        Status = "Choose Generate to update the code preview.";
    }

    [RelayCommand]
    private void Generate()
    {
        Invalidate();
        if (collection == null) return;
        try
        {
            var schema = ModelType.FromCollection(collection, SelectedFieldsOnly);
            if (!string.IsNullOrWhiteSpace(SampleJson))
            {
                var sample = SampleSchemaReader.Read(SampleJson);
                if (SelectedFieldsOnly)
                {
                    var selectedNames = collection.Fields.Where(field => field.IsSelected).Select(field => field.Name).ToHashSet();
                    sample = sample.Where(field => selectedNames.Contains(field.Name)).ToList();
                }
                schema = SampleSchemaReader.Enrich(schema, sample);
            }
            result = new CSharpModelGenerator().Generate(schema,
                new(ClassName, TargetNamespace, BaseNamespace, CollectionMapping));
            Files = new(result.Files);
            SelectedFile = Files.FirstOrDefault();
            ReviewNotes = string.Join(Environment.NewLine, result.Warnings.Select(note => "• " + note));
            HasReviewNotes = result.Warnings.Count > 0;
            HasOutput = Files.Count > 0;
            Status = $"{Files.Count} class{(Files.Count == 1 ? "" : "es")} generated · {(SelectedFieldsOnly ? "selected sync fields" : "all available fields")}";
        }
        catch (JsonException)
        {
            Status = "The sample is not valid JSON. Paste a document or an array of documents (MongoDB Extended JSON is supported).";
        }
        catch (ArgumentException ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CopyClass()
    {
        if (!HasOutput || SelectedFile == null) return;
        await Copy(SelectedFile.Code, SelectedFile.FileName);
    }

    [RelayCommand]
    private async Task CopyAll()
    {
        if (!HasOutput || result == null) return;
        await Copy(result.CombinedCode, "All generated classes");
    }

    private async Task Copy(string code, string label)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(code);
            Status = $"{label} copied to the clipboard.";
        }
        catch (Exception) { Status = "Could not access the clipboard. Select and copy the code from the preview."; }
    }

    [RelayCommand]
    private async Task ExportZip()
    {
        if (!HasOutput || result == null) return;
        try
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in result.Files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(file.FileName).Open(), new UTF8Encoding(false));
                    writer.Write(file.Code);
                }
                using var readme = new StreamWriter(zip.CreateEntry("README.txt").Open(), new UTF8Encoding(false));
                readme.WriteLine("Add the .cs files to your project. Keep your existing BaseLocalCacheModel.");
                readme.WriteLine("Requires CommunityToolkit.Mvvm, LiteDB, and your MongoDB sync model packages.");
                readme.WriteLine("Confirm the namespace, base model namespace and CollectionName mapping before use.");
                readme.WriteLine("Review inferred types and unresolved shapes. The schema is based on sampled documents.");
                foreach (var warning in result.Warnings) readme.WriteLine(warning);
            }
            stream.Position = 0;
            var saved = await FileSaver.Default.SaveAsync(result.Files[0].ClassName + "-models.zip", stream, CancellationToken.None);
            Status = saved.IsSuccessful ? "Model files exported." : "Export was cancelled or could not be completed.";
        }
        catch (Exception) { Status = "Could not export the files. You can still copy the generated code."; }
    }
}

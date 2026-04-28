using Microsoft.Win32;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using static System.Net.Mime.MediaTypeNames;

using Image = System.Windows.Controls.Image;

namespace PicSerial
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string selectedFolder = string.Empty;
        private int imageCount = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedFolder = Path.GetDirectoryName(dialog.FileName);
                PreviewPanel.Children.Clear();
                imageCount = 0;
                UpdateStatus();

                // Show spinner + message
                StartSpinner();

                // Yield to UI so spinner/message actually render
                await Task.Delay(100);

                var existingFiles = Directory.GetFiles(selectedFolder, "*.*")
                                             .Where(f => IsImageFile(f))
                                             .OrderBy(f => f)
                                             .ToList();

                await Task.Run(() =>
                {
                    int processed = 0;
                    foreach (var file in existingFiles)
                    {
                        processed++;
                        string newFileName = Path.Combine(selectedFolder, processed.ToString("D4") + Path.GetExtension(file));

                        if (Path.GetFileName(file) != Path.GetFileName(newFileName))
                        {
                            File.Copy(file, newFileName, true);
                        }

                        Dispatcher.Invoke(() =>
                        {
                            AddThumbnail(newFileName, file);
                            imageCount = processed;
                            UpdateStatus();
                        });
                    }
                });

                StopSpinner();
                ShowToast("Folder images loaded successfully!");
            }
        }


        private void BtnClearPreviews_Click(object sender, RoutedEventArgs e)
        {
            PreviewPanel.Children.Clear();
            ShowToast("Previews cleared!");
        }

        private void BtnShowFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder))
                ShowToast("No folder selected yet.");
            else
                ShowToast("Current Folder: " + selectedFolder);
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder))
            {
                ShowToast("No folder selected yet.");
            }
            else
            {
                try
                {
                    Process.Start("explorer.exe", selectedFolder);
                }
                catch (Exception ex)
                {
                    ShowToast("Error opening folder: " + ex.Message);
                }
            }
        }

        private void DragArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = files.All(IsImageFile) ? DragDropEffects.Copy : DragDropEffects.None;
            }
        }

        private async void DragArea_Drop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder))
            {
                ShowToast("Please select a folder first.");
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var imageFiles = files.Where(IsImageFile).ToList();

            if (imageFiles.Count == 0) return;

            StartSpinner(); // show spinner instead of progress bar
            ShowToast("Copying files...");

            await Task.Run(() =>
            {
                foreach (string file in imageFiles)
                {
                    imageCount++;
                    string newFileName = Path.Combine(selectedFolder, imageCount.ToString("D4") + Path.GetExtension(file));
                    File.Copy(file, newFileName, true);

                    Dispatcher.Invoke(() =>
                    {
                        AddThumbnail(newFileName, file);
                        UpdateStatus();
                    });
                }
            });

            StopSpinner(); // hide spinner when done
            ShowToast("Images copied successfully!");
        }


        private bool IsImageFile(string file)
        {
            string ext = Path.GetExtension(file).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif";
        }

        private void AddThumbnail(string newFilePath, string originalFilePath)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(newFilePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // release file lock
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();

                // Thumbnail image
                Image img = new Image
                {
                    Source = bitmap,
                    Width = 100,
                    Height = 100,
                    Margin = new Thickness(5)
                };

                // Show NEW file name under thumbnail
                TextBlock txt = new TextBlock
                {
                    Text = Path.GetFileName(newFilePath),
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5, 0, 5, 5),
                    ToolTip = $"Original: {Path.GetFileName(originalFilePath)}\nPath: {originalFilePath}"
                };

                // StackPanel to hold thumbnail + name
                StackPanel panel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(5),
                    Tag = new { NewFile = newFilePath, OriginalFile = originalFilePath }
                };
                panel.Children.Add(img);
                panel.Children.Add(txt);

                // Context menu
                ContextMenu menu = new ContextMenu();

                MenuItem openNew = new MenuItem { Header = "Open New File" };
                openNew.Click += (s, e) => Process.Start("explorer.exe", newFilePath);

                MenuItem openOriginal = new MenuItem { Header = "Open Original File" };
                openOriginal.Click += (s, e) => Process.Start("explorer.exe", originalFilePath);

                MenuItem deleteFile = new MenuItem { Header = "Delete New File" };
                deleteFile.Click += (s, e) =>
                {
                    try
                    {
                        if (File.Exists(newFilePath))
                        {
                            File.Delete(newFilePath);
                            PreviewPanel.Children.Remove(panel);
                            ShowToast($"Deleted: {Path.GetFileName(newFilePath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowToast("Error deleting file: " + ex.Message);
                    }
                };

                MenuItem deleteOriginal = new MenuItem { Header = "Delete Original File" };
                deleteOriginal.Click += (s, e) =>
                {
                    try
                    {
                        if (File.Exists(originalFilePath))
                        {
                            File.Delete(originalFilePath);
                            ShowToast($"Deleted original: {Path.GetFileName(originalFilePath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowToast("Error deleting original file: " + ex.Message);
                    }
                };

                menu.Items.Add(openNew);
                menu.Items.Add(openOriginal);
                menu.Items.Add(new Separator());
                menu.Items.Add(deleteFile);
                menu.Items.Add(deleteOriginal);

                panel.ContextMenu = menu;

                PreviewPanel.Children.Add(panel);
            }
            catch (Exception ex)
            {
                ShowToast("Error loading thumbnail: " + ex.Message);
            }
        }



        private void UpdateStatus()
        {
            StatusFolder.Text = string.IsNullOrEmpty(selectedFolder)
                ? "No folder selected"
                : $"Folder: {selectedFolder}";
            StatusCounter.Text = $"Images copied: {imageCount}";
        }

        private void StartSpinner(string message = "Loading images...")
        {
            LoadingText.Text = message;
            LoaderOverlay.Visibility = Visibility.Visible;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            LoaderOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };

            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
        }

        private void StopSpinner()
        {
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => LoaderOverlay.Visibility = Visibility.Collapsed;
            LoaderOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private async void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastMessage.Visibility = Visibility.Visible;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            ToastMessage.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Keep visible for 2 seconds
            await Task.Delay(2000);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => ToastMessage.Visibility = Visibility.Collapsed;
            ToastMessage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }


    }
}